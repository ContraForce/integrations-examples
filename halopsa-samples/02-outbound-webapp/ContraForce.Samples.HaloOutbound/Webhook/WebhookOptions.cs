using System.ComponentModel.DataAnnotations;

namespace ContraForce.Samples.HaloOutbound.Webhook;

public sealed class ContraForceWebhookOptions
{
    public const string SectionName = "ContraForce";

    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    public int MaxSkewSeconds { get; set; } = 300;
}
