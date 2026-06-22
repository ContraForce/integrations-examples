# Sample 04 — ServiceNow incident changes → ContraForce incident (ASP.NET Core)

A minimal-API web app that:

1. Receives **ServiceNow Business Rule callbacks** on `POST /snow/callbacks`
   whenever a linked incident is updated
2. Rejects callbacks missing the shared `X-SNow-Secret` header
3. Reads the `sys_id` from the payload and pulls the full incident plus its
   journal (comments / work notes) back from the Table API (ServiceNow doesn't
   include a diff)
4. Parses the `correlation_id` to recover the ContraForce `incidentId` and
   `source`. Incidents without a `cf|…|…` reference are ignored.
5. Calls the ContraForce v2 REST API using a **service account** (HTTP Basic
   with client id + client secret):
   - New ServiceNow comment → `POST /api/v2/workspaces/{workspaceId}/incidents/{incidentId}/comments`
   - ServiceNow incident resolved/closed → `PUT /api/v2/workspaces/{workspaceId}/incidents/{incidentId}/status`

## Configure the ServiceNow callback

ServiceNow has no generic outbound webhook UI; use a **Business Rule** (or Flow
Designer) to POST to this app. In your instance, **System Definition →
Business Rules → New**:

- **Table**: `Incident [incident]`
- **When**: `after`, checked for **Update** (and **Insert** if you want create
  echoes)
- **Advanced → Script**: use a `RESTMessageV2` (or `sn_ws.RESTMessageV2`) to
  POST `{ "sys_id": current.sys_id.toString(), "number": current.number.toString() }`
  to this app's public URL + `/snow/callbacks`, adding the header
  `X-SNow-Secret` with a strong random value. ServiceNow does not sign
  outbound messages, so the shared header is the auth boundary.

> Tip: gate the Business Rule with a **Condition** of
> `correlation_id STARTSWITH cf|` so it only fires for ContraForce-linked
> incidents and you don't POST on every incident in the instance.

## Run locally

```bash
cd ContraForce.Samples.SnowInbound
cp appsettings.Example.json appsettings.Development.json
# fill in secrets
dotnet run
```

## Run in a container

```bash
docker build -t snow-to-cf-inbound .
docker run --rm -p 5091:8080 \
  -e SNOW_INSTANCE_URL='https://dev12345.service-now.com' \
  -e SNOW_USERNAME='contraforce.integration' \
  -e SNOW_PASSWORD='…' \
  -e SNOW_CALLBACK_SECRET='…' \
  -e SNOW_RESOLVED_STATE='6' \
  -e SNOW_CLOSED_STATE='7' \
  -e CF_API_BASE_URL='https://prod.platform.contraforce.com/api/v2' \
  -e CF_SERVICE_ACCOUNT_CLIENT_ID='…' \
  -e CF_SERVICE_ACCOUNT_CLIENT_SECRET='…' \
  -e CF_WORKSPACE_ID='…' \
  snow-to-cf-inbound
```

## What we forward and what we don't

- **Forwards**: new customer-visible `comments` and the resolve/close
  transition.
- **Ignores**: internal `work_notes` (unless `ServiceNow:ForwardWorkNotes` is
  true), journal entries authored by the integration's own user
  (`ServiceNow:IntegrationUser`, to avoid echo loops), and assignment changes
  (ServiceNow users don't map to CF users without a lookup table).
- **State**: tracks the latest forwarded journal `sys_created_on` and a
  "closed?" flag per incident in-memory so duplicate callbacks don't
  double-post. Replace `ChangeTracker` with Redis / Cosmos / SQL for
  multi-instance deployments.

## What "Closed" means in ServiceNow

The `state` field is numeric and tenant-customizable. The defaults treat
`6` (Resolved) and `7` (Closed) as close triggers — set `ServiceNow:ResolvedState`
and `ServiceNow:ClosedState` to match your instance's incident state model.
Closing a CF incident that's already closed is a no-op, so mirroring on Resolve
and again on Close is safe.
