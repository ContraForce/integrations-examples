using System.Text.Json.Serialization;

namespace ContraForce.Samples.CwInbound.CallbackModels;

/// <summary>
/// Minimal shape of a ConnectWise Manage Callback payload.
/// Only fields used by this sample are mapped.
/// </summary>
public sealed record CwCallbackPayload(
    [property: JsonPropertyName("ID")] int Id,
    [property: JsonPropertyName("Type")] string Type,
    [property: JsonPropertyName("Action")] string Action,
    [property: JsonPropertyName("MemberID")] string? MemberId,
    [property: JsonPropertyName("ObjectID")] int ObjectId);
