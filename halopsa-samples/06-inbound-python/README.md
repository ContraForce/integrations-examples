# Sample 06 — HaloPSA ticket changes → ContraForce incident (Python / FastAPI)

A FastAPI app that:

1. Receives Halo outbound webhooks on `POST /halo/webhooks`
2. Rejects callbacks missing the shared `X-Halo-Secret` header
3. Pulls the full ticket and its actions back from Halo (Halo's webhook
   payload varies by trigger and tenant config — re-fetch to get a stable
   shape)
4. Parses the external reference (`thirdpartynumber`, or a configured custom
   field id) to recover the ContraForce `incidentId` and `source`. Tickets
   without a `cf|…|…` reference are ignored.
5. Forwards new public actions as comments and mirrors a Halo close back to
   ContraForce via the v2 REST API (service-account auth)

This is the Python equivalent of [Sample 04](../04-inbound-webapp/).

## Configure HaloPSA outbound webhooks

Same as Sample 04 — register a webhook for ticket / action events with a
shared `X-Halo-Secret` header. Halo doesn't sign payloads; the shared header
is the auth boundary.

## Run locally

```bash
cd halo_to_cf_inbound
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

cp .env.example .env
# fill in secrets

uvicorn app.main:app --host 0.0.0.0 --port 5091 --reload
```

## Run in a container

```bash
docker build -t halo-to-cf-inbound-py .
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
  halo-to-cf-inbound-py
```

## What we forward and what we don't

- **Forwards**: new public-facing actions (notes, replies) and the close
  transition.
- **Ignores**: actions where `hiddenfromuser=true`, actions authored by the
  integration's own agent (`HALO_INTEGRATION_AGENT_ID`), and assignment
  changes (Halo agents don't map to CF users without a lookup table).
- **State**: tracks "last forwarded action id" and "closed?" per ticket
  in-memory so duplicate webhooks don't double-post. Replace `ChangeTracker`
  with Redis / Cosmos / SQL for multi-instance deployments.
