using System.IO;
using System.Text.RegularExpressions;

namespace SorenB.TenantSwitcher.Services;

/// <summary>
/// Locates UiRobot.exe, UiPath.Assistant.exe and the uip CLI across the install locations that
/// vary by UiPath version and per-machine vs per-user install. Same order as the Studio
/// extension's robotLocator.ts.
/// </summary>
public static class UiPathLocator
{
    private static readonly string[] UiRobotRelativePaths =
    {
        @"UiPath\Studio\UiRobot.exe",
        @"UiPath\Robot\UiRobot.exe",
    };

    private static readonly string[] AssistantRelativePaths =
    {
        @"UiPath\Studio\UiPathAssistant\UiPath.Assistant.exe",
        @"UiPath\Robot\UiPathAssistant\UiPath.Assistant.exe",
        @"UiPath\UiPathAssistant\UiPath.Assistant.exe",
    };

    /// <summary>
    /// Finds UiRobot.exe. Order: the explicit setting (which must exist), the per-user UiPath
    /// Platform install (%LocalAppData%\Programs\UiPathPlatform\Robot\&lt;version&gt;), then
    /// classic Studio/Robot installs.
    /// </summary>
    public static string? FindUiRobotExe(string? userOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(userOverride))
        {
            return File.Exists(userOverride) ? userOverride : null;
        }

        return Find(UiRobotRelativePaths, "UiRobot.exe", "Robot");
    }

    public static string? FindAssistantExe() =>
        Find(AssistantRelativePaths, "UiPath.Assistant.exe", "Assistant");

    /// <summary>Resolves the uip CLI: a full path from settings, the global npm shim, or a bare command left to PATH.</summary>
    public static string FindUip(string? setting)
    {
        var value = string.IsNullOrWhiteSpace(setting) ? "uip" : setting.Trim();
        if (value != "uip")
        {
            return value;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            var shim = Path.Combine(appData, "npm", "uip.cmd");
            if (File.Exists(shim))
            {
                return shim;
            }
        }

        return value;
    }

    /// <summary>
    /// Compares UiPath version folder names such as "26.0.203-cloud.25204" numerically,
    /// segment by segment ("26.0.203-cloud.9" &lt; "26.0.203-cloud.25204" &lt; "26.0.210").
    /// </summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = Regex.Matches(a, @"\d+").Select(m => long.Parse(m.Value)).ToArray();
        var pb = Regex.Matches(b, @"\d+").Select(m => long.Parse(m.Value)).ToArray();
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var d = (i < pa.Length ? pa[i] : -1).CompareTo(i < pb.Length ? pb[i] : -1);
            if (d != 0)
            {
                return d;
            }
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Newest &lt;root&gt;\&lt;version&gt;\&lt;exe&gt; that exists.</summary>
    public static string? FindInVersionedFolders(string componentRoot, string exeFileName)
    {
        if (!Directory.Exists(componentRoot))
        {
            return null;
        }

        // Several version folders can exist briefly during an update; prefer the newest.
        return Directory.GetDirectories(componentRoot)
            .Select(Path.GetFileName)
            .OrderByDescending(name => name!, Comparer<string>.Create(CompareVersions))
            .Select(name => Path.Combine(componentRoot, name!, exeFileName))
            .FirstOrDefault(File.Exists);
    }

    private static string? Find(IEnumerable<string> relativeCandidates, string exeFileName, string platformComponent)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var componentRoot = Path.Combine(localAppData, "Programs", "UiPathPlatform", platformComponent);
            var versioned = FindInVersionedFolders(componentRoot, exeFileName);
            if (versioned is not null)
            {
                return versioned;
            }
        }

        foreach (var root in GetSearchRoots())
        {
            foreach (var relative in relativeCandidates)
            {
                var candidate = Path.Combine(root, relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrEmpty(programFiles))
        {
            yield return programFiles;
        }

        if (!string.IsNullOrEmpty(programFilesX86) && programFilesX86 != programFiles)
        {
            yield return programFilesX86;
        }

        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return Path.Combine(localAppData, "Programs");
        }
    }
}
