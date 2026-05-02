using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContraForce.Samples.HaloInbound.Webhook;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.HaloInbound.Halo;

/// <summary>
/// Read-only Halo client for the fields this sample cares about: ticket
/// status, custom fields, and the action stream.
/// </summary>
public sealed class HaloClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly HaloTokenProvider _tokens;
    private readonly HaloOptions _options;
    private readonly ILogger<HaloClient> _logger;

    public HaloClient(
        HttpClient http,
        HaloTokenProvider tokens,
        IOptions<HaloOptions> options,
        ILogger<HaloClient> logger
    )
    {
        _options = options.Value;
        _tokens = tokens;
        _http = http;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<HaloTicket?> GetTicketAsync(int ticketId, CancellationToken cancellationToken)
    {
        using var request = await BuildRequestAsync(
            HttpMethod.Get,
            $"Tickets/{ticketId}",
            cancellationToken
        );
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Halo GET /Tickets/{TicketId} failed: {StatusCode}",
                ticketId,
                (int)response.StatusCode
            );
            return null;
        }
        return await response.Content.ReadFromJsonAsync<HaloTicket>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<HaloActionRecord>> GetTicketActionsAsync(
        int ticketId,
        CancellationToken cancellationToken
    )
    {
        using var request = await BuildRequestAsync(
            HttpMethod.Get,
            $"Actions?ticket_id={ticketId}",
            cancellationToken
        );
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        JsonElement list =
            root.ValueKind == JsonValueKind.Array
                ? root
                : (root.TryGetProperty("actions", out var actions) ? actions : default);

        if (list.ValueKind != JsonValueKind.Array)
            return Array.Empty<HaloActionRecord>();

        var result = new List<HaloActionRecord>(list.GetArrayLength());
        foreach (var element in list.EnumerateArray())
        {
            var record = element.Deserialize<HaloActionRecord>(JsonOptions);
            if (record is not null)
                result.Add(record);
        }
        return result;
    }

    /// <summary>
    /// Reads the external reference value off a ticket — either from
    /// <c>thirdpartynumber</c> or from a configured custom field.
    /// </summary>
    public string? ExtractExternalReference(HaloTicket ticket)
    {
        if (_options.ExternalRefFieldId is int customFieldId)
        {
            var match = ticket.CustomFields?.FirstOrDefault(f => f.Id == customFieldId);
            return match?.Value;
        }
        return ticket.ThirdPartyNumber;
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken
    )
    {
        var token = await _tokens.GetTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

public sealed record HaloTicket(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("thirdpartynumber")] string? ThirdPartyNumber,
    [property: JsonPropertyName("status_id")] int? StatusId,
    [property: JsonPropertyName("status_name")] string? StatusName,
    [property: JsonPropertyName("customfields")] HaloCustomFieldRef[]? CustomFields
);

public sealed record HaloCustomFieldRef(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("value")] string? Value
);

public sealed record HaloActionRecord(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("ticket_id")] int TicketId,
    [property: JsonPropertyName("outcome")] string? Outcome,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("note_html")] string? NoteHtml,
    [property: JsonPropertyName("hiddenfromuser")] bool HiddenFromUser,
    [property: JsonPropertyName("agent_id")] int? AgentId,
    [property: JsonPropertyName("who")] string? Who,
    [property: JsonPropertyName("datetime")] DateTimeOffset? DateTime
);
