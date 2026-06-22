# ContraForce ↔ ServiceNow integration samples

Reference implementations showing how to integrate ContraForce incident events
with a ServiceNow instance in both directions.

```
                  ┌──────────────────────────────────────────┐
                  │                                          │
                  │   CF Agent investigates/comments/closes  │
                  │                                          │
┌──────────────┐  │          ┌──────────────┐         ┌──────┴───────────┐
│  ContraForce │──┘          │ Sample 1/2/5 │         │                  │
│              │──webhook───▶│  (outbound)  │────────▶│                  │
│              │             └──────────────┘         │                  │
│              │                                      │    ServiceNow    │
│              │             ┌──────────────┐         │                  │
│              │◀──REST API──│ Sample 3/4/6 │◀────────│                  │
│              │             │  (inbound)   │ callback│                  │
└──────────────┘             └──────────────┘         └──────────────────┘
```

## Samples

| # | Direction | Technology | Use case |
|---|-----------|------------|----------|
| [01](./01-outbound-logic-app) | CF → ServiceNow | Azure Logic App (Consumption) | Low-code, fully managed — drop into an Azure subscription you already own |
| [02](./02-outbound-webapp) | CF → ServiceNow | ASP.NET Core 8 minimal API | Custom mapping logic, containerizable, raw-body HMAC verify |
| [03](./03-inbound-logic-app) | ServiceNow → CF | Azure Logic App (Consumption) | Low-code receiver for a ServiceNow Business Rule callback |
| [04](./04-inbound-webapp) | ServiceNow → CF | ASP.NET Core 8 minimal API | Inbound receiver when you need richer mapping/state |
| [05](./05-outbound-python) | CF → ServiceNow | FastAPI (Python 3.12) | Same as 02, in Python |
| [06](./06-inbound-python) | ServiceNow → CF | FastAPI (Python 3.12) | Same as 04, in Python |
| [07](./07-flow-designer) | Both | **Native** ServiceNow Flow Designer + IntegrationHub | No host outside ServiceNow — build the integration in-platform (REST step out, Scripted REST API + subflow in) |

You can deploy **any combination** — they're independent. Sample 07 is a
no-external-host alternative: it builds the same bi-directional flow entirely
inside ServiceNow using Flow Designer instead of a Logic App / web app / Python
service.

## Prereqs

### 1. ContraForce webhook endpoint (outbound samples)

In the ContraForce portal, **Settings → Developers → Webhooks**, create a
webhook subscribed to `incident.created.v1`. You will need:

- **Destination URL** — the HTTPS endpoint of Sample 1 / 2 / 5 once deployed
- **Signing secret** — ContraForce generates this; copy it. The samples use
  it to verify `X-CF-Signature`.

ContraForce sends each event as an HTTP POST with:

| Header | Purpose |
|--------|---------|
| `X-CF-Signature` | `sha256=<hex>` — `HMAC-SHA256(secret, "{timestamp}.{raw_body}")` |
| `X-CF-Timestamp` | Unix seconds used in the signature payload |
| `X-CF-Event-Id` | Deterministic event id (for idempotency) |
| `X-CF-Schema` | Event type, e.g. `incident.created.v1` |
| `X-CF-Test` | `true` for test deliveries from the portal |

Body shape (camelCase):

```jsonc
{
  "type": "incident.created.v1",
  "timestamp": "2026-04-22T15:00:00Z",
  "isTest": false,
  "occurredAt": "2026-04-22T14:55:00Z",
  "data": {
    "accountId": "…",
    "accountName": "Example MSP SOC",
    "incidentId": "sentinel-incident-id-or-guid",
    "incidentNumber": 1234,
    "title": "Suspicious sign-in activity",
    "description": "…",
    "severity": "High",                   // Informational | Low | Medium | High
    "source": "sentinel",                 // sentinel | defenderxdr | crowdstrike | sentinelone
    "sourceDisplayName": "Microsoft Sentinel",
    "owner": { "displayName": "Alex Analyst", "email": "alex@example.com" },
    "createdAt": "2026-04-22T14:55:00Z",
    "lastActivityAt": "2026-04-22T14:55:00Z",
    "occurredAt": "2026-04-22T14:55:00Z",
    "alertProductNames": ["Microsoft Sentinel"],
    "alerts": [
      { "title": "Impossible travel", "severity": "High", "productName": "Microsoft Sentinel", "vendorName": "Microsoft" }
    ],
    "entities": [
      { "type": "User", "displayName": "alex@example.com" },
      { "type": "IP",   "displayName": "203.0.113.42" }
    ]
  }
}
```

### 2. ContraForce service account (inbound samples)

In the portal, **Settings → Developers → Service Accounts**, create a service
account with these scopes:

| Scope | Why |
|-------|-----|
| `incidents:read` | fetch incident details |
| `incidents:comments` | post comments back from ServiceNow journal entries |
| `incidents:write` | change status / classification when the ServiceNow incident closes |

