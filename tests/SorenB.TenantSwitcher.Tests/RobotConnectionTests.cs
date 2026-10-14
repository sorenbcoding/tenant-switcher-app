using System.Text;
using SorenB.TenantSwitcher.Models;
using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

public sealed class RobotConnectionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sb-ts-tests-" + Guid.NewGuid().ToString("N"));

    public RobotConnectionTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "service"));
        Directory.CreateDirectory(Path.Combine(_dir, "user"));
    }

    private string ServicePath => Path.Combine(_dir, "service", "UiPath.settings");

    private string UserPath => Path.Combine(_dir, "user", "UiPath.settings");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static Preset P(string name, string url) => new() { PresetName = name, OrchestratorUrl = url, ClientId = "c" };

    private static void WriteSettings(string path, string json, DateTime? lastWrite = null)
    {
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        if (lastWrite is { } t)
        {
            File.SetLastWriteTimeUtc(path, t);
        }
    }

    [Fact]
    public void ReadServerUrlFrom_handles_bom_empty_missing_and_invalid()
    {
        WriteSettings(UserPath, "{ \"TracingLevel\": \"Information\", \"UiPathServerUrl\": \"https://uipath.sdbf.dk/\" }");
        Assert.Equal("https://uipath.sdbf.dk/", RobotConnection.ReadServerUrlFrom(UserPath));

        WriteSettings(UserPath, "{ \"UiPathServerUrl\": \"\" }");
        Assert.Equal(string.Empty, RobotConnection.ReadServerUrlFrom(UserPath));

        WriteSettings(UserPath, "{ \"TracingLevel\": \"Information\" }");
        Assert.Null(RobotConnection.ReadServerUrlFrom(UserPath));

        WriteSettings(UserPath, "{ not json");
        Assert.Null(RobotConnection.ReadServerUrlFrom(UserPath));

        Assert.Null(RobotConnection.ReadServerUrlFrom(Path.Combine(_dir, "missing", "UiPath.settings")));
    }

    [Fact]
    public void ReadServerUrl_prefers_service_file_in_service_mode_and_newest_file_otherwise()
    {
        WriteSettings(ServicePath, "{ \"UiPathServerUrl\": \"https://service.local/\" }", DateTime.UtcNow.AddHours(-1));
        WriteSettings(UserPath, "{ \"UiPathServerUrl\": \"https://user.local/\" }", DateTime.UtcNow);
        var paths = new[] { ServicePath, UserPath };

        Assert.Equal("https://service.local/", new RobotConnection(paths, () => true).ReadServerUrl());
        Assert.Equal("https://user.local/", new RobotConnection(paths, () => false).ReadServerUrl());
        Assert.Null(new RobotConnection(new[] { Path.Combine(_dir, "none", "UiPath.settings") }, () => false).ReadServerUrl());
    }

    [Fact]
    public void ResolveActive_matches_by_url_key()
    {
        var dev = P("Dev", "https://cloud.uipath.com/acme/Dev/orchestrator_");
        var prod = P("Prod", "https://cloud.uipath.com/acme/Prod/orchestrator_");
        var presets = new[] { dev, prod };

        Assert.Same(prod, RobotConnection.ResolveActive(presets, "https://cloud.uipath.com/acme/Prod/orchestrator_/", null));
        Assert.Same(dev, RobotConnection.ResolveActive(presets, "https://Cloud.UiPath.com/acme/dev", null));
        Assert.Null(RobotConnection.ResolveActive(presets, "https://cloud.uipath.com/acme/Test/orchestrator_", dev.Id));
    }

    [Fact]
    public void ResolveActive_prefers_last_switched_among_same_url_and_handles_disconnected_and_unknown()
    {
        var a = P("A", "https://uipath.sdbf.dk");
        var b = P("B", "https://uipath.sdbf.dk/");
        var presets = new[] { a, b };

        Assert.Same(b, RobotConnection.ResolveActive(presets, "https://uipath.sdbf.dk/", b.Id));
        Assert.Same(a, RobotConnection.ResolveActive(presets, "https://uipath.sdbf.dk/", null));

        // Disconnected: nothing is active, whatever was switched last.
        Assert.Null(RobotConnection.ResolveActive(presets, "", b.Id));

        // Unknown (settings unreadable): fall back to the last switched preset.
        Assert.Same(b, RobotConnection.ResolveActive(presets, null, b.Id));
    }
}
