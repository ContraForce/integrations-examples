# Sample 07 — Native ServiceNow Flow Designer integration (no external host)

The samples in folders 01–06 run *outside* ServiceNow — a Logic App, a .NET web
app, or a Python service that you host and that talks to the ServiceNow Table
API. This guide shows the **fully in-platform** alternative: build the same
bi-directional integration with **Flow Designer + IntegrationHub** so nothing
runs outside your ServiceNow instance.

It covers both directions:

- **Outbound (ServiceNow → ContraForce)** — a record-triggered flow that calls
  the ContraForce v2 REST API with a service-account credential to add comments
  and mirror a close. This is the easy half and is pure low-code.
- **Inbound (ContraForce → ServiceNow)** — receive ContraForce's signed
  `incident.created.v1` webhook, verify it, and create/update an incident. This
  half needs one small script because ContraForce signs its webhooks with
  HMAC, which a pure low-code trigger can't verify.

```
                  ┌──────────────────────────────────────────┐
                  │   CF Agent investigates/comments/closes   │
┌──────────────┐  │       ┌──────────────────────────────┐    ┌──────────────┐
│  ContraForce │──┘       │  Scripted REST API + Subflow  │    │              │
│              │─webhook─▶│  (verify HMAC → create/update)│───▶│  ServiceNow  │
│              │          └──────────────────────────────┘    │ Flow Designer│
│              │          ┌──────────────────────────────┐    │              │
│              │◀─REST────│  Record-triggered Flow +      │◀───│  (incident)  │
│              │   API    │  IntegrationHub REST step     │    │              │
└──────────────┘          └──────────────────────────────┘    └──────────────┘
```

## Can ServiceNow actually do this?

Yes, on both counts:

- **Outbound REST from a flow** is the **REST step** in Flow Designer's Action
  Designer, provided by **IntegrationHub**. It performs an outbound HTTP request
  using a **Connection & Credential alias** for auth, with configurable method,
  base URL, resource path, headers, query params and body. (ServiceNow docs:
  *REST step* / *Outbound REST integration using Flow Designer*.)
- **Inbound** has two native options:
  1. A **Scripted REST API** resource whose script verifies the signature and
     then launches a flow/subflow asynchronously with
     `sn_fd.FlowAPI.getRunner().subflow(...).inBackground().run()`. This is the
     option this guide recommends, because it's the only one that can verify
     ContraForce's HMAC signature.
  2. A pure **IntegrationHub REST API Trigger**, which exposes an endpoint that
     starts a flow directly and returns the flow's `execution_id`
     asynchronously. Simpler, but it authenticates the *caller* (Basic/OAuth on
     a user), not a signed payload — see the trade-off below.

## When to use this vs. samples 01–06

| Choose Flow Designer (this guide) when… | Choose a hosted sample (01–06) when… |
|---|---|
| You want zero infrastructure outside ServiceNow | You already run Azure / containers and want them to own the logic |
| Your team builds in ServiceNow and owns IntegrationHub | You need rich mapping, unit tests, or loss-free multi-event state |
| The mapping is straightforward (the defaults here) | You want bit-exact raw-body handling without ServiceNow scripting quirks |

> **IntegrationHub licensing.** The REST step requires the **IntegrationHub**
> plugin (`com.glide.hub.integrations`), and full custom-REST use generally
> requires an **IntegrationHub Enterprise** (or higher) subscription. If you
> don't have it, you can still do outbound calls with a **RESTMessageV2 script**
> from a flow Action or a Business Rule — that path needs no IntegrationHub
> subscription and is exactly what samples 03 / 04 demonstrate server-side.

## Prerequisites

1. **Plugins**: Flow Designer (base, active by default) and **IntegrationHub**
   (+ Enterprise pack for the REST step). For the inbound half you also need the
   ability to create a **Scripted REST API** (admin) and a **Subflow**.
2. **ContraForce service account** (for outbound): create one under
   **Settings → Developers → Service Accounts** with scopes `incidents:read`,
   `incidents:comments`, `incidents:write`. You'll get a Client ID + Secret used
   as HTTP Basic. See the [parent README](../README.md) for the full list.
3. **ContraForce webhook** (for inbound): create a webhook subscribed to
   `incident.created.v1` under **Settings → Developers → Webhooks** and copy the
   signing secret.

---

## Direction 1 — Outbound: ServiceNow incident → ContraForce

Goal: when a CF-linked incident gets a new comment or is resolved/closed, push
that back to ContraForce. "CF-linked" means the incident's `correlation_id`
holds `cf|{source}|{incidentId}` (the value the outbound samples write).

