# ContraForce ↔ HaloPSA integration samples

Reference implementations showing how to integrate ContraForce incident events
with a HaloPSA instance in both directions.

```
                  ┌──────────────────────────────────────────┐
                  │                                          │
                  │   CF Agent investigates/comments/closes  │
                  │                                          │
┌──────────────┐  │          ┌──────────────┐         ┌──────┴───────────┐
│  ContraForce │──┘          │ Sample 1/2/5 │         │                  │
│              │──webhook───▶│  (outbound)  │────────▶│                  │
│              │             └──────────────┘         │                  │
│              │                                      │     HaloPSA      │
│              │             ┌──────────────┐         │                  │
│              │◀──REST API──│ Sample 3/4/6 │◀────────│                  │
│              │             │  (inbound)   │ webhook │                  │
└──────────────┘             └──────────────┘         └──────────────────┘
```

## Samples

| # | Direction | Technology | Use case |
|---|-----------|------------|----------|
| [01](./01-outbound-logic-app) | CF → Halo | Azure Logic App (Consumption) | Low-code, fully managed — drop into an Azure subscription you already own |
| [02](./02-outbound-webapp) | CF → Halo | ASP.NET Core 8 minimal API | Custom mapping logic, containerizable, raw-body HMAC verify |
| [03](./03-inbound-logic-app) | Halo → CF | Azure Logic App (Consumption) | Low-code receiver for Halo outbound webhooks |
| [04](./04-inbound-webapp) | Halo → CF | ASP.NET Core 8 minimal API | Inbound receiver when you need richer mapping/state |
| [05](./05-outbound-python) | CF → Halo | FastAPI (Python 3.12) | Same as 02, in Python |
| [06](./06-inbound-python) | Halo → CF | FastAPI (Python 3.12) | Same as 04, in Python |

You can deploy **any combination** — they're independent.

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
| `incidents:comments` | post comments back from Halo ticket actions |
| `incidents:write` | change status / classification when the Halo ticket closes |

You'll get a **Client ID** (GUID) and **Client Secret**. Used as HTTP Basic:

```
Authorization: Basic base64(<clientId>:<clientSecret>)
```

API base URL: `https://<your-env>.platform.contraforce.com/api/v2`.

### 3. HaloPSA API application (all samples)

In Halo, **Configuration → Integrations → HaloPSA API → New**:

1. Name it (e.g. `ContraForce Integration`).
2. **Authentication Method**: `Client ID and Secret (Services)`. This is the
   `client_credentials` variant — important. The plain "Client ID and Secret"
   choice is the password grant.
3. **Login Type**: `Agent`. Pick a dedicated service-account agent.
4. Save — Halo issues a `Client ID` and a `Client Secret`. The secret is
   shown **once**; copy it now.
5. On the **Permissions** tab, grant at minimum:
   - `read:tickets`, `edit:tickets`
   - `read:customers`
   - (optional) `read:agents`, `read:teams` for assignment lookups
6. Confirm the chosen agent's **Role** also grants the corresponding UI
   permissions — Halo applies the intersection of role + API scopes.

The samples authenticate using OAuth2 client_credentials:

```
POST {haloAuthUrl}/auth/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&
client_id=<guid>&
client_secret=<secret>&
scope=all
```

Token responses include `access_token`, `token_type=Bearer`, and `expires_in`.
Cache and refresh on expiry. Use `Authorization: Bearer <token>` on all API
calls.

> **Per-tenant values to verify:** `auth` URL (hosted Halo uses
> `https://<tenant>.halopsa.com/auth/token`; self-hosted has a separate
> Authorization Server URL — see your Halo admin), `tickettype_id`,
> `priority_id`, `status_id` for "Closed", and the numeric ids for any
> custom fields. Look them up in your Halo instance via `GET /api/TicketType`,
> `GET /api/Priority`, `GET /api/Status`.

## Configuration — never commit secrets

Each sample reads its configuration from environment variables (or, for Logic
Apps, ARM template parameters that reference Key Vault). Example parameter
files use placeholders. Copy them to a local, git-ignored file before
deploying.

| Setting | Description |
|---------|-------------|
| `CF_WEBHOOK_SECRET` | Webhook signing secret from the ContraForce portal |
| `CF_API_BASE_URL` | e.g. `https://prod.platform.contraforce.com/api/v2` |
| `CF_SERVICE_ACCOUNT_CLIENT_ID` | Service account client id (GUID) |
| `CF_SERVICE_ACCOUNT_CLIENT_SECRET` | Service account client secret |
| `CF_WORKSPACE_ID` | The workspace id incidents belong to |
| `HALO_AUTH_URL` | e.g. `https://yourname.halopsa.com/auth` |
| `HALO_API_BASE_URL` | e.g. `https://yourname.halopsa.com/api` |
| `HALO_CLIENT_ID` | Halo OAuth client id (GUID) |
| `HALO_CLIENT_SECRET` | Halo OAuth client secret |
| `HALO_TENANT` | Hosted multi-tenant tenant slug (omit for single-tenant / self-hosted) |
| `HALO_DEFAULT_TICKETTYPE_ID` | Numeric id of the ticket type to use |
| `HALO_DEFAULT_CLIENT_ID` | Halo client (customer) id the ticket should belong to |
| `HALO_CLOSED_STATUS_ID` | Numeric id of the status that represents "Closed" |
| `HALO_PRIVATE_NOTE_OUTCOME` | Outcome name or id used for inbound notes (e.g. `"Private Note"`) |
| `HALO_WEBHOOK_SECRET` | Shared secret expected on `X-Halo-Secret` from Halo webhooks |

For production deployments prefer Key Vault references or managed identity
credentials over raw environment variables.

## Idempotency

ContraForce webhook delivery is **at-least-once**. The samples store an
external reference on the Halo ticket so repeat deliveries update rather than
duplicate. Two storage options are supported, configurable per sample:

- **`thirdpartynumber`** (default) — Halo's built-in external-id field.
  Easy to filter on (`GET /api/Tickets?thirdpartynumber=…`).
- **Custom field** — set `HALO_EXTERNAL_REF_FIELD_ID` to the id of an
  "External Reference" custom field instead. Useful if `thirdpartynumber` is
  already used by another integration.

The reference value used by all samples is `cf|{source}|{incidentId}` — the
same format the ConnectWise samples use.

For Halo → CF, the Halo ticket id is used as the marker on the comment so
the same Halo update isn't echoed twice.

## Mapping

Default mapping applied by the samples:

| ContraForce | HaloPSA |
|-------------|---------|
| `title` | `summary` (truncated to 200 chars) |
| `description` + alert/entity list | `details_html` |
| `severity` | `priority_id` (`High` → 1, `Medium` → 2, `Low`/`Informational` → 3) — configurable |
| `incidentNumber`, `incidentId`, `source` | `thirdpartynumber` (or custom field) |

Edit the `Map*` helpers in each sample to fit your Halo workflow.

## Webhook auth from Halo

Halo's outbound webhooks do **not** sign payloads with HMAC; they support
arbitrary custom headers configured per webhook. The inbound samples expect a
shared secret on `X-Halo-Secret` and reject anything else with 401. Treat
this header value as a credential — rotate regularly and pin to a long random
value.

If you need stronger guarantees, fence the receiver at the network layer
(VNet / NSG / WAF allow-listing your Halo egress).
