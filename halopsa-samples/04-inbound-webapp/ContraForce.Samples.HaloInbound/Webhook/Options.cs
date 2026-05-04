using System.ComponentModel.DataAnnotations;

namespace ContraForce.Samples.HaloInbound.Webhook;

public sealed class HaloOptions
{
    public const string SectionName = "Halo";

    [Required]
    public string AuthUrl { get; set; } = string.Empty;

    [Required]
    public string ApiBaseUrl { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    public string? Tenant { get; set; }

    public string Scope { get; set; } = "all";

    /// <summary>
    /// Shared secret expected on the <c>X-Halo-Secret</c> header of each
    /// incoming Halo webhook. Webhooks without this header are rejected.
    /// </summary>
    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Numeric id of the Halo status that represents "Closed". Look it up
    /// via <c>GET /api/Status</c> against your Halo instance.
    /// </summary>
    [Required]
    public int ClosedStatusId { get; set; }

    /// <summary>
    /// Optional id of the custom field used as the external reference. If
    /// omitted, the integration reads from <c>thirdpartynumber</c>.
    /// </summary>
    public int? ExternalRefFieldId { get; set; }

    /// <summary>
    /// If set, actions authored by this Halo agent id are skipped to avoid
    /// echo loops. Should be the agent the integration logs in as.
    /// </summary>
    public int? IntegrationAgentId { get; set; }
}

public sealed class ContraForceOptions
{
    public const string SectionName = "ContraForce";

    [Required]
    public string ApiBaseUrl { get; set; } = string.Empty;

    [Required]
    public string ServiceAccountClientId { get; set; } = string.Empty;

    [Required]
    public string ServiceAccountClientSecret { get; set; } = string.Empty;

    [Required]
    public Guid WorkspaceId { get; set; }
}
