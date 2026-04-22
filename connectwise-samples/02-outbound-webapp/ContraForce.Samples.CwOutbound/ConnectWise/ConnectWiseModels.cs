using System.Text.Json.Serialization;

namespace ContraForce.Samples.CwOutbound.ConnectWise;

public sealed record CwTicket(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("summary")] string? Summary
);

public sealed record CwTicketCreate(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("initialDescription")] string InitialDescription,
    [property: JsonPropertyName("board")] CwBoardRef Board,
    [property: JsonPropertyName("company")] CwCompanyRef Company,
    [property: JsonPropertyName("priority")] CwPriorityRef Priority,
    [property: JsonPropertyName("externalReference")] string ExternalReference
);

public sealed record CwBoardRef([property: JsonPropertyName("id")] int Id);

public sealed record CwCompanyRef([property: JsonPropertyName("identifier")] string Identifier);

public sealed record CwPriorityRef([property: JsonPropertyName("id")] int Id);
