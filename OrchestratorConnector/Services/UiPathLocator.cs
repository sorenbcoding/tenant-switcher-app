using System.IO;
using System.Linq;

namespace OrchestratorConnector.Services;

/// <summary>
/// Locates UiRobot.exe / UiPath.Assistant.exe across the install locations that vary by
/// UiPath version and per-machine-vs-per-user install, instead of a single hardcoded path.
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

    public static string? FindUiRobotExe(string? userOverride = null) =>
        Find(UiRobotRelativePaths, "UiRobot.exe", "Robot", userOverride);

    public static string? FindAssistantExe(string? userOverride = null) =>
        Find(AssistantRelativePaths, "UiPath.Assistant.exe", "Assistant", userOverride);

    private static string? Find(IEnumerable<string> relativeCandidates, string exeFileName, string platformComponent, string? userOverride)
    {
        if (!string.IsNullOrWhiteSpace(userOverride) && File.Exists(userOverride))
        {
            return userOverride;
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

        // Modern per-user "UiPath Platform" installs place each component under a
        // version-numbered folder instead, e.g.
        // %LocalAppData%\Programs\UiPathPlatform\Robot\26.0.202-cloud.25004\UiRobot.exe
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

        return null;
    }

    private static string? FindInVersionedFolders(string componentRoot, string exeFileName)
    {
        if (!Directory.Exists(componentRoot))
        {
            return null;
        }

        // Multiple version folders can exist briefly during an update; prefer the newest.
        return Directory.GetDirectories(componentRoot)
            .OrderByDescending(directory => directory, StringComparer.OrdinalIgnoreCase)
            .Select(directory => Path.Combine(directory, exeFileName))
            .FirstOrDefault(File.Exists);
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