### 1.1 Store the ContraForce credential

ServiceNow REST steps authenticate through a **Connection & Credential alias**,
which keeps the secret out of the flow definition.

1. **Connections & Credentials → Credentials → New → Basic Auth Credentials**.
   - **Username** = ContraForce service-account **Client ID**
   - **Password** = service-account **Client Secret**
   - Name it e.g. `ContraForce Service Account`.
2. **Connections & Credentials → Connection & Credential Aliases → New**.
   - Name: `ContraForce API`, Type: `Connection and Credential`.
3. **Connections → New** under that alias.
   - **Connection URL** = `https://prod.platform.contraforce.com/api/v2`
     (use your environment's host).
   - **Credential** = the Basic Auth credential from step 1.

### 1.2 Build the Action with a REST step

**Flow Designer → New → Action**. Add inputs: `incident_id` (string),
`source` (string), `comment` (string), and a boolean `is_close`. Then add a
**REST step**:

| Field | Value |
|-------|-------|
| Connection | **Use Connection Alias** → `ContraForce API` |
| HTTP Method | `POST` (add comment) / `PUT` (close) |
| Resource path (comment) | `/workspaces/{WORKSPACE_ID}/incidents/{incident_id}/comments` |
| Resource path (close) | `/workspaces/{WORKSPACE_ID}/incidents/{incident_id}/status` |
| Headers | `Content-Type: application/json`, `Accept: application/json` |
| Request body (comment) | see below |
| Request body (close) | see below |

Comment body (map data pills into the JSON):

```json
{
  "incidentId": "<incident_id>",
  "content": "<comment>",
  "source": "<source>"
}
```

Close body:

```json
{
  "incidentId": "<incident_id>",
  "source": "<source>",
  "status": "Closed",
  "comment": "<comment>",
  "classification": "Undetermined",
  "classificationReason": "InaccurateData"
}
```

> `classification` + `classificationReason` are required when closing Sentinel
> incidents. `Undetermined` / `InaccurateData` are neutral defaults — see the
> hosted samples for the rationale.

**Publish the Action.** A ServiceNow Action does not run until it is published.

### 1.3 Build the Flow

**Flow Designer → New → Flow** with a **Record trigger**:

- **Table**: `Incident [incident]`, **When**: `Updated`.
- **Condition**: `Correlation ID` `starts with` `cf|` — so the flow only fires
  for ContraForce-linked incidents, not every incident in the instance.

Steps:

1. **(Comment mirror)** Add an **If**: "Comments (additional comments)" *changes*.
   Inside, call your Action with `is_close = false`, mapping
   `incident.correlation_id` split on `|` into `source` (part 2) and
   `incident_id` (part 3), and `comment` = the latest **Additional comments**
   entry. (Use a small inline script or the journal field's most-recent value.)
2. **(Close mirror)** Add an **If**: `State` `is one of` `Resolved, Closed`.
   Inside, call your Action with `is_close = true` and `comment` = a short
   "Closed in ServiceNow (…)" string.

To recover `source` and `incident_id` from `correlation_id`, a one-line script
step works: `return current.correlation_id.split('|');` then reference `[1]`
and `[2]`.

### Avoiding echo loops

When inbound (Direction 2) writes a comment into ServiceNow, this outbound flow
will see that comment change. Two defenses:

- Have inbound write CF-originated comments as **work notes** while this
  outbound flow forwards **additional comments** only (the default above).
- Or gate the outbound comment step on `sys_created_by != <integration user>`.

---

## Direction 2 — Inbound: ContraForce webhook → ServiceNow incident

ContraForce POSTs a signed `incident.created.v1` event. The challenge: it
authenticates with an **HMAC signature** (`X-CF-Signature: sha256=<hex>`), not a
username/password, so a plain REST API Trigger can't verify it. The robust
pattern is a **Scripted REST API** that verifies the signature in script, then
launches a **subflow** to do the low-code create/update.

### 2.1 Add the signing secret

Store the webhook signing secret as a system property (or, better, an encrypted
property / credential record):

- **sys_properties → New**: name `contraforce.webhook_secret`, value = the
  secret from the CF portal. Mark it private.

### 2.2 Create the HMAC verifier (Script Include)

Create a Script Include named **`ContraForceWebhookVerifier`** and paste
[`scripts/ContraForceWebhookVerifier.script-include.js`](./scripts/ContraForceWebhookVerifier.script-include.js).

It recomputes `HMAC_SHA256(secret, "{timestamp}.{raw_body}")` and compares it to
the header in constant time, and rejects events outside a 5-minute clock skew.

> **Why a hex conversion?** ServiceNow's `GlideCertificateEncryption.generateMac`
> returns the MAC **base64-encoded**, but ContraForce sends it as **lowercase
> hex** (`sha256=…`). The Script Include normalizes the computed MAC to hex
> before comparing. (In a scoped app the class is `CertificateEncryption`.)

### 2.3 Create the Scripted REST API

**System Web Services → Scripted REST APIs → New**:

- Name `ContraForce Webhooks`, API ID `cf_webhooks`.
- Add a **Resource**: HTTP method `POST`, relative path `/incident`.
- Paste [`scripts/cf_webhook_resource.scripted-rest.js`](./scripts/cf_webhook_resource.scripted-rest.js)
  into the resource **Script**.

The resource reads `request.body.dataString` (the **raw** body — never
`JSON.stringify(request.body.data)`, which reorders/reformats and breaks the
signature), verifies it, maps the payload, then calls the subflow
asynchronously and returns `200`.

Your endpoint URL becomes:

```
https://<instance>.service-now.com/api/<scope>/cf_webhooks/incident
```

Paste that into the ContraForce portal as the webhook destination.

> **Authentication setting.** The HMAC check authenticates the *payload*. Keep
> platform authentication **on** (a dedicated integration user) and/or fence the
> endpoint with a WAF/IP allow-list so the verifier isn't exposed to anonymous
> traffic. ContraForce sends only the signature, so you cannot rely on Basic
> auth *from* ContraForce — the HMAC is the payload's authenticity proof.

### 2.4 Build the create-or-update Subflow

**Flow Designer → New → Subflow** named **`cf_create_or_update_incident`**,
**Trigger: None** (it's launched from the resource script). Inputs (match the
`inputs` object the script sends):

| Input | Type |
|-------|------|
| `correlation_id` | String |
| `short_description` | String |
| `description` | String |
| `urgency` | String |
| `impact` | String |
| `event_id` | String |

Steps:

1. **Look Up Records** — `Incident`, where `Correlation ID` `is`
   `{{correlation_id}}`, limit 1. (This is the idempotency check — ContraForce
   delivery is at-least-once.)
2. **If** records found:
   - **Update Record** (the found incident) → set **Work notes** to
     `ContraForce re-delivered event {{event_id}}`.
3. **Else**:
   - **Create Record** → `Incident` with `short_description`, `description`,
     `urgency`, `impact`, and **`correlation_id` = `{{correlation_id}}`**
     (this is what makes future deliveries and the outbound flow find it).

Set the subflow's scope prefix in the resource script's `.subflow('global.…')`
call to wherever you build it.

### Simpler alternative: pure REST API Trigger (no HMAC)

If you're comfortable trusting the transport instead of verifying the signature,
you can skip the Scripted REST API and Script Include and use an **IntegrationHub
REST API Trigger** directly on a flow. It exposes an endpoint that starts the
flow and returns the `execution_id` asynchronously. Trade-off: it authenticates
the **caller** (a ServiceNow user via Basic/OAuth), so you'd ignore
`X-CF-Signature` and instead rely on network controls plus that user
credential. It's faster to build but weaker — prefer the verified path for any
internet-exposed endpoint.

---

## Idempotency, in one line

Everything keys off the native **`correlation_id`** field holding
`cf|{source}|{incidentId}`. The Look Up Records step makes inbound deliveries
update-not-duplicate, and the same field lets the outbound flow recover the CF
`incidentId` and `source`. It's the same contract the hosted samples use, so you
can mix and match (e.g. inbound via Flow Designer, outbound via a Logic App).

## Further reading (ServiceNow docs & community)

- REST step (IntegrationHub): `docs.servicenow.com` → *REST step* /
  *REST request action designer*
- [Outbound REST integration using Flow Designer](https://www.servicenow.com/community/developer-articles/outbound-rest-integration-using-flow-designer/ta-p/2308300)
- IntegrationHub **REST API Trigger**: `docs.servicenow.com` → *REST trigger*
- [Trigger a subflow from a Scripted REST API](https://www.servicenow.com/community/developer-forum/trigger-a-subflow-from-scripted-rest-api/m-p/2861302)
- [HMAC validation in ServiceNow with CertificateEncryption](https://www.servicenow.com/community/developer-advocate-blog/hmac-validation-in-servicenow-securing-webhook-integrations-with/ba-p/3382297)
- `FlowAPI` server API: `docs.servicenow.com` → *FlowAPI - Scoped, Global*
