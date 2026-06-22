using System.ComponentModel.DataAnnotations;

namespace ContraForce.Samples.SnowOutbound.ServiceNow;

public sealed class ServiceNowOptions
{
    public const string SectionName = "ServiceNow";

    /// <summary>
    /// ServiceNow instance base URL, e.g.
    /// <c>https://dev12345.service-now.com</c>. The sample appends
    /// <c>/api/now/table/</c> itself.
    /// </summary>
    [Required]
    public string InstanceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Username of a dedicated integration user with the
    /// <c>itil</c> / <c>web_service_admin</c> roles (or a custom role granting
    /// read/create/write on the <c>incident</c> table).
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Default <c>impact</c> value (1 = High, 2 = Medium, 3 = Low). ServiceNow
    /// derives <c>priority</c> from the urgency × impact matrix, so we set
    /// urgency from the incident severity and impact from this default.
    /// </summary>
    public int DefaultImpact { get; set; } = 2;

    /// <summary>
    /// Optional sys_id of an assignment group to route new incidents to.
    /// Leave empty to let ServiceNow assignment rules decide.
    /// </summary>
    public string? AssignmentGroupSysId { get; set; }

    /// <summary>
    /// Optional sys_id of the caller (a <c>sys_user</c> record). Some instances
    /// require <c>caller_id</c> on create; set it if yours does.
    /// </summary>
    public string? CallerSysId { get; set; }
}
