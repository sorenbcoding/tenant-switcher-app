using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceProcess;
using SorenB.TenantSwitcher.Models;

namespace SorenB.TenantSwitcher.Services;

public enum PreflightStatus
{
    Ready,
    Blocked,
    Unverified,
}

public sealed record Preflight(PreflightStatus Status, string Message = "", bool OpenSettings = false);

/// <summary>Everything one switch needs; resolved by the caller from settings and app state.</summary>
public sealed record SwitchRequest(Preset Target, Preset? Previous, AppSettings Settings, bool SyncUipCli);

/// <summary>
/// Switches the local Robot to another tenant. Runs the same steps as the Studio extension's
/// scripts/switch.ps1 (close Assistant, disconnect, connect with rollback, restart the Robot
/// service, start Assistant, sync the uip CLI), in-process because a desktop app is not
/// restarted by the tenant change. Secrets are read from Credential Manager here and masked
/// in every step detail and log line.
/// </summary>
public sealed class TenantSwitchService
{
    private const string AssistantProcessName = "UiPath.Assistant";
    private const string RobotServiceName = "UiPath Robot";
    private const string UipSecretVariable = "OC_UIP_CLIENT_SECRET";

    private static readonly TimeSpan RobotTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan UipTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan AssistantExitTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(30);

    private readonly IdentityClient _identity;
    private readonly SwitchLog _log;
    private int _running;

    public TenantSwitchService(IdentityClient identity, SwitchLog log)
    {
        _identity = identity;
        _log = log;
    }

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>Checks that a switch can start: Robot found, secret stored and (optionally) accepted by Identity Server.</summary>
    public async Task<Preflight> PreflightAsync(Preset target, AppSettings settings, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            return new(PreflightStatus.Blocked, "A tenant switch is already running. Wait for it to finish, then switch again.");
        }

        if (UiPathLocator.FindUiRobotExe(settings.UiRobotPath) is null)
        {
            var message = string.IsNullOrWhiteSpace(settings.UiRobotPath)
                ? "UiRobot.exe was not found — no UiPath Robot install in the default locations. Set the UiRobot.exe path in Settings."
                : $"UiRobot.exe was not found — \"{settings.UiRobotPath}\" does not exist. Correct the UiRobot.exe path in Settings.";
            return new(PreflightStatus.Blocked, message, OpenSettings: true);
        }

        var secret = CredentialService.TryReadSecret(target.Id);
        if (string.IsNullOrEmpty(secret))
        {
            return new(PreflightStatus.Blocked,
                $"No client secret is stored for \"{target.PresetName}\" — it is missing from Windows Credential Manager. Edit the preset and enter the client secret.");
        }

        if (settings.ValidateBeforeSwitch)
        {
            var result = await _identity.ValidateAsync(target.OrchestratorUrl, target.ClientId, secret, ct).ConfigureAwait(false);
            if (result.Status == ValidationStatus.Rejected)
            {
                return new(PreflightStatus.Blocked, $"The switch to \"{target.PresetName}\" was not started. {result.Message}");
            }

            if (result.Status == ValidationStatus.Unknown)
            {
                return new(PreflightStatus.Unverified, result.Message);
            }
        }

