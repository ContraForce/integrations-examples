using System.Text.Json.Serialization;

namespace ContraForce.Samples.SnowOutbound.Webhook;

/// <summary>
/// Top-level envelope ContraForce sends for every webhook event.
/// </summary>
public sealed record WebhookEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("isTest")] bool IsTest,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("data")] IncidentCreatedPayload? Data
);

/// <summary>
/// Payload shape for <c>incident.created.v1</c>. Fields mirror
/// <c>EventCastSubscriber.eventPayloadObject</c> on the ContraForce side.
/// </summary>
public sealed record IncidentCreatedPayload(
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("accountName")] string AccountName,
    [property: JsonPropertyName("incidentId")] string IncidentId,
    [property: JsonPropertyName("incidentNumber")] int IncidentNumber,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("sourceDisplayName")] string SourceDisplayName,
    [property: JsonPropertyName("owner")] Owner? Owner,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("lastActivityAt")] DateTimeOffset LastActivityAt,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("alertProductNames")] string[] AlertProductNames,
    [property: JsonPropertyName("alerts")] Alert[] Alerts,
    [property: JsonPropertyName("entities")] Entity[] Entities
);

public sealed record Owner(
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("email")] string? Email
);

public sealed record Alert(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("productName")] string ProductName,
    [property: JsonPropertyName("vendorName")] string VendorName
);

public sealed record Entity(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("displayName")] string DisplayName
);
