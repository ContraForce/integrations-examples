using System.Text;
using ContraForce.Samples.SnowOutbound.Webhook;

namespace ContraForce.Samples.SnowOutbound.ServiceNow;

/// <summary>
/// Translates a ContraForce incident into a ServiceNow incident create payload.
/// Edit this file to fit your instance — everything else in the sample is
/// plumbing.
/// </summary>
public static class IncidentMapper
{
    public static SnowIncidentCreate Map(
        IncidentCreatedPayload payload,
        string correlationId,
        ServiceNowOptions options
    )
    {
        var shortDescription = Truncate($"[CF #{payload.IncidentNumber}] {payload.Title}", 160);
        return new SnowIncidentCreate(
            ShortDescription: shortDescription,
            Description: BuildDescription(payload),
            Urgency: MapUrgency(payload.Severity).ToString(),
            Impact: options.DefaultImpact.ToString(),
            CorrelationId: correlationId,
            AssignmentGroup: string.IsNullOrWhiteSpace(options.AssignmentGroupSysId)
                ? null
                : options.AssignmentGroupSysId,
            CallerId: string.IsNullOrWhiteSpace(options.CallerSysId) ? null : options.CallerSysId
        );
    }

    /// <summary>
    /// ServiceNow urgency: 1 = High, 2 = Medium, 3 = Low. Priority is derived
    /// from the urgency × impact matrix on the instance.
    /// </summary>
    public static int MapUrgency(string severity) =>
        severity?.ToUpperInvariant() switch
        {
            "HIGH" => 1,
            "MEDIUM" => 2,
            _ => 3,
        };

    private static string BuildDescription(IncidentCreatedPayload p)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ContraForce incident {p.IncidentId} (#{p.IncidentNumber})");
        sb.AppendLine();
        sb.AppendLine($"Severity: {p.Severity}");
        sb.AppendLine($"Source:   {p.SourceDisplayName}");
        sb.AppendLine($"Created:  {p.CreatedAt:u}");
        if (p.Owner is not null)
            sb.AppendLine($"Owner:    {p.Owner.DisplayName} <{p.Owner.Email}>");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(p.Description))
        {
            sb.AppendLine(p.Description);
            sb.AppendLine();
        }

        if (p.Alerts.Length > 0)
        {
            sb.AppendLine("Alerts:");
            foreach (var alert in p.Alerts)
                sb.AppendLine($"  - [{alert.Severity}] {alert.Title} ({alert.ProductName})");
            sb.AppendLine();
        }

        if (p.Entities.Length > 0)
        {
            sb.AppendLine("Entities:");
            foreach (var entity in p.Entities)
                sb.AppendLine($"  - {entity.Type}: {entity.DisplayName}");
        }

        return sb.ToString();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
