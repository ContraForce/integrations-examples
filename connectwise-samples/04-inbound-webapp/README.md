# Sample 04 — ConnectWise ticket changes → ContraForce incident (ASP.NET Core)

A minimal-API web app that:

1. Receives **ConnectWise Callbacks** on `POST /cw/callbacks` whenever a
   ticket changes (notes added, status changed, etc.)
2. Fetches the full ticket (and its notes) from ConnectWise to know what
   actually changed
3. Parses `externalReference` to recover the ContraForce `incidentId` and
   `source` — it expects tickets created by Sample 01 or Sample 02 and
   ignores anything without a `cf|…|…` reference
4. Calls the ContraForce v2 REST API using a **service account** (HTTP Basic
   with client id + client secret):
   - New CW ticket note → `POST /api/v2/workspaces/{workspaceId}/incidents/{incidentId}/comments`
   - CW ticket closed → `PUT /api/v2/workspaces/{workspaceId}/incidents/{incidentId}/status`

## Configure ConnectWise Callbacks

Ask a ConnectWise admin to register a callback pointing to this app's public
URL (System → Callbacks in newer versions). You want callbacks for the
`ServiceTicket` entity for at least `updated`. Each callback posts a JSON
body like:

```json
{
  "ID": 0,
  "Type": "ServiceTicket",
  "Action": "updated",
  "MemberID": "integrator",
  "ObjectID": 1234,
  "Entity": "…"
}
```

`ObjectID` is the ticket id — the receiver fetches the full ticket from CW
to figure out what actually changed (CW doesn't include a diff). For
production you'll want to persist the last-seen note id per ticket so you
don't re-post the same note twice after a CW update that doesn't add
anything new; this sample keeps that state in-memory.

## Run locally

```bash
cd ContraForce.Samples.CwInbound
cp appsettings.Example.json appsettings.Development.json
# fill in secrets — or use user-secrets
dotnet run
```

## Run in a container

```bash
docker build -t cf-to-cw-inbound .
docker run --rm -p 5081:8080 \
  -e CW_BASE_URL='…' \
  -e CW_COMPANY_ID='…' \
  -e CW_PUBLIC_KEY='…' \
  -e CW_PRIVATE_KEY='…' \
  -e CW_CLIENT_ID='…' \
  -e CF_API_BASE_URL='https://prod.platform.contraforce.com/api/v2' \
  -e CF_SERVICE_ACCOUNT_CLIENT_ID='…' \
  -e CF_SERVICE_ACCOUNT_CLIENT_SECRET='…' \
  -e CF_WORKSPACE_ID='…' \
  cf-to-cw-inbound
```

## Authenticating the callback

CW callbacks do not sign their payload. For a public endpoint, require one
of:

- A per-callback shared-secret header (set `CW_CALLBACK_SECRET` and require it
  on `X-Callback-Secret`) — the sample implements this; callbacks without
  the secret are rejected with 401.
- Network-level restriction (VNet/NSG) to your CW cloud's egress range.

## Known limitations / extend as needed

- Only forwards **new ticket notes** and **status changes**. Add more events
  by extending `ChangeDetector.cs`.
- Keeps the "last seen note id" per ticket in memory. For multi-instance
  production, back this with Redis, Cosmos, or a SQL table.
- Does not currently forward assignee changes (CW `owner` is a text field
  and mapping to a CF user requires a lookup table specific to your team).
