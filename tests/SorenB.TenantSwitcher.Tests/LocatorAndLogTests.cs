using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

public sealed class LocatorAndLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sb-ts-tests-" + Guid.NewGuid().ToString("N"));

    public LocatorAndLogTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void CompareVersions_sorts_numerically_segment_by_segment()
    {
        var sorted = new[] { "26.0.210", "26.0.203-cloud.25204", "26.0.203-cloud.9", "25.10.1" }
            .OrderBy(v => v, Comparer<string>.Create(UiPathLocator.CompareVersions))
            .ToArray();
        Assert.Equal(new[] { "25.10.1", "26.0.203-cloud.9", "26.0.203-cloud.25204", "26.0.210" }, sorted);
    }

    [Fact]
    public void FindInVersionedFolders_picks_newest_existing_exe()
    {
        foreach (var v in new[] { "26.0.203-cloud.9", "26.0.203-cloud.25204", "26.0.300" })
        {
            Directory.CreateDirectory(Path.Combine(_dir, v));
        }

        File.WriteAllText(Path.Combine(_dir, "26.0.203-cloud.9", "UiRobot.exe"), "");
        File.WriteAllText(Path.Combine(_dir, "26.0.203-cloud.25204", "UiRobot.exe"), "");
        // 26.0.300 has no exe (a half-finished update) and must be skipped.

        Assert.Equal(Path.Combine(_dir, "26.0.203-cloud.25204", "UiRobot.exe"), UiPathLocator.FindInVersionedFolders(_dir, "UiRobot.exe"));
    }

    [Fact]
    public void FindUiRobotExe_returns_null_for_missing_override()
    {
        Assert.Null(UiPathLocator.FindUiRobotExe(Path.Combine(_dir, "missing", "UiRobot.exe")));
    }

    [Fact]
    public void Log_masks_secrets()
    {
        Assert.Equal("connect --clientSecret ******** done", SwitchLog.Mask(" connect --clientSecret s3cr3t done ", new[] { "", "s3cr3t" }));

        var log = new SwitchLog(Path.Combine(_dir, "switch.log"));
        log.Write("job1", "secret is s3cr3t", new[] { "s3cr3t" });
        var text = File.ReadAllText(log.FilePath);
        Assert.Contains("[job1] secret is ********", text);
        Assert.DoesNotContain("s3cr3t", text);
    }
}
