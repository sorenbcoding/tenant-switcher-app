using System.Globalization;
using SorenB.TenantSwitcher.Models;

namespace SorenB.TenantSwitcher.Services;

public enum FormField
{
    PresetName,
    OrchestratorUrl,
    ClientId,
    ClientSecret,
}

/// <summary>Values submitted by the add/edit form. An empty secret while editing means "keep the stored secret".</summary>
public sealed record PresetFormValues(Guid? Id, string PresetName, string OrchestratorUrl, string ClientId, string ClientSecret);

/// <summary>Field-level validation of the preset form, ported from the Studio extension's presetForm.ts.</summary>
public static class PresetValidation
{
    /// <summary>Returns one message per invalid field; an empty result means the form can be saved.</summary>
    public static IReadOnlyDictionary<FormField, string> Validate(PresetFormValues values, IEnumerable<Preset> existing, bool hasStoredSecret)
    {
        var errors = new Dictionary<FormField, string>();

        var name = values.PresetName.Trim();
        if (name.Length == 0)
        {
            errors[FormField.PresetName] = "Enter a preset name.";
        }
        else
        {
            var clash = existing.FirstOrDefault(p => p.Id != values.Id && SameName(p.PresetName, name));
            if (clash is not null)
            {
                errors[FormField.PresetName] = $"A preset named \"{clash.PresetName}\" already exists. Choose another name.";
            }
        }

        var urlError = OrchestratorUrl.Check(values.OrchestratorUrl);
        if (urlError is not null)
        {
            errors[FormField.OrchestratorUrl] = urlError;
        }

        if (values.ClientId.Trim().Length == 0)
        {
            errors[FormField.ClientId] = "Enter the machine client ID.";
        }

        if (values.ClientSecret.Length == 0 && !hasStoredSecret)
        {
            errors[FormField.ClientSecret] = "Enter the client secret.";
        }

        return errors;
    }

    /// <summary>Case-insensitive, accent-sensitive name comparison (same as the extension's localeCompare with sensitivity 'accent').</summary>
    public static bool SameName(string a, string b) =>
        string.Compare(a.Trim(), b.Trim(), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase) == 0;
}
