using System.ComponentModel.DataAnnotations;

namespace ContraForce.Samples.HaloOutbound.Halo;

public sealed class HaloOptions
{
    public const string SectionName = "Halo";

    /// <summary>
    /// Halo authorization-server URL. For hosted Halo this is typically
    /// <c>https://&lt;tenant&gt;.halopsa.com/auth</c>. For self-hosted,
    /// see the integration application's configuration in Halo.
    /// </summary>
    [Required]
    public string AuthUrl { get; set; } = string.Empty;

    /// <summary>
    /// Halo REST base URL — typically
    /// <c>https://&lt;tenant&gt;.halopsa.com/api</c>.
    /// </summary>
    [Required]
    public string ApiBaseUrl { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional tenant name; required for some hosted multi-tenant Halo
    /// auth endpoints. Leave empty for single-tenant or self-hosted.
    /// </summary>
    public string? Tenant { get; set; }

    public string Scope { get; set; } = "all";

    [Required]
    public int DefaultTicketTypeId { get; set; }

    [Required]
    public int DefaultClientId { get; set; }

    /// <summary>
    /// If set, the integration stores the external reference value in this
    /// custom field id instead of the built-in <c>thirdpartynumber</c> field.
    /// </summary>
    public int? ExternalRefFieldId { get; set; }
}
