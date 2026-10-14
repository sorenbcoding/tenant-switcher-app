using System.Text.RegularExpressions;

namespace SorenB.TenantSwitcher.Services;

/// <summary>Authority, organization and tenant of an Automation Cloud / Automation Suite URL.</summary>
public sealed record TenantParts(string Authority, string Organization, string Tenant);

/// <summary>
/// Helpers for Orchestrator tenant URLs. Ported from the Studio extension's identity.ts so
/// both tools treat URLs the same way.
/// </summary>
public static class OrchestratorUrl
{
    private static readonly Regex CloudHost = new(@"(^|\.)uipath\.(com|us)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Maps an Orchestrator URL to its Identity Server token endpoint.
    /// Automation Cloud / Suite: https://host/{org}/{tenant}[/orchestrator_] -> https://host/{org}/identity_/connect/token.
    /// Standalone Orchestrator: https://host[/vdir] -> https://host[/vdir]/identity/connect/token.
    /// </summary>
    /// <exception cref="FormatException">The URL is not a full URL.</exception>
    public static string TokenEndpoint(string orchestratorUrl)
    {
        var parts = GetTenantParts(orchestratorUrl);
        if (parts is not null)
        {
            return $"{parts.Authority}/{parts.Organization}/identity_/connect/token";
        }

        var url = Parse(orchestratorUrl) ?? throw new FormatException($"\"{orchestratorUrl}\" is not a full URL.");
        var segments = Segments(url);
        var orchIndex = OrchestratorIndex(segments);
        var kept = orchIndex >= 0 ? segments.Take(orchIndex) : segments;
        var basePath = string.Join('/', kept);
        return $"{Origin(url)}{(basePath.Length > 0 ? "/" + basePath : "")}/identity/connect/token";
    }

    /// <summary>Authority/organization/tenant for <c>uip login</c>, or null for standalone Orchestrator URLs.</summary>
    public static TenantParts? GetTenantParts(string orchestratorUrl)
    {
        var url = Parse(orchestratorUrl);
        if (url is null)
        {
            return null;
        }

        var segments = Segments(url);
        var orchIndex = OrchestratorIndex(segments);
        if (orchIndex >= 2 || (orchIndex < 0 && segments.Length == 2 && IsCloudHost(url.Host)))
        {
            return new TenantParts(Origin(url), segments[0], segments[1]);
        }

        return null;
    }

    /// <summary>Comparable key: lower-case origin + path without trailing slash or /orchestrator_.</summary>
    public static string UrlKey(string orchestratorUrl)
    {
        var url = Parse(orchestratorUrl);
        if (url is null)
        {
            return orchestratorUrl.Trim().ToLowerInvariant().TrimEnd('/');
        }

        var segments = Segments(url);
        var orchIndex = OrchestratorIndex(segments);
        var kept = orchIndex >= 0 ? segments.Take(orchIndex) : segments;
        return $"{Origin(url)}/{string.Join('/', kept)}".ToLowerInvariant().TrimEnd('/');
    }

    /// <summary>Compact label such as "cloud.uipath.com/acme/Dev".</summary>
    public static string ShortUrl(string orchestratorUrl)
    {
        var url = Parse(orchestratorUrl);
        if (url is null)
        {
            return orchestratorUrl;
        }

        var parts = GetTenantParts(orchestratorUrl);
        return parts is not null
            ? $"{url.Authority}/{parts.Organization}/{parts.Tenant}"
            : url.Authority + url.AbsolutePath.TrimEnd('/');
    }

    /// <summary>Validates a URL entered in the form; returns an error message or null.</summary>
    public static string? Check(string value)
    {
        var url = Parse(value);
        if (url is null)
        {
            return "Enter a full URL, e.g. https://cloud.uipath.com/myorg/mytenant/orchestrator_";
        }

        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return "The URL must start with https://";
        }

        if (IsCloudHost(url.Host) && Segments(url).Length < 2)
        {
            return "Automation Cloud URLs must include organization and tenant, e.g. https://cloud.uipath.com/myorg/mytenant/orchestrator_";
        }

        return null;
    }

    private static Uri? Parse(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var url) ? url : null;

    private static string Origin(Uri url) => url.GetLeftPart(UriPartial.Authority);

    private static string[] Segments(Uri url) =>
        url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();

    private static int OrchestratorIndex(string[] segments) =>
        Array.FindIndex(segments, s => s.Equals("orchestrator_", StringComparison.OrdinalIgnoreCase));

    private static bool IsCloudHost(string host) => CloudHost.IsMatch(host);
}
