using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContraForce.Samples.SnowInbound.CallbackModels;
using Microsoft.Extensions.Options;

namespace ContraForce.Samples.SnowInbound.ServiceNow;

/// <summary>
/// Read-only view of ServiceNow for the fields this sample cares about: the
/// incident record and its journal stream (comments / work notes).
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

    public async Task<SnowIncident?> GetIncidentAsync(
        string sysId,
        CancellationToken cancellationToken
    )
    {
        var path =
            $"incident/{Uri.EscapeDataString(sysId)}?sysparm_exclude_reference_link=true"
            + "&sysparm_fields=sys_id,number,state,correlation_id,short_description,close_code,close_notes";

        using var response = await _http.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ServiceNow GET /incident/{SysId} failed: {StatusCode}",
                sysId,
                (int)response.StatusCode
            );
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<SnowResult<SnowIncident>>(
            JsonOptions,
            cancellationToken
        );
        return result?.Result;
    }

    /// <summary>
    /// Read the journal entries (comments / work notes) for an incident,
    /// oldest first. ServiceNow stores both in the <c>sys_journal_field</c>
    /// table, discriminated by <c>element</c>.
    /// </summary>
    public async Task<IReadOnlyList<SnowJournalEntry>> GetJournalEntriesAsync(
        string sysId,
        CancellationToken cancellationToken
    )
    {
        var query = $"element_id={sysId}^ORDERBYsys_created_on";
        var path =
            "sys_journal_field?sysparm_fields=sys_id,element,value,sys_created_on,sys_created_by"
            + $"&sysparm_query={Uri.EscapeDataString(query)}";

        using var response = await _http.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SnowResult<List<SnowJournalEntry>>>(
            JsonOptions,
            cancellationToken
        );
        return result?.Result ?? [];
    }
}

public sealed record SnowResult<T>([property: JsonPropertyName("result")] T Result);

public sealed record SnowIncident(
    [property: JsonPropertyName("sys_id")] string SysId,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("correlation_id")] string? CorrelationId,
    [property: JsonPropertyName("short_description")] string? ShortDescription,
    [property: JsonPropertyName("close_code")] string? CloseCode,
    [property: JsonPropertyName("close_notes")] string? CloseNotes
);

public sealed record SnowJournalEntry(
    [property: JsonPropertyName("sys_id")] string SysId,
    [property: JsonPropertyName("element")] string? Element,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("sys_created_on")] string? CreatedOn,
    [property: JsonPropertyName("sys_created_by")] string? CreatedBy
);
