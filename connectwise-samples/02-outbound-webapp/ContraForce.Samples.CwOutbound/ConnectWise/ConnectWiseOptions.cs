using System.ComponentModel.DataAnnotations;

namespace ContraForce.Samples.CwOutbound.ConnectWise;

public sealed class ConnectWiseOptions
{
    public const string SectionName = "ConnectWise";

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string CompanyId { get; set; } = string.Empty;

    [Required]
    public string PublicKey { get; set; } = string.Empty;

    [Required]
    public string PrivateKey { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public int DefaultBoardId { get; set; }

    [Required]
    public string DefaultCompanyIdentifier { get; set; } = string.Empty;

    public string ApiVersionHeader { get; set; } =
        "application/vnd.connectwise.com+json; version=2020.1";
}
