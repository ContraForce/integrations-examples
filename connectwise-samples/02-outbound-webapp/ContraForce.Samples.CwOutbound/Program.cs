using System.Text.Json;
using ContraForce.Samples.CwOutbound.ConnectWise;
using ContraForce.Samples.CwOutbound.Webhook;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<ContraForceWebhookOptions>()
    .Bind(builder.Configuration.GetSection(ContraForceWebhookOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ConnectWiseOptions>()
    .Bind(builder.Configuration.GetSection(ConnectWiseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ConnectWiseClient>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/cf/webhooks",
    async (
        HttpContext http,
        ConnectWiseClient cw,
        Microsoft.Extensions.Options.IOptions<ContraForceWebhookOptions> webhookOptions,
        Microsoft.Extensions.Options.IOptions<ConnectWiseOptions> cwOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken
    ) =>
    {
        // 1. Read raw body — required for exact-bytes HMAC verification.
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

        // 4. Only act on schemas we understand; ack everything else as a no-op
        //    so ContraForce doesn't retry indefinitely.
        if (!string.Equals(schema, "incident.created.v1", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Ignoring unhandled webhook schema {Schema}", schema);
            return Results.Ok(new { status = "ignored", reason = "unhandled schema" });
        }

        var envelope = JsonSerializer.Deserialize<WebhookEnvelope>(rawBody);
        if (envelope?.Data is null)
            return Results.BadRequest(new { error = "malformed payload" });

        // 5. Idempotency — dedupe by (source, incidentId). This identifier is also
        //    what the inbound receiver reads back to route CW ticket changes to
        //    the correct CF incident.
        var incident = envelope.Data;
        var externalReference = $"cf|{incident.Source}|{incident.IncidentId}";
        var existingId = await cw.FindTicketByExternalReferenceAsync(
            externalReference,
            cancellationToken
        );
        if (existingId is int ticketId)
        {
            await cw.AddNoteAsync(
                ticketId,
                $"ContraForce re-delivered event {eventId}",
                cancellationToken
            );
            return Results.Ok(new { status = "updated", ticketId });
        }

        var ticket = TicketMapper.Map(incident, externalReference, cwOptions.Value);
        var newId = await cw.CreateTicketAsync(ticket, cancellationToken);
        return Results.Ok(new { status = "created", ticketId = newId });
    }
);

app.Run();
