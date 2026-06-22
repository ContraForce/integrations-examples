using System.Text.Json.Serialization;

namespace ContraForce.Samples.SnowInbound.CallbackModels;

/// <summary>
/// Minimal shape of the JSON a ServiceNow Business Rule (or Flow Designer
/// outbound REST message) posts when an incident changes. The receiver only
/// needs the <c>sys_id</c> — it re-fetches the full record from the Table API.
/// </summary>
public sealed record SnowCallbackPayload(
    [property: JsonPropertyName("sys_id")] string? SysId,
    [property: JsonPropertyName("number")] string? Number
);
