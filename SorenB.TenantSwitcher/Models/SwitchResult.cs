namespace SorenB.TenantSwitcher.Models;

public enum StepOutcome
{
    Ok,
    Skipped,
    Warning,
    Failed,
}

public sealed record SwitchStep(string Name, StepOutcome Outcome, string Detail = "");

public enum ResultSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>Outcome of one tenant switch, shown in the result banner until dismissed.</summary>
public sealed record SwitchResult(
    Guid TargetId,
    string TargetName,
    bool Success,
    bool RolledBack,
    ResultSeverity Severity,
    string Message,
    IReadOnlyList<SwitchStep> Steps,
    DateTimeOffset FinishedAt);
