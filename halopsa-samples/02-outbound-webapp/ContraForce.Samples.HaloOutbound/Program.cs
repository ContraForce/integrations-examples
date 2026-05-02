using System.Text.Json;
using ContraForce.Samples.HaloOutbound.Halo;
using ContraForce.Samples.HaloOutbound.Webhook;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<ContraForceWebhookOptions>()
    .Bind(builder.Configuration.GetSection(ContraForceWebhookOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<HaloOptions>()
    .Bind(builder.Configuration.GetSection(HaloOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<HaloTokenProvider>();
builder.Services.AddHttpClient<HaloClient>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/cf/webhooks",
    async (
        HttpContext http,
        HaloClient halo,
        IOptions<ContraForceWebhookOptions> webhookOptions,
        IOptions<HaloOptions> haloOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    ) =>
    {
        // 1. Read the raw body — required for exact-bytes HMAC verification.
        http.Request.EnableBuffering();
        using var ms = new MemoryStream();
        await http.Request.Body.CopyToAsync(ms, cancellationToken);
        var rawBody = ms.ToArray();

        // 2. Extract CF headers.
        var sig = http.Request.Headers["X-CF-Signature"].ToString();
        var ts = http.Request.Headers["X-CF-Timestamp"].ToString();
        var eventId = http.Request.Headers["X-CF-Event-Id"].ToString();
        var schema = http.Request.Headers["X-CF-Schema"].ToString();

        if (string.IsNullOrEmpty(eventId))
        {
            logger.LogWarning("Webhook missing X-CF-Event-Id");
            return Results.BadRequest(new { error = "missing X-CF-Event-Id" });
        }

        // 3. Verify the signature against the raw body.
        var opts = webhookOptions.Value;
        var valid = WebhookSignatureValidator.Verify(
            secret: opts.WebhookSecret,
            signatureHeader: sig,
            timestampHeader: ts,
            rawBody: rawBody,
            maxSkewSeconds: opts.MaxSkewSeconds,
            now: DateTimeOffset.UtcNow
        );

        if (!valid)
        {
            logger.LogWarning("Webhook signature verification failed for event {EventId}", eventId);
            return Results.Unauthorized();
        }

        // 4. Only act on schemas we understand; ack everything else as a no-op.
        if (!string.Equals(schema, "incident.created.v1", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Ignoring unhandled webhook schema {Schema}", schema);
            return Results.Ok(new { status = "ignored", reason = "unhandled schema" });
        }

        var envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawBody);
        if (envelope?.Data is null)
            return Results.BadRequest(new { error = "malformed payload" });

        // 5. Idempotency — dedupe by (source, incidentId).
        var incident = envelope.Data;
        var externalReference = $"cf|{incident.Source}|{incident.IncidentId}";
        var existingId = await halo.FindTicketByExternalReferenceAsync(
            externalReference,
            cancellationToken
        );

        if (existingId is int ticketId)
        {
            await halo.AddNoteAsync(
                ticketId,
                noteHtml: $"<p>ContraForce re-delivered event <code>{eventId}</code></p>",
                hiddenFromUser: true,
                cancellationToken
            );
            return Results.Ok(new { status = "updated", ticketId });
        }

        var upsert = TicketMapper.Map(
            incident,
            externalReference,
            haloOptions.Value,
            existingTicketId: null
        );
        var newId = await halo.CreateOrUpdateTicketAsync(upsert, cancellationToken);
        return Results.Ok(new { status = "created", ticketId = newId });
    }
);

app.Run();
