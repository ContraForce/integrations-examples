using ContraForce.Samples.HaloInbound.ContraForce;
using ContraForce.Samples.HaloInbound.Halo;
using ContraForce.Samples.HaloInbound.Webhook;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<HaloOptions>()
    .Bind(builder.Configuration.GetSection(HaloOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ContraForceOptions>()
    .Bind(builder.Configuration.GetSection(ContraForceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<HaloTokenProvider>();
builder.Services.AddHttpClient<HaloClient>();
builder.Services.AddHttpClient<ContraForceClient>();
builder.Services.AddSingleton<ChangeTracker>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/halo/webhooks",
    async (
        HttpContext http,
        HaloWebhookPayload payload,
        HaloClient halo,
        ContraForceClient cf,
        ChangeTracker tracker,
        IOptions<HaloOptions> haloOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    ) =>
    {
        var opts = haloOptions.Value;

        // 1. Reject webhooks missing the shared secret.
        var providedSecret = http.Request.Headers["X-Halo-Secret"].ToString();
        if (!string.Equals(providedSecret, opts.WebhookSecret, StringComparison.Ordinal))
        {
            logger.LogWarning("Halo webhook missing/invalid X-Halo-Secret");
            return Results.Unauthorized();
        }

        var ticketId = payload.ResolveTicketId();
        if (ticketId is null)
            return Results.Ok(new { status = "ignored", reason = "no ticket id in payload" });

        // 2. Pull the full ticket — Halo's webhook payload varies by trigger
        //    so we re-fetch to get a stable shape.
        var ticket = await halo.GetTicketAsync(ticketId.Value, cancellationToken);
        if (ticket is null)
            return Results.Ok(new { status = "ignored", reason = "ticket not found" });

        // 3. Only act on tickets our outbound side created.
        var externalReference = halo.ExtractExternalReference(ticket);
        if (
            !ExternalReferenceParser.TryParse(externalReference, out var source, out var incidentId)
        )
            return Results.Ok(new { status = "ignored", reason = "not a CF-linked ticket" });

        var previous = tracker.Snapshot(ticket.Id);
        var actions = await halo.GetTicketActionsAsync(ticket.Id, cancellationToken);
        var latestActionId = actions.Count > 0 ? actions.Max(a => a.Id) : 0;

        // 4. Forward any new public actions (notes / replies) added since the
        //    last time we saw this ticket. Skip private notes and our own
        //    integrator's actions to avoid echo-loops.
        var newActions = actions
            .Where(a => a.Id > previous.LastActionId)
            .Where(a => !a.HiddenFromUser)
            .Where(a => opts.IntegrationAgentId is not int agentId || a.AgentId != agentId)
            .OrderBy(a => a.Id)
            .ToList();

        foreach (var action in newActions)
        {
            var text = action.NoteHtml ?? action.Note ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var who = action.Who ?? "Halo";
            var body = $"[Halo ticket #{ticket.Id} note by {who}]\n\n{text}";
            await cf.AddIncidentCommentAsync(incidentId, source, body, cancellationToken);
            logger.LogInformation(
                "Forwarded Halo action {ActionId} to CF incident {IncidentId}",
                action.Id,
                incidentId
            );
        }

        // 5. Close on the CF side if the Halo ticket just closed.
        var isClosed = ticket.StatusId == opts.ClosedStatusId;
        if (isClosed && !previous.Closed)
        {
            var comment =
                $"Closed in HaloPSA (ticket #{ticket.Id}, status '{ticket.StatusName ?? "Closed"}').";
            await cf.CloseIncidentAsync(incidentId, source, comment, cancellationToken);
            logger.LogInformation(
                "Closed CF incident {IncidentId} mirroring Halo ticket {TicketId}",
                incidentId,
                ticket.Id
            );
        }

        tracker.Record(ticket.Id, new TicketState(LastActionId: latestActionId, Closed: isClosed));
        return Results.Ok(
            new
            {
                status = "processed",
                actionsForwarded = newActions.Count,
                closed = isClosed && !previous.Closed,
            }
        );
    }
);

app.Run();
