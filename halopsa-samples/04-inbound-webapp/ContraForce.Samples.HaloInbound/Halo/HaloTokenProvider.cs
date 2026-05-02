using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ContraForce.Samples.HaloInbound.Webhook;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.HaloInbound.Halo;

/// <summary>
/// Caches the Halo OAuth2 client_credentials access token and refreshes it
/// 60 seconds before expiry.
/// </summary>
public sealed class HaloTokenProvider
{
    private readonly HttpClient _http;
    private readonly HaloOptions _options;
    private readonly ILogger<HaloTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public HaloTokenProvider(
        HttpClient http,
        IOptions<HaloOptions> options,
        ILogger<HaloTokenProvider> logger
    )
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromSeconds(60))
            return _token;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromSeconds(60))
                return _token;

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = _options.Scope,
            };
            if (!string.IsNullOrWhiteSpace(_options.Tenant))
                form["tenant"] = _options.Tenant!;

            var url = _options.AuthUrl.TrimEnd('/') + "/token";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(form),
            };

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Halo token request failed: {StatusCode} {Body}",
                    (int)response.StatusCode,
                    body
                );
                response.EnsureSuccessStatusCode();
            }

            var payload =
                await response.Content.ReadFromJsonAsync<HaloTokenResponse>(
                    cancellationToken: cancellationToken
                ) ?? throw new InvalidOperationException("Halo returned empty token response");

            _token = payload.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record HaloTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}
