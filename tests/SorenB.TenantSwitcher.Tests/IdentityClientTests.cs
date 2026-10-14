using System.Net;
using System.Net.Http;
using SorenB.TenantSwitcher.Services;
using Xunit;

namespace SorenB.TenantSwitcher.Tests;

public class IdentityClientTests
{
    private const string Url = "https://cloud.uipath.com/acme/t1/orchestrator_";

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public HttpRequestMessage? Seen { get; private set; }

        public string? SeenBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen = request;
            SeenBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return _respond(request);
        }
    }

    private static FakeHandler Respond(HttpStatusCode status, string? body = null) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body ?? "") });

    [Fact]
    public async Task Posts_client_credentials_to_the_identity_endpoint()
    {
        var handler = Respond(HttpStatusCode.OK, "{\"access_token\":\"x\"}");
        var result = await new IdentityClient(handler).ValidateAsync(Url, "id", "s&cret");

        Assert.Equal(ValidationStatus.Ok, result.Status);
        Assert.Equal("https://cloud.uipath.com/acme/identity_/connect/token", handler.Seen!.RequestUri!.ToString());
        Assert.Contains("client_secret=s%26cret", handler.SeenBody);
    }

    [Fact]
    public async Task Reports_rejected_credentials()
    {
        var r1 = await new IdentityClient(Respond(HttpStatusCode.BadRequest, "{\"error\":\"invalid_client\"}")).ValidateAsync(Url, "id", "bad");
        var r2 = await new IdentityClient(Respond(HttpStatusCode.Unauthorized)).ValidateAsync(Url, "id", "bad");
        var r3 = await new IdentityClient(Respond(HttpStatusCode.NotFound)).ValidateAsync(Url, "id", "bad");

        Assert.Equal(ValidationStatus.Rejected, r1.Status);
        Assert.StartsWith("Identity Server rejected the client ID or secret — they do not match a machine in this tenant.", r2.Message);
        Assert.StartsWith("No token endpoint at https://cloud.uipath.com/acme/identity_/connect/token", r3.Message);
    }

    [Fact]
    public async Task Reports_unknown_for_network_errors_and_unexpected_responses()
    {
        var boom = new FakeHandler(_ => throw new HttpRequestException("No connection could be made."));
        var r1 = await new IdentityClient(boom).ValidateAsync(Url, "id", "s");
        var r2 = await new IdentityClient(Respond(HttpStatusCode.BadRequest, "{\"error\":\"invalid_scope\"}")).ValidateAsync(Url, "id", "s");
        var r3 = await new IdentityClient(Respond(HttpStatusCode.ServiceUnavailable, "<html/>")).ValidateAsync(Url, "id", "s");

        Assert.Equal(ValidationStatus.Unknown, r1.Status);
        Assert.Contains("No connection could be made. Check the network", r1.Message);
        Assert.Equal("Unexpected response from https://cloud.uipath.com/acme/identity_/connect/token — HTTP 400 (invalid_scope).", r2.Message);
        Assert.Equal(ValidationStatus.Unknown, r3.Status);
    }

    [Fact]
    public async Task Rejects_unparseable_url()
    {
        var result = await new IdentityClient(Respond(HttpStatusCode.OK)).ValidateAsync("nope", "id", "s");
        Assert.Equal(ValidationStatus.Rejected, result.Status);
    }
}
