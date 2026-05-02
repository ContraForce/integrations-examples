using System.Net;
using System.Text;
using ContraForce.Samples.HaloOutbound.Webhook;

namespace ContraForce.Samples.HaloOutbound.Halo;

/// <summary>
/// Translates a ContraForce incident into a Halo ticket upsert payload.
/// Edit this file to fit your Halo workflow — everything else is plumbing.
/// </summary>
public static class TicketMapper
{
    public static HaloTicketUpsert Map(
        IncidentCreatedPayload payload,
        string externalReference,
        HaloOptions options,
        int? existingTicketId
    )
    {
        var summary = Truncate($"[CF #{payload.IncidentNumber}] {payload.Title}", 200);
        var customFields = options.ExternalRefFieldId is int fieldId
            ? new[] { new HaloCustomField(fieldId, externalReference) }
            : null;

        return new HaloTicketUpsert(
            Id: existingTicketId,
            Summary: summary,
            DetailsHtml: BuildDetailsHtml(payload),
            ClientId: options.DefaultClientId,
            TicketTypeId: options.DefaultTicketTypeId,
            PriorityId: MapPriority(payload.Severity),
            ThirdPartyNumber: options.ExternalRefFieldId is null ? externalReference : null,
            CustomFields: customFields
        );
    }

    public static int MapPriority(string severity) =>
        severity?.ToUpperInvariant() switch
        {
            "HIGH" => 1,
            "MEDIUM" => 2,
            _ => 3,
        };

    private static string BuildDetailsHtml(IncidentCreatedPayload p)
    {
        var sb = new StringBuilder();
        sb.Append("<p><strong>ContraForce incident</strong> ");
        sb.Append(WebUtility.HtmlEncode(p.IncidentId));
        sb.Append(" (#");
        sb.Append(p.IncidentNumber);
        sb.AppendLine(")</p>");

        sb.AppendLine("<ul>");
        sb.AppendLine($"<li><strong>Severity:</strong> {WebUtility.HtmlEncode(p.Severity)}</li>");
        sb.AppendLine(
            $"<li><strong>Source:</strong> {WebUtility.HtmlEncode(p.SourceDisplayName)}</li>"
        );
        sb.AppendLine($"<li><strong>Created:</strong> {p.CreatedAt:u}</li>");
        if (p.Owner is not null)
        {
            sb.AppendLine(
                $"<li><strong>Owner:</strong> {WebUtility.HtmlEncode(p.Owner.DisplayName)} &lt;{WebUtility.HtmlEncode(p.Owner.Email)}&gt;</li>"
            );
        }
        sb.AppendLine("</ul>");

        if (!string.IsNullOrWhiteSpace(p.Description))
        {
            sb.Append("<p>");
            sb.Append(WebUtility.HtmlEncode(p.Description));
            sb.AppendLine("</p>");
        }

        if (p.Alerts.Length > 0)
        {
            sb.AppendLine("<p><strong>Alerts</strong></p><ul>");
            foreach (var alert in p.Alerts)
            {
                sb.Append("<li>[");
                sb.Append(WebUtility.HtmlEncode(alert.Severity));
                sb.Append("] ");
                sb.Append(WebUtility.HtmlEncode(alert.Title));
                sb.Append(" (");
                sb.Append(WebUtility.HtmlEncode(alert.ProductName));
                sb.AppendLine(")</li>");
            }
            sb.AppendLine("</ul>");
        }

        if (p.Entities.Length > 0)
        {
            sb.AppendLine("<p><strong>Entities</strong></p><ul>");
            foreach (var entity in p.Entities)
            {
                sb.Append("<li>");
                sb.Append(WebUtility.HtmlEncode(entity.Type));
                sb.Append(": ");
                sb.Append(WebUtility.HtmlEncode(entity.DisplayName));
                sb.AppendLine("</li>");
            }
            sb.AppendLine("</ul>");
        }

        return sb.ToString();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
