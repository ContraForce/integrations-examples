using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContraForce.Samples.CwInbound.CallbackModels;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.CwInbound.ConnectWise;

/// <summary>
/// Read-only view of ConnectWise Manage for the fields this sample cares about:
/// the service ticket and its notes, ordered by id descending.
/// </summary>
public sealed class ConnectWiseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<ConnectWiseClient> _logger;

    public ConnectWiseClient(HttpClient http, IOptions<ConnectWiseOptions> options, ILogger<ConnectWiseClient> logger)
    {
        var opts = options.Value;
        _http = http;
        _logger = logger;

        _http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Accept.ParseAdd(opts.ApiVersionHeader);
        _http.DefaultRequestHeaders.Add("clientId", opts.ClientId);

        var credentialBytes = Encoding.UTF8.GetBytes($"{opts.CompanyId}+{opts.PublicKey}:{opts.PrivateKey}");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
    }

    public async Task<CwTicket?> GetTicketAsync(int ticketId, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"service/tickets/{ticketId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch ticket {TicketId}: {StatusCode}", ticketId, (int)response.StatusCode);
            return null;
        }
        return await response.Content.ReadFromJsonAsync<CwTicket>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<CwTicketNote>> GetTicketNotesAsync(int ticketId, int pageSize, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            $"service/tickets/{ticketId}/notes?orderBy=id+desc&pageSize={pageSize}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var notes = await response.Content.ReadFromJsonAsync<List<CwTicketNote>>(JsonOptions, cancellationToken);
        return notes ?? [];
    }
}

public sealed record CwTicket(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] int Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("summary")] string? Summary,
    [property: System.Text.Json.Serialization.JsonPropertyName("externalReference")] string? ExternalReference,
    [property: System.Text.Json.Serialization.JsonPropertyName("closedFlag")] bool ClosedFlag,
    [property: System.Text.Json.Serialization.JsonPropertyName("status")] CwStatus? Status);

public sealed record CwStatus(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] int Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("name")] string? Name);

public sealed record CwTicketNote(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] int Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("text")] string? Text,
    [property: System.Text.Json.Serialization.JsonPropertyName("detailDescriptionFlag")] bool DetailDescriptionFlag,
    [property: System.Text.Json.Serialization.JsonPropertyName("internalAnalysisFlag")] bool InternalAnalysisFlag,
    [property: System.Text.Json.Serialization.JsonPropertyName("_info")] CwNoteInfo? Info);

public sealed record CwNoteInfo(
    [property: System.Text.Json.Serialization.JsonPropertyName("lastUpdated")] DateTimeOffset? LastUpdated,
    [property: System.Text.Json.Serialization.JsonPropertyName("updatedBy")] string? UpdatedBy);
