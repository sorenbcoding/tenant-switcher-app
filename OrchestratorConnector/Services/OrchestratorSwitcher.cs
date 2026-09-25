using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using OrchestratorConnector.Models;

namespace OrchestratorConnector.Services;

public sealed record SwitchProgress(string Message, bool IsError);

/// <summary>
/// Runs the kill Assistant -> UiRobot --disconnect -> UiRobot --connect -> restart service (if
/// present) -> relaunch Assistant sequence. Hardened version of the original buttonConnect_Click:
/// real waits instead of fixed delays, ArgumentList instead of an interpolated argument string,
/// dynamic executable discovery, and error reporting instead of silent failure.
/// </summary>
public sealed class OrchestratorSwitcher
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(20);

    private const string AssistantProcessName = "UiPath.Assistant";
    private const string RobotServiceName = "UiPath Robot";

    public event EventHandler<SwitchProgress>? ProgressChanged;

    public async Task<bool> SwitchAsync(Preset preset, string clientSecret, CancellationToken ct = default)
    {
        var uiRobotPath = UiPathLocator.FindUiRobotExe();
        if (uiRobotPath is null)
        {
            Report("UiRobot.exe was not found on this machine.", isError: true);
            return false;
        }

        if (!await KillAssistantAsync(ct))
        {
            return false;
        }

        if (!await RunUiRobotAsync(uiRobotPath, new[] { "--disconnect" }, "Disconnecting current tenant...", ct))
        {
            return false;
        }

        if (!await RunUiRobotAsync(
                uiRobotPath,
                new[] { "--connect", "-url", preset.OrchestratorUrl, "-clientid", preset.ClientId, "-clientsecret", clientSecret },
                "Connecting to tenant...",
                ct))
        {
            return false;
        }

        // Not fatal: newer per-user UiPath Robot installs don't run as a Windows service at
        // all, so there's nothing to restart there, and the tenant switch already took
        // effect via UiRobot.exe --connect above regardless.
        await RestartServiceAsync(ct);

        var assistantPath = UiPathLocator.FindAssistantExe();
        if (assistantPath is not null)
        {
            LaunchAssistant(assistantPath);
        }
        else
        {
            Report("UiPath Assistant was not found - start it manually.", isError: true);
        }

        Report("Done!", isError: false);
        return true;
    }

    private async Task<bool> KillAssistantAsync(CancellationToken ct)
    {
        Report("Shutting down UiPath Assistant...", isError: false);
        try
        {
            foreach (var process in Process.GetProcessesByName(AssistantProcessName))
            {
                using (process)
                {
                    process.Kill();
                    await process.WaitForExitAsync(ct).WaitAsync(ProcessTimeout, ct);
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.TimeoutException)
        {
            Report($"Could not stop UiPath Assistant: {ex.Message}", isError: true);
            return false;
        }
    }

    private async Task<bool> RunUiRobotAsync(string uiRobotPath, IReadOnlyList<string> arguments, string statusMessage, CancellationToken ct)
    {
        Report(statusMessage, isError: false);
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = uiRobotPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Report($"Failed to start {Path.GetFileName(uiRobotPath)}.", isError: true);
                return false;
            }

            await process.WaitForExitAsync(ct).WaitAsync(ProcessTimeout, ct);
            return true;
        }
        catch (System.TimeoutException)
        {
            Report($"{Path.GetFileName(uiRobotPath)} did not finish in time.", isError: true);
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Report($"Failed to run UiRobot.exe: {ex.Message}", isError: true);
            return false;
        }
    }

    private const int ErrorServiceDoesNotExist = 1060;

    private async Task<bool> RestartServiceAsync(CancellationToken ct)
    {
        Report("Restarting UiPath Robot service...", isError: false);
        try
        {
            await Task.Run(
                () =>
                {
                    using var serviceController = new ServiceController(RobotServiceName);
                    if (serviceController.Status != ServiceControllerStatus.Stopped)
                    {
                        serviceController.Stop();
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                    }

                    serviceController.Start();
                    serviceController.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
                },
                ct);

            return true;
        }
        catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception { NativeErrorCode: ErrorServiceDoesNotExist })
        {
            // Newer per-user UiPath Robot installs (e.g. "UiPath Platform") don't run as a
            // Windows service at all - nothing to restart, and that's expected, not an error.
            Report("No \"UiPath Robot\" Windows service on this machine - skipping (not used by this UiPath install).", isError: false);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ServiceProcess.TimeoutException or Win32Exception)
        {
            Report($"Could not restart the UiPath Robot service: {ex.Message}", isError: true);
            return false;
        }
    }

    private void LaunchAssistant(string assistantPath)
    {
        Report("Starting UiPath Assistant...", isError: false);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = assistantPath,
                UseShellExecute = false,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Report($"Could not start UiPath Assistant: {ex.Message}", isError: true);
        }
    }

    private void Report(string message, bool isError) => ProgressChanged?.Invoke(this, new SwitchProgress(message, isError));
}
