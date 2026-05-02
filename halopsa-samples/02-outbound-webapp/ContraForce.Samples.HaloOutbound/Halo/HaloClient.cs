using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.HaloOutbound.Halo;

/// <summary>
/// Thin HaloPSA REST client for the subset used by this sample.
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

    /// <summary>
    /// Find an existing ticket whose external-reference value matches.
    /// Uses <c>thirdpartynumber</c> by default, or the configured custom field id.
    /// Returns the ticket id, or <c>null</c>.
    /// </summary>
    public async Task<int?> FindTicketByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken
    )
    {
        var query = _options.ExternalRefFieldId is int customFieldId
            ? $"?count=1&searchcustomfield_{customFieldId}={Uri.EscapeDataString(externalReference)}"
            : $"?count=1&thirdpartynumber={Uri.EscapeDataString(externalReference)}";

        using var request = await BuildRequestAsync(
            HttpMethod.Get,
            "Tickets" + query,
            cancellationToken
        );
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "GET /api/Tickets", cancellationToken);

        // Halo returns either an array directly or `{ "tickets": [...] }`
        // depending on query params; handle both.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        JsonElement list =
            root.ValueKind == JsonValueKind.Array
                ? root
                : (root.TryGetProperty("tickets", out var tickets) ? tickets : default);

        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            return null;

        var first = list[0];
        return first.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var id) ? id : null;
    }

    public async Task<int> CreateOrUpdateTicketAsync(
        HaloTicketUpsert ticket,
        CancellationToken cancellationToken
    )
    {
        // Halo's POST /api/Tickets expects an *array* body — even for a single
        // ticket. The same endpoint serves create and update; include an id
        // to update.
        using var request = await BuildRequestAsync(HttpMethod.Post, "Tickets", cancellationToken);
        request.Content = JsonContent.Create(new[] { ticket }, options: JsonOptions);

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "POST /api/Tickets", cancellationToken);

        var created =
            await response.Content.ReadFromJsonAsync<HaloTicket[]>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Halo returned empty body for ticket upsert");

        if (created.Length == 0)
            throw new InvalidOperationException("Halo returned empty array for ticket upsert");

        var id = created[0].Id;
        _logger.LogInformation(
            "Halo ticket {Action} {TicketId} (extRef {ExternalReference})",
            ticket.Id is null ? "created" : "updated",
            id,
            ticket.ThirdPartyNumber
        );
        return id;
    }

    public async Task AddNoteAsync(
        int ticketId,
        string noteHtml,
        bool hiddenFromUser,
        CancellationToken cancellationToken
    )
    {
        var action = new HaloAction(
            TicketId: ticketId,
            Outcome: "Private Note",
            NoteHtml: noteHtml,
            HiddenFromUser: hiddenFromUser
        );

        using var request = await BuildRequestAsync(HttpMethod.Post, "Actions", cancellationToken);
        request.Content = JsonContent.Create(new[] { action }, options: JsonOptions);

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "POST /api/Actions", cancellationToken);
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

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken
    )
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Halo {Operation} failed: {StatusCode} {ReasonPhrase} — body: {Body}",
            operation,
            (int)response.StatusCode,
            response.ReasonPhrase,
            body
        );
        response.EnsureSuccessStatusCode();
    }
}
