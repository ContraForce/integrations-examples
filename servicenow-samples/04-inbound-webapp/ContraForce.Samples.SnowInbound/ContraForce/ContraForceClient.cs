using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContraForce.Samples.SnowInbound.CallbackModels;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.SnowInbound.ContraForce;

/// <summary>
/// Minimal client for the ContraForce public REST API.
/// Authenticates with a service-account credential (HTTP Basic).
/// </summary>
public sealed class ContraForceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ContraForceOptions _options;
    private readonly ILogger<ContraForceClient> _logger;

    public ContraForceClient(
        HttpClient http,
        IOptions<ContraForceOptions> options,
        ILogger<ContraForceClient> logger
    )
    {
        _options = options.Value;
        _http = http;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);

        var credentialBytes = Encoding.UTF8.GetBytes(
            $"{_options.ServiceAccountClientId}:{_options.ServiceAccountClientSecret}"
        );
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(credentialBytes)
        );
    }

    public async Task AddIncidentCommentAsync(
        string incidentId,
        string source,
        string content,
        CancellationToken cancellationToken
    )
    {
        var path =
            $"workspaces/{_options.WorkspaceId}/incidents/{Uri.EscapeDataString(incidentId)}/comments";
        var body = new SubmitIncidentCommentRequest(incidentId, null, content, null, source);
        using var response = await _http.PostAsJsonAsync(
            path,
            body,
            JsonOptions,
            cancellationToken
        );
        await EnsureSuccessAsync(response, path, cancellationToken);
    }

    public async Task CloseIncidentAsync(
        string incidentId,
        string source,
        string? closingComment,
        CancellationToken cancellationToken
    )
    {
        var path =
            $"workspaces/{_options.WorkspaceId}/incidents/{Uri.EscapeDataString(incidentId)}/status";
        var body = new UpdateIncidentStatusRequest(
            IncidentId: incidentId,
            Source: source,
            Status: "Closed",
            Comment: closingComment,
            // Classification + ClassificationReason are required when closing
            // Sentinel incidents — tweak if your ServiceNow workflow drives
            // different classifications. "Undetermined" / "InaccurateData" are
            // the safest neutral defaults for a ServiceNow-driven close.
            Classification: "Undetermined",
            ClassificationReason: "InaccurateData"
        );
        using var response = await _http.PutAsJsonAsync(path, body, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken
    )
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "ContraForce call to {Path} failed: {StatusCode} {Reason} — body: {Body}",
            path,
            (int)response.StatusCode,
            response.ReasonPhrase,
            body
        );
        response.EnsureSuccessStatusCode();
    }
}

public sealed record SubmitIncidentCommentRequest(
    string IncidentId,
    string? CommentId,
    string Content,
    string? ExtensionId,
    string Source
);

public sealed record UpdateIncidentStatusRequest(
    string IncidentId,
    string Source,
    string Status,
    string? Comment,
    string? Classification,
    string? ClassificationReason
);
