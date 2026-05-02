using System.Text.Json.Serialization;

namespace ContraForce.Samples.HaloInbound.Webhook;

/// <summary>
/// Loose shape for the body Halo posts on a webhook. Halo's payload varies
/// by trigger and tenant config — we only need the ticket id, so accept
/// either the ticket-style payload (<c>id</c>) or the action-style payload
/// (<c>ticket_id</c>).
/// </summary>
public sealed record HaloWebhookPayload(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("ticket_id")] int? TicketId,
    [property: JsonPropertyName("agent_id")] int? AgentId,
    [property: JsonPropertyName("hiddenfromuser")] bool? HiddenFromUser,
    [property: JsonPropertyName("outcome")] string? Outcome
)
{
    public int? ResolveTicketId() => TicketId ?? Id;
}
