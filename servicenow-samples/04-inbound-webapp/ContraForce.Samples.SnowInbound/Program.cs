using ContraForce.Samples.SnowInbound.CallbackModels;
using ContraForce.Samples.SnowInbound.ContraForce;
using ContraForce.Samples.SnowInbound.ServiceNow;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<ServiceNowOptions>()
    .Bind(builder.Configuration.GetSection(ServiceNowOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ContraForceOptions>()
    .Bind(builder.Configuration.GetSection(ContraForceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ServiceNowClient>();
builder.Services.AddHttpClient<ContraForceClient>();
builder.Services.AddSingleton<ChangeTracker>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/snow/callbacks",
    async (
        HttpContext http,
        SnowCallbackPayload callback,
        ServiceNowClient snow,
        ContraForceClient cf,
        ChangeTracker tracker,
        IOptions<ServiceNowOptions> snowOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    ) =>
    {
        var opts = snowOptions.Value;

        // 1. Reject callbacks missing the shared secret.
        var providedSecret = http.Request.Headers["X-SNow-Secret"].ToString();
        if (!string.Equals(providedSecret, opts.CallbackSecret, StringComparison.Ordinal))
        {
            logger.LogWarning("ServiceNow callback missing/invalid X-SNow-Secret");
            return Results.Unauthorized();
        }

        if (string.IsNullOrEmpty(callback.SysId))
            return Results.Ok(new { status = "ignored", reason = "no sys_id in payload" });

        // 2. Pull the full incident — the Business Rule payload only carries the
        //    sys_id, so re-fetch to get a stable shape including correlation_id.
        var incident = await snow.GetIncidentAsync(callback.SysId, cancellationToken);
        if (incident is null)
            return Results.Ok(new { status = "ignored", reason = "incident not found" });

        // 3. Only act on incidents our outbound side created.
        if (
            !ExternalReferenceParser.TryParse(
                incident.CorrelationId,
                out var source,
                out var incidentId
            )
        )
            return Results.Ok(new { status = "ignored", reason = "not a CF-linked incident" });

        var previous = tracker.Snapshot(incident.SysId);
        var journal = await snow.GetJournalEntriesAsync(incident.SysId, cancellationToken);

        // Advance the watermark past every entry we've now seen, forwarded or
        // not, so a later callback doesn't reprocess them.
        var latestCreatedOn =
            journal
                .Select(e => e.CreatedOn ?? string.Empty)
                .Append(previous.LastJournalCreatedOn)
                .Max(StringComparer.Ordinal)
            ?? previous.LastJournalCreatedOn;

        // 4. Forward new journal entries added since the last time we saw this
        //    incident. Forward customer-visible comments by default (and work
        //    notes too when configured); skip entries authored by our own
        //    integration user to avoid echo loops.
        var newEntries = journal
            .Where(e =>
                string.Equals(e.Element, "comments", StringComparison.OrdinalIgnoreCase)
                || (
                    opts.ForwardWorkNotes
                    && string.Equals(e.Element, "work_notes", StringComparison.OrdinalIgnoreCase)
                )
            )
            .Where(e =>
                string.CompareOrdinal(e.CreatedOn ?? string.Empty, previous.LastJournalCreatedOn)
                > 0
            )
            .Where(e => !IsOwnEcho(e, opts.IntegrationUser))
            .OrderBy(e => e.CreatedOn, StringComparer.Ordinal)
            .ToList();

        foreach (var entry in newEntries)
        {
            var text = entry.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var author = entry.CreatedBy ?? "ServiceNow";
            var kind = string.Equals(
                entry.Element,
                "work_notes",
                StringComparison.OrdinalIgnoreCase
            )
                ? "work note"
                : "comment";
            var body = $"[ServiceNow {incident.Number} {kind} by {author}]\n\n{text}";
            await cf.AddIncidentCommentAsync(incidentId, source, body, cancellationToken);
            logger.LogInformation(
                "Forwarded ServiceNow journal entry {EntryId} to CF incident {IncidentId}",
                entry.SysId,
                incidentId
            );
        }

        // 5. Close on the CF side if the ServiceNow incident just resolved/closed.
        var stateValue = int.TryParse(incident.State, out var s) ? s : -1;
        var isClosed = stateValue == opts.ResolvedState || stateValue == opts.ClosedState;
        if (isClosed && !previous.Closed)
        {
            var detail = string.IsNullOrWhiteSpace(incident.CloseNotes)
                ? incident.CloseCode
                : incident.CloseNotes;
            var closingComment =
                $"Closed in ServiceNow (incident {incident.Number}, state {incident.State})."
                + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}");
            await cf.CloseIncidentAsync(incidentId, source, closingComment, cancellationToken);
            logger.LogInformation(
                "Closed CF incident {IncidentId} mirroring ServiceNow incident {Number}",
                incidentId,
                incident.Number
            );
        }

        tracker.Record(
            incident.SysId,
            new IncidentState(LastJournalCreatedOn: latestCreatedOn, Closed: isClosed)
        );
        return Results.Ok(
            new
            {
                status = "processed",
                commentsForwarded = newEntries.Count,
                closed = isClosed && !previous.Closed,
            }
        );
    }
);

app.Run();

static bool IsOwnEcho(SnowJournalEntry entry, string? integrationUser)
{
    if (string.IsNullOrEmpty(integrationUser))
        return false;
    return string.Equals(entry.CreatedBy, integrationUser, StringComparison.OrdinalIgnoreCase);
}
