using System.Text;
using ContraForce.Samples.CwOutbound.Webhook;

namespace ContraForce.Samples.CwOutbound.ConnectWise;

/// <summary>
/// Translates a ContraForce incident into a ConnectWise ticket create payload.
/// Edit this file to fit your board schema — everything else in the sample
/// is plumbing.
/// </summary>
public static class TicketMapper
{
    public static CwTicketCreate Map(
        IncidentCreatedPayload payload,
        string externalReference,
        ConnectWiseOptions options
    )
    {
        var summary = Truncate($"[CF #{payload.IncidentNumber}] {payload.Title}", 100);
        return new CwTicketCreate(
            Summary: summary,
            InitialDescription: BuildDescription(payload),
            Board: new CwBoardRef(options.DefaultBoardId),
            Company: new CwCompanyRef(options.DefaultCompanyIdentifier),
            Priority: new CwPriorityRef(MapPriority(payload.Severity)),
            ExternalReference: externalReference
        );
    }

    public static int MapPriority(string severity) =>
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
