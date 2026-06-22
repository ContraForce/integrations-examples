using System.ComponentModel.DataAnnotations;

namespace ContraForce.Samples.SnowInbound.CallbackModels;

public sealed class ServiceNowOptions
{
    public const string SectionName = "ServiceNow";

    [Required]
    public string InstanceUrl { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret expected on the <c>X-SNow-Secret</c> header of each
    /// incoming Business Rule callback. Callbacks without this header are
    /// rejected. ServiceNow doesn't sign outbound REST messages, so this is the
    /// auth boundary.
    /// </summary>
    [Required]
    public string CallbackSecret { get; set; } = string.Empty;

    /// <summary>
    /// Numeric <c>state</c> value that represents "Resolved" (default 6). A
    /// transition into this state mirrors a close back to ContraForce.
    /// </summary>
    public int ResolvedState { get; set; } = 6;

    /// <summary>
    /// Numeric <c>state</c> value that represents "Closed" (default 7).
    /// </summary>
    public int ClosedState { get; set; } = 7;

    /// <summary>
    /// When false (default), only customer-visible <c>comments</c> journal
    /// entries are forwarded to ContraForce. Set true to also forward internal
    /// <c>work_notes</c>.
    /// </summary>
    public bool ForwardWorkNotes { get; set; }

    /// <summary>
    /// Optional username the integration authenticates as. Journal entries
    /// authored by this user are skipped to avoid echo loops.
    /// </summary>
    public string? IntegrationUser { get; set; }
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
