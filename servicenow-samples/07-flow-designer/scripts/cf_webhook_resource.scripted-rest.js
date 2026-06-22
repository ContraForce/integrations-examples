// Scripted REST API resource script — ContraForce inbound webhook receiver.
//
// Where this goes:
//   System Web Services → Scripted REST APIs → New
//     - Name:   ContraForce Webhooks
//     - API ID: cf_webhooks
//   Then add a resource:
//     - HTTP method: POST
//     - Relative path: /incident
//   Paste this into the resource's "Script" field.
//
// Full inbound endpoint becomes:
//   https://<instance>.service-now.com/api/<scope>/cf_webhooks/incident
//
// IMPORTANT — turn OFF "Requires authentication" on this resource only if you
// fence the endpoint another way. The HMAC check below authenticates the
// *payload* (it proves the body came from ContraForce), but it does not stop an
// attacker from forcing your verifier to run. Best practice: leave platform
// authentication ON with a dedicated integration user AND verify the HMAC, or
// place the endpoint behind a WAF/IP allow-list.

(function process(/*RESTAPIRequest*/ request, /*RESTAPIResponse*/ response) {
    // 1. Load the signing secret out of source control. A system property works;
    //    a credential record or encrypted property is better.
    var secret = gs.getProperty('contraforce.webhook_secret');

    // 2. Read the RAW body and CF headers. request.body.dataString is the exact
    //    bytes ContraForce signed. Do NOT JSON.stringify(request.body.data) — the
    //    re-serialized form is not byte-identical and the signature will fail.
    var rawBody = request.body.dataString;
    var signature = request.getHeader('X-CF-Signature');
    var timestamp = request.getHeader('X-CF-Timestamp');
    var eventId = request.getHeader('X-CF-Event-Id');
    var schema = request.getHeader('X-CF-Schema');

    if (!eventId) {
        response.setStatus(400);
        return { error: 'missing X-CF-Event-Id' };
    }

    // 3. Verify the HMAC signature against the raw body.
    var verifier = new ContraForceWebhookVerifier(secret, 300);
    if (!verifier.verify(signature, timestamp, rawBody)) {
        gs.warn('[ContraForce] signature verification failed for event ' + eventId);
        response.setStatus(401);
        return { error: 'invalid signature' };
    }

    // 4. Only act on schemas we understand; ack everything else as a no-op.
    if (('' + schema).toLowerCase() !== 'incident.created.v1') {
        return { status: 'ignored', reason: 'unhandled schema' };
    }

    var data;
    try {
        data = JSON.parse(rawBody).data;
    } catch (e) {
        response.setStatus(400);
        return { error: 'malformed payload' };
    }
    if (!data) {
        response.setStatus(400);
        return { error: 'malformed payload' };
    }

    // 5. Map the incident and hand off to the subflow ASYNCHRONOUSLY. The
    //    subflow does the find-by-correlation_id / create-or-update work; this
    //    resource returns immediately so ContraForce gets a fast 200.
    var severity = ('' + data.severity).toUpperCase();
    var urgency = severity === 'HIGH' ? '1' : (severity === 'MEDIUM' ? '2' : '3');

    var shortDescription = ('[CF #' + data.incidentNumber + '] ' + data.title).substring(0, 160);
    var description =
        'ContraForce incident ' + data.incidentId + ' (#' + data.incidentNumber + ')\n\n' +
        'Severity: ' + data.severity + '\n' +
        'Source: ' + data.sourceDisplayName + '\n\n' +
        (data.description || '');

    var inputs = {
        correlation_id: 'cf|' + data.source + '|' + data.incidentId,
        short_description: shortDescription,
        description: description,
        urgency: urgency,
        impact: '2',
        event_id: eventId
    };

    // global.<name> if the subflow lives in the global scope; adjust the scope
    // prefix to match where you built it.
    sn_fd.FlowAPI.getRunner()
        .subflow('global.cf_create_or_update_incident')
        .inBackground()
        .withInputs(inputs)
        .run();

    response.setStatus(200);
    return { status: 'accepted', eventId: eventId };
})(request, response);
