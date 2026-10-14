using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

// Cases taken from the Studio extension's test/identity.test.ts.
public class OrchestratorUrlTests
{
    [Theory]
    [InlineData("https://cloud.uipath.com/acme/DefaultTenant/orchestrator_", "https://cloud.uipath.com/acme/identity_/connect/token")]
    [InlineData("https://cloud.uipath.com/acme/DefaultTenant/orchestrator_/", "https://cloud.uipath.com/acme/identity_/connect/token")]
    [InlineData("https://cloud.uipath.com/acme/DefaultTenant", "https://cloud.uipath.com/acme/identity_/connect/token")]
    [InlineData("https://staging.uipath.com/acme/dev/orchestrator_", "https://staging.uipath.com/acme/identity_/connect/token")]
    [InlineData("https://suite.contoso.local/acme/prod/orchestrator_", "https://suite.contoso.local/acme/identity_/connect/token")]
    [InlineData("https://orchestrator.contoso.local", "https://orchestrator.contoso.local/identity/connect/token")]
    [InlineData("https://orchestrator.contoso.local/", "https://orchestrator.contoso.local/identity/connect/token")]
    [InlineData("https://contoso.local/uipath", "https://contoso.local/uipath/identity/connect/token")]
    public void TokenEndpoint_maps_tenant_url(string input, string expected)
    {
        Assert.Equal(expected, OrchestratorUrl.TokenEndpoint(input));
    }

    [Fact]
    public void TokenEndpoint_throws_for_non_url()
    {
        Assert.Throws<FormatException>(() => OrchestratorUrl.TokenEndpoint("cloud.uipath.com"));
    }

    [Fact]
    public void GetTenantParts_extracts_authority_org_and_tenant()
    {
        Assert.Equal(
            new TenantParts("https://cloud.uipath.com", "acme", "Dev"),
            OrchestratorUrl.GetTenantParts("https://cloud.uipath.com/acme/Dev/orchestrator_/"));
        Assert.Null(OrchestratorUrl.GetTenantParts("https://orchestrator.contoso.local"));
    }

    [Fact]
    public void UrlKey_treats_equivalent_urls_as_the_same_connection()
    {
        var key = OrchestratorUrl.UrlKey("https://cloud.uipath.com/acme/Dev/orchestrator_");
        Assert.Equal(key, OrchestratorUrl.UrlKey("https://Cloud.UiPath.com/acme/dev/orchestrator_/"));
        Assert.Equal(key, OrchestratorUrl.UrlKey("https://cloud.uipath.com/acme/Dev"));
        Assert.NotEqual(key, OrchestratorUrl.UrlKey("https://cloud.uipath.com/acme/Prod/orchestrator_"));
    }

    [Theory]
    [InlineData("https://cloud.uipath.com/acme/Dev/orchestrator_", "cloud.uipath.com/acme/Dev")]
    [InlineData("https://orchestrator.contoso.local/", "orchestrator.contoso.local")]
    [InlineData("https://contoso.local:8443/uipath/", "contoso.local:8443/uipath")]
    [InlineData("not a url", "not a url")]
    public void ShortUrl_gives_compact_label(string input, string expected)
    {
        Assert.Equal(expected, OrchestratorUrl.ShortUrl(input));
    }

    [Fact]
    public void Check_accepts_tenant_urls_and_rejects_incomplete_ones()
    {
        Assert.Null(OrchestratorUrl.Check("https://cloud.uipath.com/acme/t1/orchestrator_"));
        Assert.Null(OrchestratorUrl.Check("https://orchestrator.contoso.local"));
        Assert.Equal("Enter a full URL, e.g. https://cloud.uipath.com/myorg/mytenant/orchestrator_", OrchestratorUrl.Check("cloud.uipath.com"));
        Assert.Equal("The URL must start with https://", OrchestratorUrl.Check("ftp://cloud.uipath.com/acme/t1"));
        Assert.StartsWith("Automation Cloud URLs must include organization and tenant", OrchestratorUrl.Check("https://cloud.uipath.com/acme"));
    }
}
