using SorenB.TenantSwitcher.Models;
using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

public class PresetValidationTests
{
    private static readonly Preset Existing = new()
    {
        Id = Guid.NewGuid(),
        PresetName = "Acme production",
        OrchestratorUrl = "https://cloud.uipath.com/acme/Production/orchestrator_",
        ClientId = "abc",
    };

    private static PresetFormValues Valid(Guid? id = null, string name = "Acme dev", string secret = "s") =>
        new(id, name, "https://cloud.uipath.com/acme/Dev/orchestrator_", "client", secret);

    [Fact]
    public void Valid_form_has_no_errors()
    {
        Assert.Empty(PresetValidation.Validate(Valid(), new[] { Existing }, hasStoredSecret: false));
    }

    [Fact]
    public void Empty_form_reports_every_field()
    {
        var errors = PresetValidation.Validate(new PresetFormValues(null, " ", "", " ", ""), Array.Empty<Preset>(), false);
        Assert.Equal("Enter a preset name.", errors[FormField.PresetName]);
        Assert.Equal("Enter a full URL, e.g. https://cloud.uipath.com/myorg/mytenant/orchestrator_", errors[FormField.OrchestratorUrl]);
        Assert.Equal("Enter the machine client ID.", errors[FormField.ClientId]);
        Assert.Equal("Enter the client secret.", errors[FormField.ClientSecret]);
    }

    [Fact]
    public void Duplicate_name_is_rejected_case_insensitively()
    {
        var errors = PresetValidation.Validate(Valid(name: "ACME PRODUCTION "), new[] { Existing }, false);
        Assert.Equal("A preset named \"Acme production\" already exists. Choose another name.", errors[FormField.PresetName]);
    }

    [Fact]
    public void Editing_a_preset_may_keep_its_own_name_and_stored_secret()
    {
        var errors = PresetValidation.Validate(Valid(Existing.Id, "Acme production", secret: ""), new[] { Existing }, hasStoredSecret: true);
        Assert.Empty(errors);
    }
}
