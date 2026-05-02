# Sample 04 — HaloPSA ticket changes → ContraForce incident (ASP.NET Core)

A minimal-API web app that:

1. Receives **HaloPSA outbound webhooks** on `POST /halo/webhooks` whenever a
   ticket / action is created or updated
2. Rejects webhooks missing the shared `X-Halo-Secret` header
3. Reads the ticket id from the payload and pulls the full ticket plus its
   recent actions back from Halo (Halo doesn't include a diff)
4. Parses the external reference (`thirdpartynumber`, or a configured custom
   field id) to recover the ContraForce `incidentId` and `source`. Tickets
   without a `cf|…|…` reference are ignored.
5. Calls the ContraForce v2 REST API using a **service account** (HTTP Basic
   with client id + client secret):
   - New Halo public action → `POST /api/v2/workspaces/{workspaceId}/incidents/{incidentId}/comments`
   - Halo ticket closed → `PUT /api/v2/workspaces/{workspaceId}/incidents/{incidentId}/status`

## Configure HaloPSA outbound webhooks

In Halo, **Configuration → Integrations → Webhooks → New**:

- **URL**: this app's public URL + `/halo/webhooks`.
- **Method**: POST.
- **Triggers**: at minimum `New Ticket Logged` and `Ticket Updated`. (Action
  events are typically routed via "Ticket Updated" — verify per-tenant.)
- **Headers**: add `X-Halo-Secret` with a strong random value, and configure
  the same value as `Halo:WebhookSecret` for this app. Halo does not sign
  payloads with HMAC, so the shared header is the auth boundary.
- **Body**: leave Halo's default (the ticket / action object). The receiver
  pulls the full ticket back from `/api/Tickets/{id}` regardless.

## Run locally

```bash
cd ContraForce.Samples.HaloInbound
cp appsettings.Example.json appsettings.Development.json
# fill in secrets
dotnet run
```

## Run in a container

```bash
docker build -t halo-to-cf-inbound .
docker run --rm -p 5091:8080 \
  -e HALO_AUTH_URL='https://yourname.halopsa.com/auth' \
  -e HALO_API_BASE_URL='https://yourname.halopsa.com/api' \
  -e HALO_CLIENT_ID='…' \
  -e HALO_CLIENT_SECRET='…' \
  -e HALO_WEBHOOK_SECRET='…' \
  -e HALO_CLOSED_STATUS_ID='9' \
  -e CF_API_BASE_URL='https://prod.platform.contraforce.com/api/v2' \
  -e CF_SERVICE_ACCOUNT_CLIENT_ID='…' \
  -e CF_SERVICE_ACCOUNT_CLIENT_SECRET='…' \
  -e CF_WORKSPACE_ID='…' \
  halo-to-cf-inbound
```

## Authenticating the webhook

Halo webhooks do not sign their payload. For a public endpoint, require one
of:

- A per-webhook shared-secret header (set `HALO_WEBHOOK_SECRET` and require it
  on `X-Halo-Secret`) — the sample implements this; webhooks without the
  secret are rejected with 401.
- Network-level restriction (VNet/NSG/WAF) to your Halo cloud's egress range.

## What we forward and what we don't

- **Forwards**: new public-facing actions (notes, replies) and the close
  transition.
- **Ignores**: private/internal-only actions, actions authored by the
  integration's own agent (to avoid echo loops), and ticket assignment changes
  (Halo agents don't map cleanly to CF users without a lookup table).
- **State**: tracks "last forwarded action id" and "closed?" per ticket
  in-memory so duplicate webhooks don't post duplicate comments. Replace
  `ChangeTracker` with Redis / Cosmos / SQL for multi-instance deployments.

## What "Closed" means in Halo

Halo's closed status id is tenant-defined. Set `Halo:ClosedStatusId` to the
numeric id of the status you want to treat as the close trigger; you can
look this up via `GET /api/Status` against your Halo instance.
