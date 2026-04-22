# ContraForce ↔ ConnectWise Manage integration samples

Reference implementations showing how to integrate ContraForce incident events
with a ConnectWise Manage PSA instance in both directions.

```
                  ┌─────────────────────────────────────────────┐
                  │                                             │
                  │   CF Agent investigates/comments/closes     │
                  │                                             │
┌──────────────┐  │          ┌──────────────┐         ┌─────────┴────────┐
│  ContraForce │──┘          │ Sample 1 / 2 │         │                  │
│              │──webhook───▶│  (outbound)  │────────▶│                  │
│              │             └──────────────┘         │                  │
│              │                                      │   ConnectWise    │
│              │             ┌──────────────┐         │   Manage PSA     │
│              │◀──REST API──│ Sample 3 / 4 │◀────────│                  │
│              │             │  (inbound)   │ callback│                  │
└──────────────┘             └──────────────┘         └──────────────────┘
```

## Samples

| # | Direction | Technology | Use case |
|---|-----------|------------|----------|
| [01](./01-outbound-logic-app) | CF → CW | Azure Logic App (Consumption) | Low-code, fully managed, great for an MSP that wants to avoid hosting infrastructure |
| [02](./02-outbound-webapp) | CF → CW | ASP.NET Core 8 minimal API | More control, custom mapping logic, containerizable |
| [03](./03-inbound-logic-app) | CW → CF | Azure Logic App (Consumption) | Low-code inbound receiver driven by ConnectWise Callbacks |
| [04](./04-inbound-webapp) | CW → CF | ASP.NET Core 8 minimal API | Inbound receiver when you need richer mapping/enrichment |

You can deploy **any combination** — e.g. a Logic App for outbound and a web app
for inbound. They are independent.

## Prereqs

### 1. ContraForce webhook endpoint (outbound samples)

In the ContraForce portal, go to **Settings → Developers → Webhooks** and
create a webhook subscribed to `incident.created.v1`. You will need:

- **Destination URL** — the HTTPS endpoint of Sample 1 or Sample 2 after deployment
- **Signing secret** — ContraForce generates this; copy it somewhere safe. The samples use it to verify `X-CF-Signature`.

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

In the portal go to **Settings → Developers → Service Accounts** and create a
new service account with the scopes you need:

| Scope | Why |
|-------|-----|
| `incidents:read` | fetch incident details |
| `incidents:comments` | post comments back from CW ticket notes |
| `incidents:write` | change status / classification when the CW ticket closes |

Create a credential — you will get a **Client ID** (GUID) and a **Client
Secret**. Both are used as HTTP Basic credentials:

```
Authorization: Basic base64(<clientId>:<clientSecret>)
```

The API base URL follows this pattern:

```
https://<your-env>.platform.contraforce.com/api/v2
```

### 3. ConnectWise Manage API member (all samples)

Create a **dedicated API member** in ConnectWise (**System → Members → API
Members**) for this integration. Grant the member a role with at least:

- Service → Service Tickets: Add / Edit
- Service → Service Notes: Add / Edit

Generate a **Public/Private key pair** for that member.

You also need a **Client ID** issued by ConnectWise (one per integration — see
<https://developer.connectwise.com/ClientID>).

ConnectWise REST auth uses HTTP Basic:

```
Username: <companyId>+<publicKey>
Password: <privateKey>
Headers:  clientId: <your-integration-client-id>
          Accept:   application/vnd.connectwise.com+json; version=2020.1
```

And the base URL is your ConnectWise site, e.g.
`https://na.myconnectwise.net/v4_6_release/apis/3.0` — use the regional
hostname that matches your ConnectWise Manage Cloud instance.

## Configuration — never commit secrets

Every sample reads its configuration from **environment variables** or (for
Logic Apps) **ARM template parameters** that reference Azure Key Vault. The
example parameter files in each sample use placeholder values. Copy them to a
local file that is git-ignored before deploying.

| Setting | Description |
|---------|-------------|
| `CF_WEBHOOK_SECRET` | Webhook signing secret from the ContraForce portal |
| `CF_API_BASE_URL` | e.g. `https://prod.platform.contraforce.com/api/v2` |
| `CF_SERVICE_ACCOUNT_CLIENT_ID` | Service account client id (GUID) |
| `CF_SERVICE_ACCOUNT_CLIENT_SECRET` | Service account client secret |
| `CF_WORKSPACE_ID` | The workspace id the incidents belong to |
| `CW_BASE_URL` | e.g. `https://na.myconnectwise.net/v4_6_release/apis/3.0` |
| `CW_COMPANY_ID` | Your ConnectWise company id |
| `CW_PUBLIC_KEY` | Public key of the dedicated API member |
| `CW_PRIVATE_KEY` | Private key of the dedicated API member |
| `CW_CLIENT_ID` | Your registered ConnectWise integration client id |
| `CW_DEFAULT_BOARD_ID` | Numeric id of the board tickets should land on |
| `CW_DEFAULT_COMPANY_IDENTIFIER` | Company identifier (string) for the end customer |

For production deployments prefer Key Vault references or managed identity
credentials over raw environment variables.

## Idempotency

Webhook delivery is **at-least-once**. The samples store an external
reference — for CF → CW, the `X-CF-Event-Id` is added to the ticket's
`externalReference` so repeated deliveries of the same event update the same
ticket rather than creating duplicates. For CW → CF, the CW ticket id is used
as the identifying marker on the comment.

## Mapping

Default mapping applied by the samples:

| ContraForce | ConnectWise Manage |
|-------------|--------------------|
| `title` | `summary` (truncated to 100 chars) |
| `description` + alert/entity list | `initialDescription` |
| `severity` | `priority` (`High` → 1, `Medium` → 2, `Low`/`Informational` → 3) — configurable |
| `incidentNumber` | included in ticket `externalReference` + custom field |

Swap the mapping logic in `Map*` helpers to fit your board's schema.
