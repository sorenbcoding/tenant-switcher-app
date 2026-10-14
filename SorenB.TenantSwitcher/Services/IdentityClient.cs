using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace SorenB.TenantSwitcher.Services;

public enum ValidationStatus
{
    /// <summary>Identity Server issued a token.</summary>
    Ok,

    /// <summary>The server said the credentials or the URL are wrong.</summary>
    Rejected,

    /// <summary>Could not tell (network, proxy, unexpected response).</summary>
    Unknown,
}

public sealed record ValidationResult(ValidationStatus Status, string Message = "");

/// <summary>
/// Requests a client_credentials token to prove a preset's credentials work, without touching
/// the Robot. Ported from the Studio extension's validateCredentials.
/// </summary>
public sealed class IdentityClient
{
    private readonly HttpClient _http;

    public IdentityClient(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<ValidationResult> ValidateAsync(string orchestratorUrl, string clientId, string clientSecret, CancellationToken ct = default)
    {
        string endpoint;
        try
        {
            endpoint = OrchestratorUrl.TokenEndpoint(orchestratorUrl);
        }
        catch (FormatException)
        {
            return new(ValidationStatus.Rejected,
                $"Orchestrator URL \"{orchestratorUrl}\" could not be parsed — it is not a full URL. Edit the preset and enter the tenant URL.");
        }

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                }),
            };
            request.Headers.Accept.ParseAdd("application/json");
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var reason = ex is TaskCanceledException ? "the request timed out" : ex.Message.TrimEnd('.');
            return new(ValidationStatus.Unknown, $"Could not reach {endpoint} — {reason}. Check the network or proxy settings.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return new(ValidationStatus.Ok);
            }

            var (error, description) = await ReadErrorAsync(response, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized || error is "invalid_client" or "unauthorized_client")
            {
                var detail = description.Length > 0 ? description : "they do not match a machine in this tenant";
                return new(ValidationStatus.Rejected,
                    $"Identity Server rejected the client ID or secret — {detail}. Edit the preset and enter the client ID and secret shown for the machine in Orchestrator.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new(ValidationStatus.Rejected,
                    $"No token endpoint at {endpoint} — the Orchestrator URL is probably wrong. Edit the preset and check the organization and tenant in the URL.");
            }

            var suffix = error.Length > 0 ? $" ({error}{(description.Length > 0 ? ": " + description : "")})" : "";
            return new(ValidationStatus.Unknown, $"Unexpected response from {endpoint} — HTTP {(int)response.StatusCode}{suffix}.");
        }
    }

    private static async Task<(string Error, string Description)> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;
            return (
                root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : "",
                root.TryGetProperty("error_description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString()! : "");
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException)
        {
            // Non-JSON error page.
            return ("", "");
        }
    }
}
