using ContraForce.Samples.CwInbound.CallbackModels;
using ContraForce.Samples.CwInbound.ConnectWise;
using ContraForce.Samples.CwInbound.ContraForce;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<ConnectWiseOptions>()
    .Bind(builder.Configuration.GetSection(ConnectWiseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ContraForceOptions>()
    .Bind(builder.Configuration.GetSection(ContraForceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ConnectWiseClient>();
builder.Services.AddHttpClient<ContraForceClient>();
builder.Services.AddSingleton<ChangeTracker>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/cw/callbacks",
    async (
        HttpContext http,
        CwCallbackPayload callback,
        ConnectWiseClient cw,
        ContraForceClient cf,
        ChangeTracker tracker,
        IOptions<ConnectWiseOptions> cwOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    ) =>
    {
        // 1. Reject callbacks missing the shared secret.
        var providedSecret = http.Request.Headers["X-Callback-Secret"].ToString();
        if (!string.Equals(providedSecret, cwOptions.Value.CallbackSecret, StringComparison.Ordinal))
        {
            logger.LogWarning("CW callback missing/invalid X-Callback-Secret");
            return Results.Unauthorized();
        }

        // 2. Only handle service-ticket callbacks.
        if (!string.Equals(callback.Type, "ServiceTicket", StringComparison.OrdinalIgnoreCase))
            return Results.Ok(new { status = "ignored", reason = "not a ServiceTicket" });

        var ticket = await cw.GetTicketAsync(callback.ObjectId, cancellationToken);
        if (ticket is null)
            return Results.Ok(new { status = "ignored", reason = "ticket not found" });

        // 3. Only act on tickets our outbound side created.
        if (!ExternalReferenceParser.TryParse(ticket.ExternalReference, out var source, out var incidentId))
            return Results.Ok(new { status = "ignored", reason = "not a CF-linked ticket" });

        var previous = tracker.Snapshot(ticket.Id);
        var notes = await cw.GetTicketNotesAsync(ticket.Id, pageSize: 50, cancellationToken);
        var latestNoteId = notes.Count > 0 ? notes.Max(n => n.Id) : 0;

        // 4. Forward any new notes added since the last time we saw this ticket.
        //    Skip notes authored by our own integrator so we don't echo-loop.
        var memberId = callback.MemberId ?? string.Empty;
        var newNotes = notes
            .Where(n => n.Id > previous.LastNoteId)
            .Where(n => !IsOwnEcho(n, memberId))
            .OrderBy(n => n.Id)
            .ToList();

        foreach (var note in newNotes)
        {
            var text = note.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var author = note.Info?.UpdatedBy ?? memberId;
            var body = $"[CW ticket #{ticket.Id} note by {author}]\n\n{text}";
            await cf.AddIncidentCommentAsync(incidentId, source, body, cancellationToken);
            logger.LogInformation("Forwarded CW note {NoteId} to CF incident {IncidentId}", note.Id, incidentId);
        }

        // 5. Close on the CF side if the CW ticket just closed.
        if (ticket.ClosedFlag && !previous.Closed)
        {
            var closingComment = $"Closed in ConnectWise (ticket #{ticket.Id}, status '{ticket.Status?.Name}').";
            await cf.CloseIncidentAsync(incidentId, source, closingComment, cancellationToken);
            logger.LogInformation("Closed CF incident {IncidentId} mirroring CW ticket {TicketId}", incidentId, ticket.Id);
        }

        tracker.Record(ticket.Id, new TicketState(LastNoteId: latestNoteId, Closed: ticket.ClosedFlag));
        return Results.Ok(new { status = "processed", notesForwarded = newNotes.Count, closed = ticket.ClosedFlag && !previous.Closed });
    }
);

app.Run();

static bool IsOwnEcho(CwTicketNote note, string integratorMemberId)
{
    if (string.IsNullOrEmpty(integratorMemberId))
        return false;
    return string.Equals(note.Info?.UpdatedBy, integratorMemberId, StringComparison.OrdinalIgnoreCase);
}
