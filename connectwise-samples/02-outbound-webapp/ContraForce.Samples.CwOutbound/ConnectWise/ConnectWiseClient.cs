using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.CwOutbound.ConnectWise;

/// <summary>
/// Thin ConnectWise Manage REST client for the subset used by this sample.
/// </summary>
public sealed class ConnectWiseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ConnectWiseOptions _options;
    private readonly ILogger<ConnectWiseClient> _logger;

    public ConnectWiseClient(
        HttpClient http,
        IOptions<ConnectWiseOptions> options,
        ILogger<ConnectWiseClient> logger
    )
    {
        _options = options.Value;
        _http = http;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Accept.ParseAdd(_options.ApiVersionHeader);
        _http.DefaultRequestHeaders.Add("clientId", _options.ClientId);

        var credentialBytes = Encoding.UTF8.GetBytes(
            $"{_options.CompanyId}+{_options.PublicKey}:{_options.PrivateKey}"
        );
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(credentialBytes)
        );
    }

    /// <summary>
    /// Find an existing ticket by its <c>externalReference</c> value.
    /// Returns the ticket id or <c>null</c> if none exists.
    /// </summary>
    public async Task<int?> FindTicketByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken
    )
    {
        var escaped = externalReference.Replace("\"", "\\\"");
        var query =
            $"?pageSize=1&conditions={Uri.EscapeDataString($"externalReference = \"{escaped}\"")}";
        using var response = await _http.GetAsync("service/tickets" + query, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tickets = await response.Content.ReadFromJsonAsync<List<CwTicket>>(
            JsonOptions,
            cancellationToken
        );
        return tickets is { Count: > 0 } ? tickets[0].Id : null;
    }

    public async Task<int> CreateTicketAsync(
        CwTicketCreate ticket,
        CancellationToken cancellationToken
    )
    {
        using var response = await _http.PostAsJsonAsync(
            "service/tickets",
            ticket,
            JsonOptions,
            cancellationToken
        );
        await EnsureSuccessAsync(response, cancellationToken);

        var created =
            await response.Content.ReadFromJsonAsync<CwTicket>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException(
                "ConnectWise returned empty body for ticket create"
            );
        _logger.LogInformation(
            "Created ConnectWise ticket {TicketId} with externalReference {ExternalReference}",
            created.Id,
            ticket.ExternalReference
        );
        return created.Id;
    }

    public async Task AddNoteAsync(int ticketId, string text, CancellationToken cancellationToken)
    {
        var note = new
        {
            text,
            detailDescriptionFlag = true,
            customerUpdatedFlag = false,
            internalAnalysisFlag = true,
        };
        using var response = await _http.PostAsJsonAsync(
            $"service/tickets/{ticketId}/notes",
            note,
            JsonOptions,
            cancellationToken
        );
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "ConnectWise call failed: {StatusCode} {ReasonPhrase} — body: {Body}",
            (int)response.StatusCode,
            response.ReasonPhrase,
            body
        );
        response.EnsureSuccessStatusCode();
    }
}
