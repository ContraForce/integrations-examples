using System.ComponentModel.DataAnnotations;

namespace ContraForce.Samples.CwInbound.CallbackModels;

public sealed class ConnectWiseOptions
{
    public const string SectionName = "ConnectWise";

    [Required] public string BaseUrl { get; set; } = string.Empty;
    [Required] public string CompanyId { get; set; } = string.Empty;
    [Required] public string PublicKey { get; set; } = string.Empty;
    [Required] public string PrivateKey { get; set; } = string.Empty;
    [Required] public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret expected on the <c>X-Callback-Secret</c> header of each
    /// incoming CW callback. Callbacks without this header are rejected.
    /// </summary>
    [Required] public string CallbackSecret { get; set; } = string.Empty;

    public string ApiVersionHeader { get; set; } = "application/vnd.connectwise.com+json; version=2020.1";
}

public sealed class ContraForceOptions
{
    public const string SectionName = "ContraForce";

    [Required] public string ApiBaseUrl { get; set; } = string.Empty;
    [Required] public string ServiceAccountClientId { get; set; } = string.Empty;
    [Required] public string ServiceAccountClientSecret { get; set; } = string.Empty;
    [Required] public Guid WorkspaceId { get; set; }
}
