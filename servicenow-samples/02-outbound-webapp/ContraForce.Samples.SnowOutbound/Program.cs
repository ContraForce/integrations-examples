using System.Text.Json;
using ContraForce.Samples.SnowOutbound.ServiceNow;
using ContraForce.Samples.SnowOutbound.Webhook;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddOptions<ContraForceWebhookOptions>()
    .Bind(builder.Configuration.GetSection(ContraForceWebhookOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder
    .Services.AddOptions<ServiceNowOptions>()
    .Bind(builder.Configuration.GetSection(ServiceNowOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ServiceNowClient>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/cf/webhooks",
    async (
        HttpContext http,
        ServiceNowClient snow,
        IOptions<ContraForceWebhookOptions> webhookOptions,
        IOptions<ServiceNowOptions> snowOptions,
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

        // 5. Idempotency — dedupe by (source, incidentId) stored in the native
        //    correlation_id field. The inbound receiver reads this back to route
        //    ServiceNow incident changes to the correct CF incident.
        var incident = envelope.Data;
        var correlationId = $"cf|{incident.Source}|{incident.IncidentId}";
        var existingSysId = await snow.FindIncidentByCorrelationIdAsync(
            correlationId,
            cancellationToken
        );
        if (existingSysId is not null)
        {
            await snow.AddWorkNoteAsync(
                existingSysId,
                $"ContraForce re-delivered event {eventId}",
                cancellationToken
            );
            return Results.Ok(new { status = "updated", sysId = existingSysId });
        }

        var create = IncidentMapper.Map(incident, correlationId, snowOptions.Value);
        var created = await snow.CreateIncidentAsync(create, cancellationToken);
        return Results.Ok(
            new
            {
                status = "created",
                sysId = created.SysId,
                number = created.Number,
            }
        );
    }
);

app.Run();
