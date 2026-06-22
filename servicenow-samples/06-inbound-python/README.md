# Sample 06 — ServiceNow incident changes → ContraForce incident (Python / FastAPI)

A FastAPI app that:

1. Receives ServiceNow Business Rule callbacks on `POST /snow/callbacks`
2. Rejects callbacks missing the shared `X-SNow-Secret` header
3. Pulls the full incident and its journal (comments / work notes) back from
   the Table API (the Business Rule payload only carries the `sys_id`)
4. Parses the `correlation_id` to recover the ContraForce `incidentId` and
   `source`. Incidents without a `cf|…|…` reference are ignored.
5. Forwards new comments and mirrors a ServiceNow resolve/close back to
   ContraForce via the v2 REST API (service-account auth)

This is the Python equivalent of [Sample 04](../04-inbound-webapp/).

## Configure the ServiceNow callback

Same as Sample 04 — a Business Rule (or Flow Designer outbound REST message)
on the `incident` table that POSTs `{ "sys_id": "…", "number": "…" }` with a
shared `X-SNow-Secret` header. Gate it with a condition of
`correlation_id STARTSWITH cf|` so it only fires for CF-linked incidents.

## Run locally

```bash
cd snow_to_cf_inbound
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

cp .env.example .env
# fill in secrets

uvicorn app.main:app --host 0.0.0.0 --port 5091 --reload
```

## Run in a container

```bash
docker build -t snow-to-cf-inbound-py .
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
  snow-to-cf-inbound-py
```

## What we forward and what we don't

- **Forwards**: new customer-visible `comments` and the resolve/close
  transition.
- **Ignores**: internal `work_notes` (unless `SNOW_FORWARD_WORK_NOTES=true`),
  journal entries authored by the integration's own user
  (`SNOW_INTEGRATION_USER`), and assignment changes.
- **State**: tracks the latest forwarded journal `sys_created_on` and a
  "closed?" flag per incident in-memory so duplicate callbacks don't
  double-post. Replace `ChangeTracker` with Redis / Cosmos / SQL for
  multi-instance deployments.