You'll get a **Client ID** (GUID) and **Client Secret**. Used as HTTP Basic:

```
Authorization: Basic base64(<clientId>:<clientSecret>)
```

API base URL: `https://<your-env>.platform.contraforce.com/api/v2`.

### 3. ServiceNow integration user (all samples)

Create a **dedicated integration user** in ServiceNow (**User Administration →
Users → New**). Mark it *Web service access only* and give it a role that can
read, create, and update the `incident` table plus read `sys_journal_field`:

- The out-of-box `itil` role covers incident read/create/write.
- Reading the journal (comments / work notes) needs read access to
  `sys_journal_field` — `itil` has this on incidents the user can see.
- If you prefer least privilege, build a custom role with table ACLs for
  `incident` (read/create/write) and `sys_journal_field` (read).

The samples authenticate to the **Table API** with HTTP Basic:

```
GET/POST/PATCH https://<instance>.service-now.com/api/now/table/incident
Authorization: Basic base64(<username>:<password>)
Accept: application/json
```

> **OAuth instead of Basic.** ServiceNow also supports OAuth 2.0. To use it,
> register an OAuth application registry endpoint, obtain a bearer token, and
> set `Authorization: Bearer <token>` instead of Basic. The HaloPSA samples in
> this repo show the token-provider pattern you'd drop in.

> **Per-instance values to verify:** the incident `state` numbers that mean
> "Resolved" and "Closed" (defaults `6` / `7`), the `impact` value to default
> to, and any assignment-group / caller `sys_id`s. ServiceNow derives
> `priority` from the urgency × impact matrix — the samples set `urgency` from
> the incident severity.

## Configuration — never commit secrets

Each sample reads its configuration from environment variables (or, for Logic
Apps, ARM template parameters). Example parameter files use placeholders. Copy
them to a local, git-ignored file before deploying.

| Setting | Description |
|---------|-------------|
| `CF_WEBHOOK_SECRET` | Webhook signing secret from the ContraForce portal |
| `CF_API_BASE_URL` | e.g. `https://prod.platform.contraforce.com/api/v2` |
| `CF_SERVICE_ACCOUNT_CLIENT_ID` | Service account client id (GUID) |
| `CF_SERVICE_ACCOUNT_CLIENT_SECRET` | Service account client secret |
| `CF_WORKSPACE_ID` | The workspace id incidents belong to |
| `SNOW_INSTANCE_URL` | e.g. `https://dev12345.service-now.com` |
| `SNOW_USERNAME` | Username of the dedicated integration user |
| `SNOW_PASSWORD` | Password of the integration user |
| `SNOW_DEFAULT_IMPACT` | Default `impact` for new incidents (1 = High, 2 = Medium, 3 = Low) |
| `SNOW_RESOLVED_STATE` | Numeric `state` that represents "Resolved" (default 6) |
| `SNOW_CLOSED_STATE` | Numeric `state` that represents "Closed" (default 7) |
| `SNOW_CALLBACK_SECRET` | Shared secret expected on `X-SNow-Secret` from Business Rule callbacks |
| `SNOW_FORWARD_WORK_NOTES` | Forward internal work notes in addition to comments (inbound) |
| `SNOW_INTEGRATION_USER` | Username journal entries are skipped for, to avoid echo loops |

For production deployments prefer Key Vault references or managed identity
credentials over raw environment variables.

## Idempotency

ContraForce webhook delivery is **at-least-once**. The samples store the
external reference `cf|{source}|{incidentId}` in ServiceNow's built-in
`correlation_id` field, so repeat deliveries update rather than duplicate. The
field is indexed and easy to filter on
(`GET /api/now/table/incident?sysparm_query=correlation_id=…`). The reference
format is the same one the ConnectWise and HaloPSA samples use.

For ServiceNow → CF, the receiver tracks the latest forwarded journal
`sys_created_on` per incident so the same comment isn't echoed twice.

## Mapping

Default mapping applied by the samples:

| ContraForce | ServiceNow |
|-------------|------------|
| `title` | `short_description` (truncated to 160 chars) |
| `description` + alert/entity list | `description` |
| `severity` | `urgency` (`High` → 1, `Medium` → 2, `Low`/`Informational` → 3); `impact` from default → `priority` |
| `incidentNumber`, `incidentId`, `source` | `correlation_id` (`cf|{source}|{incidentId}`) |

Edit the `*Mapper` / `mapping` helpers in each sample to fit your instance.

## Callback auth from ServiceNow

ServiceNow Business Rules (and Flow Designer outbound REST messages) do **not**
sign payloads; they send whatever headers you script. The inbound samples
expect a shared secret on `X-SNow-Secret` and reject anything else with 401.
Treat this header value as a credential — rotate it regularly and pin to a long
random value. For stronger guarantees, fence the receiver at the network layer
(VNet / NSG / WAF allow-listing your ServiceNow egress).
