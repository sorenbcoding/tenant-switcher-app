using System.Text;
using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

public sealed class AssistantSettingsTests : IDisposable
{
    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sb-ts-tests-" + Guid.NewGuid().ToString("N"));

    public AssistantSettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // Shape of a real agent-settings.json, trimmed.
    private static string Settings(bool show) =>
        "{\"agent\":{\"language\":\"da\",\"theme\":\"light\",\"useFolderView\":false,\"switchToRunningTab\":true,\"showStartMenuShortcuts\":"
        + (show ? "true" : "false")
        + ",\"hiddenCustomTiles\":[],\"zoomLevel\":100},\"electron\":{\"window\":{\"position\":{\"x\":10,\"y\":20}}},\"user\":{\"identifier\":\"æøå\"}}";

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string Write(string name, string content) => Write(name, Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Reads_true_false_and_a_bom()
    {
        Assert.True(AssistantSettings.ReadShowStartMenuShortcuts(Write("on.json", Settings(true))));
        Assert.False(AssistantSettings.ReadShowStartMenuShortcuts(Write("off.json", Settings(false))));
        Assert.True(AssistantSettings.ReadShowStartMenuShortcuts(Write("bom.json", Bom.Concat(Encoding.UTF8.GetBytes(Settings(true))).ToArray())));
    }

    [Theory]
    [InlineData("{\"agent\":{}}")]
    [InlineData("null")]
    [InlineData("{\"agent\":")]
    [InlineData("{\"agent\":{\"showStartMenuShortcuts\":\"yes\"}}")]
    public void Reads_null_when_the_key_or_valid_json_is_missing(string content)
    {
        Assert.Null(AssistantSettings.ReadShowStartMenuShortcuts(Write("x.json", content)));
    }

    [Fact]
    public void Reads_null_when_the_file_is_missing()
    {
        Assert.Null(AssistantSettings.ReadShowStartMenuShortcuts(Path.Combine(_dir, "missing.json")));
    }

    [Fact]
    public void Disable_changes_only_the_one_value()
    {
        var path = Write("turn-off.json", Settings(true));

        Assert.Equal(StartMenuChange.Changed, AssistantSettings.DisableStartMenuShortcuts(path));

        Assert.Equal(Encoding.UTF8.GetBytes(Settings(false)), File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Disable_keeps_a_bom()
    {
        var path = Write("turn-off-bom.json", Bom.Concat(Encoding.UTF8.GetBytes(Settings(true))).ToArray());

        Assert.Equal(StartMenuChange.Changed, AssistantSettings.DisableStartMenuShortcuts(path));

        Assert.Equal(Bom.Concat(Encoding.UTF8.GetBytes(Settings(false))).ToArray(), File.ReadAllBytes(path));
    }

    [Fact]
    public void Disable_leaves_the_file_alone_when_already_off_or_missing()
    {
        var path = Write("already-off.json", Settings(false));
        var before = File.GetLastWriteTimeUtc(path);

        Assert.Equal(StartMenuChange.Unchanged, AssistantSettings.DisableStartMenuShortcuts(path));
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        Assert.Equal(StartMenuChange.Missing, AssistantSettings.DisableStartMenuShortcuts(Path.Combine(_dir, "nope.json")));
    }

    [Fact]
    public void Warns_only_when_the_setting_is_on_and_the_warning_is_enabled()
    {
        var on = Write("on.json", Settings(true));
        var off = Write("off.json", Settings(false));

        Assert.True(TenantSwitchService.StartMenuWarning(new AppSettings(), on));
        Assert.False(TenantSwitchService.StartMenuWarning(new AppSettings { RestartAssistant = false }, on));
        Assert.Null(TenantSwitchService.StartMenuWarning(new AppSettings { WarnAboutStartMenuShortcuts = false }, on));
        Assert.Null(TenantSwitchService.StartMenuWarning(new AppSettings(), off));
    }
}
