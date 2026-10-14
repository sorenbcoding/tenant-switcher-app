using System.Windows.Input;

namespace SorenB.TenantSwitcher.ViewModels;

public enum BannerSeverity
{
    Info,
    Warning,
    Error,
    Progress,
}

/// <summary>One banner at the top of the presets view: switch progress, the last result, or a notice.</summary>
public sealed class BannerViewModel
{
    public BannerViewModel(BannerSeverity severity, string message, string detail = "")
    {
        Severity = severity;
        Message = message;
        Detail = detail;
    }

    public BannerSeverity Severity { get; }

    /// <summary>Severity as text, for the BannerBar style trigger.</summary>
    public string SeverityName => Severity.ToString();

    public string Message { get; }

    public string Detail { get; }

    public bool IsAlert => Severity == BannerSeverity.Error;

    public ICommand? OkCommand { get; init; }

    public ICommand? ShowLogCommand { get; init; }

    public ICommand? OpenSettingsCommand { get; init; }

    public bool HasButtons => OkCommand is not null || ShowLogCommand is not null || OpenSettingsCommand is not null;
}
