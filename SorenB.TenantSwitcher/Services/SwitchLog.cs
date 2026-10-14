using System.IO;

namespace SorenB.TenantSwitcher.Services;

/// <summary>
/// Appends switch steps to switch.log in %LocalAppData%\SorenB\TenantSwitcher. Every line is
/// passed through <see cref="Mask"/> so client secrets never reach the file.
/// </summary>
public sealed class SwitchLog
{
    public const string Masked = "********";

    private readonly object _lock = new();

    public SwitchLog(string? filePathOverride = null)
    {
        FilePath = filePathOverride ?? Path.Combine(AppStateStore.DefaultDirectory, "switch.log");
    }

    public string FilePath { get; }

    public void Write(string jobId, string line, IEnumerable<string> secrets)
    {
        var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{jobId}] {Mask(line, secrets)}{Environment.NewLine}";
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, text);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log is diagnostic only; a locked or read-only file must not fail a switch.
        }
    }

    /// <summary>Replaces every non-empty secret in <paramref name="text"/> with <see cref="Masked"/> and trims it.</summary>
    public static string Mask(string? text, IEnumerable<string> secrets)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        foreach (var secret in secrets)
        {
            if (!string.IsNullOrEmpty(secret))
            {
                text = text.Replace(secret, Masked, StringComparison.Ordinal);
            }
        }

        return text.Trim();
    }
}
