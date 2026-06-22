using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.SnowOutbound.ServiceNow;

/// <summary>
/// Thin ServiceNow Table API client for the subset used by this sample.
/// Authenticates with HTTP Basic (a dedicated integration user).
/// </summary>
public sealed class ServiceNowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<ServiceNowClient> _logger;

    public ServiceNowClient(
        HttpClient http,
        IOptions<ServiceNowOptions> options,
        ILogger<ServiceNowClient> logger
    )
    {
        var opts = options.Value;
        _http = http;
        _logger = logger;

        _http.BaseAddress = new Uri(opts.InstanceUrl.TrimEnd('/') + "/api/now/table/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        var credentialBytes = Encoding.UTF8.GetBytes($"{opts.Username}:{opts.Password}");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(credentialBytes)
        );
    }

    /// <summary>
    /// Find an existing incident by its <c>correlation_id</c> value. Returns
    /// the <c>sys_id</c> or <c>null</c> if none exists.
    /// </summary>
    public async Task<string?> FindIncidentByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken
    )
    {
        var query =
            "incident?sysparm_limit=1&sysparm_exclude_reference_link=true"
            + "&sysparm_fields=sys_id,number,state"
            + $"&sysparm_query={Uri.EscapeDataString($"correlation_id={correlationId}")}";

        using var response = await _http.GetAsync(query, cancellationToken);
        await EnsureSuccessAsync(response, "GET /incident", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<SnowResult<List<SnowIncidentRef>>>(
            JsonOptions,
            cancellationToken
        );
        return result is { Result.Count: > 0 } ? result.Result[0].SysId : null;
    }

    public async Task<SnowIncidentRef> CreateIncidentAsync(
        SnowIncidentCreate incident,
        CancellationToken cancellationToken
    )
    {
        const string path =
            "incident?sysparm_exclude_reference_link=true&sysparm_fields=sys_id,number,state";
        using var response = await _http.PostAsJsonAsync(
            path,
            incident,
            JsonOptions,
            cancellationToken
        );
        await EnsureSuccessAsync(response, "POST /incident", cancellationToken);

        var result =
            await response.Content.ReadFromJsonAsync<SnowResult<SnowIncidentRef>>(
                JsonOptions,
                cancellationToken
            ) ?? throw new InvalidOperationException("ServiceNow returned empty body for create");

        _logger.LogInformation(
            "Created ServiceNow incident {Number} ({SysId}) with correlation_id {CorrelationId}",
            result.Result.Number,
            result.Result.SysId,
            incident.CorrelationId
        );
        return result.Result;
    }

    /// <summary>
    /// Append an internal work note to an incident. The Table API journals the
    /// value, so PATCHing <c>work_notes</c> adds an entry rather than replacing.
    /// </summary>
    public async Task AddWorkNoteAsync(
        string sysId,
        string note,
        CancellationToken cancellationToken
    )
    {
        var path = $"incident/{Uri.EscapeDataString(sysId)}?sysparm_fields=sys_id";
        var body = new { work_notes = note };
        using var response = await _http.PatchAsJsonAsync(
            path,
            body,
            JsonOptions,
            cancellationToken
        );
        await EnsureSuccessAsync(response, "PATCH /incident", cancellationToken);
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
            "ServiceNow {Operation} failed: {StatusCode} {ReasonPhrase} — body: {Body}",
            operation,
            (int)response.StatusCode,
            response.ReasonPhrase,
            body
        );
        response.EnsureSuccessStatusCode();
    }
}