        return new(PreflightStatus.Ready);
    }

    /// <summary>Runs the switch. Never throws; failures are reported in the result.</summary>
    /// <param name="progress">Receives the name of each step as it starts.</param>
    public async Task<SwitchResult> SwitchAsync(SwitchRequest request, IProgress<string>? progress = null)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return Fail(request.Target, Array.Empty<SwitchStep>(), "A tenant switch is already running. Wait for it to finish, then switch again.", rolledBack: false);
        }

        try
        {
            return await Task.Run(() => new SwitchRun(this, request, progress).Execute()).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private static SwitchResult Fail(Preset target, IReadOnlyList<SwitchStep> steps, string message, bool rolledBack) =>
        new(target.Id, target.PresetName, false, rolledBack, ResultSeverity.Error,
            $"The switch to \"{target.PresetName}\" failed — {message}", steps, DateTimeOffset.Now);

    /// <summary>State of one switch: its steps, the secrets to mask and the job id in the log.</summary>
    private sealed class SwitchRun
    {
        private readonly TenantSwitchService _owner;
        private readonly SwitchRequest _request;
        private readonly IProgress<string>? _progress;
        private readonly List<SwitchStep> _steps = new();
        private readonly List<string> _secrets = new();
        private readonly string _jobId = Guid.NewGuid().ToString("N")[..8];

        public SwitchRun(TenantSwitchService owner, SwitchRequest request, IProgress<string>? progress)
        {
            _owner = owner;
            _request = request;
            _progress = progress;
        }

        private AppSettings Settings => _request.Settings;

        private Preset Target => _request.Target;

        public SwitchResult Execute()
        {
            try
            {
                Log($"Switching to '{Target.PresetName}' ({Target.OrchestratorUrl})");
                var uiRobot = UiPathLocator.FindUiRobotExe(Settings.UiRobotPath)
                    ?? throw new InvalidOperationException("UiRobot.exe was not found. Set the UiRobot.exe path in Settings.");
                var assistant = UiPathLocator.FindAssistantExe();

                // 1. Close Assistant so it reconnects cleanly afterwards.
                if (Settings.RestartAssistant)
                {
                    Begin("Close Assistant");
                    var closed = CloseAssistant();
                    Add("Close Assistant", closed > 0 ? StepOutcome.Ok : StepOutcome.Skipped,
                        closed > 0 ? $"{closed} process(es) closed" : "Assistant was not running");
                }

                // 2. Disconnect. A non-zero exit usually means "already disconnected", so it is only a warning.
                Begin("Disconnect Robot");
                var r = Run(uiRobot, new[] { "disconnect" }, RobotTimeout);
                Add("Disconnect Robot", r.Code == 0 ? StepOutcome.Ok : StepOutcome.Warning, $"exit {r.Code} {r.Output}");

                // 3. Connect to the target tenant, rolling back to the previous one on failure.
                Begin("Connect Robot");
                r = Connect(uiRobot, Target);
                if (r.Code == 0)
                {
                    Add("Connect Robot", StepOutcome.Ok, Target.OrchestratorUrl);
                }
                else
                {
                    Add("Connect Robot", StepOutcome.Failed, $"exit {r.Code} {r.Output}");
                    var output = r.Output.Length > 0 ? r.Output.TrimEnd('.') : "no output";
                    var reason = $"UiRobot connect returned exit code {r.Code} ({output}).";
                    var rolledBack = false;
                    var previous = _request.Previous;
                    if (previous is not null)
                    {
                        var step = $"Reconnect previous ({previous.PresetName})";
                        Begin(step);
                        try
                        {
                            var rb = Connect(uiRobot, previous);
                            rolledBack = rb.Code == 0;
                            Add(step, rolledBack ? StepOutcome.Ok : StepOutcome.Failed,
                                rolledBack ? previous.OrchestratorUrl : $"exit {rb.Code} {rb.Output}");
                        }
                        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                        {
                            Add(step, StepOutcome.Failed, ex.Message);
                        }

                        reason += rolledBack
                            ? $" Reconnected to '{previous.PresetName}'."
                            : $" Reconnecting to '{previous.PresetName}' also failed, so the Robot is disconnected.";
                    }
                    else
                    {
                        reason += " No previously connected preset is known, so the Robot is disconnected.";
                    }

                    reason += " Check the machine client ID and secret in Orchestrator, then switch again.";
                    if (Settings.RestartAssistant && assistant is not null)
                    {
                        TryStart(assistant);
                    }

                    return Finish(false, reason, rolledBack);
                }

                // 4. Restart the Robot Windows service, if this install has one (service mode).
                Begin("Restart Robot service");
                RestartRobotService();

                // 5. Start Assistant again.
                if (Settings.RestartAssistant)
                {
                    Begin("Start Assistant");
                    if (assistant is not null && TryStart(assistant))
                    {
                        Add("Start Assistant", StepOutcome.Ok);
                    }
                    else
                    {
                        Add("Start Assistant", StepOutcome.Warning, "UiPath.Assistant.exe was not found. Start UiPath Assistant manually.");
                    }
                }

                // 6. Optionally point the uip CLI at the same tenant.
                if (_request.SyncUipCli)
                {
                    Begin("Sync uip CLI login");
                    SyncUipCli();
                }

                return Finish(true, $"Connected to '{Target.PresetName}'.", rolledBack: false);
            }
            catch (Exception ex)
            {
                Add("Unexpected error", StepOutcome.Failed, ex.Message);
                return Finish(false, ex.Message, rolledBack: false);
            }
        }

        private static int CloseAssistant()
        {
            var processes = Process.GetProcessesByName(AssistantProcessName);
            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit((int)AssistantExitTimeout.TotalMilliseconds);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                    {
                        // Already exited, or exiting.
                    }
                }
            }

            return processes.Length;
        }

        private (int Code, string Output) Connect(string uiRobot, Preset preset)
        {
            var secret = CredentialService.TryReadSecret(preset.Id);
            if (string.IsNullOrEmpty(secret))
            {
                throw new InvalidOperationException(
                    $"No client secret is stored for preset '{preset.PresetName}'. Edit the preset and enter the client secret.");
            }

            _secrets.Add(secret);
            return Run(uiRobot, new[] { "connect", "--url", preset.OrchestratorUrl, "--clientId", preset.ClientId, "--clientSecret", secret }, RobotTimeout);
        }

        private void RestartRobotService()
        {
            const string step = "Restart Robot service";
            if (!ServiceController.GetServices().Any(s => s.ServiceName.Equals(RobotServiceName, StringComparison.OrdinalIgnoreCase)))
            {
                Add(step, StepOutcome.Skipped, "No UiPath Robot service (user-mode install)");
                return;
            }

            try
            {
                if (IsAdministrator())
                {
                    using var service = new ServiceController(RobotServiceName);
                    if (service.Status != ServiceControllerStatus.Stopped)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                    }

                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                }
                else
                {
                    var psi = new ProcessStartInfo("powershell.exe")
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Arguments = "-NoProfile -Command \"Restart-Service -Name 'UiPath Robot' -Force\"",
                    };
                    using var elevated = Process.Start(psi) ?? throw new InvalidOperationException("The elevated service restart did not start.");
                    elevated.WaitForExit();
                    if (elevated.ExitCode != 0)
                    {
                        throw new InvalidOperationException(
                            $"The elevated service restart returned exit code {elevated.ExitCode}. Restart the UiPath Robot service manually.");
                    }
                }

                Add(step, StepOutcome.Ok);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Add(step, StepOutcome.Warning, "The administrator prompt was declined. Restart the UiPath Robot service manually.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException)
            {
                Add(step, StepOutcome.Warning, ex.Message);
            }
        }

        private void SyncUipCli()
        {
            const string step = "Sync uip CLI login";
            try
            {
                var parts = OrchestratorUrl.GetTenantParts(Target.OrchestratorUrl)
                    ?? throw new InvalidOperationException("The uip CLI needs an Automation Cloud or Automation Suite URL with organization and tenant.");
                var secret = CredentialService.TryReadSecret(Target.Id)
                    ?? throw new InvalidOperationException($"No client secret is stored for preset '{Target.PresetName}'.");
                _secrets.Add(secret);

                var args = new List<string>
                {
                    "login", "--authority", parts.Authority, "--client-id", Target.ClientId,
                    "--client-secret", "env." + UipSecretVariable, "--output", "json",
                    "--organization", parts.Organization, "--tenant", parts.Tenant,
                };
                var r = Run(UiPathLocator.FindUip(Settings.UipCliPath), args, UipTimeout, new() { [UipSecretVariable] = secret });
                Add(step, r.Code == 0 ? StepOutcome.Ok : StepOutcome.Warning, $"exit {r.Code} {r.Output}");
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                Add(step, StepOutcome.Warning, ex.Message);
            }
        }

        /// <summary>Runs a process without a window and returns its exit code and output; -999 on timeout.</summary>
        private static (int Code, string Output) Run(string file, IEnumerable<string> arguments, TimeSpan timeout, Dictionary<string, string>? environment = null)
        {
            // .cmd shims (e.g. the npm uip.cmd) and bare commands are resolved by cmd.exe.
            var direct = file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            var psi = new ProcessStartInfo(direct ? file : "cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (!direct)
            {
                psi.ArgumentList.Add("/d");
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(file);
            }

            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            foreach (var (key, value) in environment ?? new())
            {
                psi.Environment[key] = value;
            }

            using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {Path.GetFileName(file)}.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // Exited in the meantime.
                }

                return (-999, $"Timed out after {timeout.TotalSeconds:0} s");
            }

            process.WaitForExit();
            return (process.ExitCode, (stdout.Result + "\n" + stderr.Result).Trim());
        }

        private static bool TryStart(string path)
        {
            try
            {
                using var _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                return false;
            }
        }

        private static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void Begin(string step) => _progress?.Report(step);

        private void Add(string name, StepOutcome outcome, string detail = "")
        {
            var masked = SwitchLog.Mask(detail, _secrets);
            _steps.Add(new SwitchStep(name, outcome, masked));
            Log($"{name} -> {outcome.ToString().ToLowerInvariant()} {masked}");
        }

        private void Log(string line) => _owner._log.Write(_jobId, line, _secrets);

        private SwitchResult Finish(bool success, string message, bool rolledBack)
        {
            message = SwitchLog.Mask(message, _secrets);
            Log($"Finished: success={success} {message}");
            if (!success)
            {
                return Fail(Target, _steps, message, rolledBack);
            }

            var warnings = _steps.Where(s => s.Outcome == StepOutcome.Warning).Select(s => s.Name).ToList();
            return warnings.Count > 0
                ? new(Target.Id, Target.PresetName, true, false, ResultSeverity.Warning,
                    $"{message} Some steps need attention: {string.Join(", ", warnings)}. See the log for details.", _steps, DateTimeOffset.Now)
                : new(Target.Id, Target.PresetName, true, false, ResultSeverity.Info, message, _steps, DateTimeOffset.Now);
        }
    }
}
