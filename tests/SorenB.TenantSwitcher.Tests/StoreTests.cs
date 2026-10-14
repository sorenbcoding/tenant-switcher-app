using System.Text;
using SorenB.TenantSwitcher.Models;
using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sb-ts-tests-" + Guid.NewGuid().ToString("N"));

    public StoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Reads_file_with_bom_and_upper_case_id()
    {
        var path = Path.Combine(_dir, "presets.json");
        var json = "[{\"Id\":\"0F8FAD5B-D9CB-469F-A165-70867728950E\",\"PresetName\":\"Dev\",\"OrchestratorUrl\":\"https://cloud.uipath.com/a/Dev\",\"ClientId\":\"c\"}]";
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var presets = await new PresetStore(path).LoadAsync();

        var preset = Assert.Single(presets);
        Assert.Equal(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), preset.Id);
        Assert.Equal("Dev", preset.PresetName);
    }

    [Fact]
    public async Task Writes_bare_array_with_pascal_case_keys_and_lower_case_ids()
    {
        var path = Path.Combine(_dir, "presets.json");
        var store = new PresetStore(path);
        var stored = await store.UpsertAsync(new Preset { PresetName = " Dev ", OrchestratorUrl = "https://x/a/b ", ClientId = " c " });

        var text = await File.ReadAllTextAsync(path);
        Assert.StartsWith("[", text.TrimStart());
        Assert.Contains("\"PresetName\": \"Dev\"", text);
        Assert.Contains("\"Id\": \"" + stored.Id.ToString("D").ToLowerInvariant() + "\"", text);
    }

    [Fact]
    public async Task Upsert_matches_by_id_then_by_name_and_remove_deletes()
    {
        var store = new PresetStore(Path.Combine(_dir, "presets.json"));
        var a = await store.UpsertAsync(new Preset { PresetName = "A", OrchestratorUrl = "https://x/a/1", ClientId = "1" });

        // Renaming by id keeps one entry.
        await store.UpsertAsync(new Preset { Id = a.Id, PresetName = "A renamed", OrchestratorUrl = "https://x/a/1", ClientId = "1" });
        // Same name with a new id replaces it and keeps the existing id.
        var replaced = await store.UpsertAsync(new Preset { PresetName = "a RENAMED", OrchestratorUrl = "https://x/a/2", ClientId = "2" });

        var presets = await store.LoadAsync();
        Assert.Single(presets);
        Assert.Equal(a.Id, replaced.Id);
        Assert.Equal("https://x/a/2", presets[0].OrchestratorUrl);

        await store.RemoveAsync(a.Id);
        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public async Task Invalid_json_raises_InvalidDataException()
    {
        var path = Path.Combine(_dir, "presets.json");
        await File.WriteAllTextAsync(path, "{ not json");
        await Assert.ThrowsAsync<InvalidDataException>(() => new PresetStore(path).LoadAsync());
    }

    [Fact]
    public void App_state_round_trips_and_survives_a_corrupt_file()
    {
        var store = new AppStateStore(_dir);
        var id = Guid.NewGuid();
        var state = store.LoadState();
        state.ActivePresetId = id;
        state.Presets[id] = new PresetOptions { SyncUipCli = true };
        state.LastResult = new SwitchResult(id, "Dev", true, false, ResultSeverity.Info, "Connected to 'Dev'.",
            new[] { new SwitchStep("Connect Robot", StepOutcome.Ok) }, DateTimeOffset.Now);
        store.SaveState(state);

        var loaded = store.LoadState();
        Assert.Equal(id, loaded.ActivePresetId);
        Assert.True(loaded.Presets[id].SyncUipCli);
        Assert.Equal(StepOutcome.Ok, loaded.LastResult!.Steps[0].Outcome);

        File.WriteAllText(Path.Combine(_dir, "settings.json"), "garbage");
        Assert.True(store.LoadSettings().ConfirmBeforeSwitch);
    }
}
