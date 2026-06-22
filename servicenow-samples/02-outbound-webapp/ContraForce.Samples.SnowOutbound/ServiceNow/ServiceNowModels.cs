using System.Text.Json.Serialization;

namespace ContraForce.Samples.SnowOutbound.ServiceNow;

/// <summary>
/// The Table API wraps a single record in <c>{ "result": { … } }</c> and a
/// list in <c>{ "result": [ … ] }</c>.
/// </summary>
public sealed record SnowResult<T>([property: JsonPropertyName("result")] T Result);

/// <summary>
/// The slice of an <c>incident</c> record this sample reads back.
/// </summary>
public sealed record SnowIncidentRef(
    [property: JsonPropertyName("sys_id")] string SysId,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("state")] string? State
);

/// <summary>
/// Body for <c>POST /api/now/table/incident</c>. <c>urgency</c>, <c>impact</c>
/// and <c>state</c> are sent as strings — the Table API accepts both, and
/// strings avoid culture-specific integer formatting. Null reference fields
/// are omitted so we never blank out instance defaults.
/// </summary>
public sealed record SnowIncidentCreate(
    [property: JsonPropertyName("short_description")] string ShortDescription,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("urgency")] string Urgency,
    [property: JsonPropertyName("impact")] string Impact,
    [property: JsonPropertyName("correlation_id")] string CorrelationId,
    [property: JsonPropertyName("assignment_group")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? AssignmentGroup,
    [property: JsonPropertyName("caller_id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? CallerId
);
