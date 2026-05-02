using System.Text.Json.Serialization;

namespace ContraForce.Samples.HaloOutbound.Halo;

public sealed record HaloTicket(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("thirdpartynumber")] string? ThirdPartyNumber,
    [property: JsonPropertyName("status_id")] int? StatusId
);

/// <summary>
/// Halo's <c>POST /api/Tickets</c> body — same endpoint serves create and
/// update; include an <c>id</c> to update.
/// </summary>
public sealed record HaloTicketUpsert(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("details_html")] string DetailsHtml,
    [property: JsonPropertyName("client_id")] int ClientId,
    [property: JsonPropertyName("tickettype_id")] int TicketTypeId,
    [property: JsonPropertyName("priority_id")] int PriorityId,
    [property: JsonPropertyName("thirdpartynumber")] string? ThirdPartyNumber,
    [property: JsonPropertyName("customfields")] HaloCustomField[]? CustomFields
);

public sealed record HaloCustomField(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("value")] string Value
);

public sealed record HaloAction(
    [property: JsonPropertyName("ticket_id")] int TicketId,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("note_html")] string NoteHtml,
    [property: JsonPropertyName("hiddenfromuser")] bool HiddenFromUser
);
