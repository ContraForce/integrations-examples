# Sample 06 — ConnectWise ticket changes → ContraForce incident (Python / FastAPI)

A FastAPI app that:

1. Receives **ConnectWise Callbacks** on `POST /cw/callbacks`
2. Rejects callbacks missing the shared `X-Callback-Secret` header
3. Fetches the full ticket and its notes from ConnectWise to know what
   actually changed (CW callbacks don't include a diff)
4. Parses `externalReference` (`cf|{source}|{incidentId}`) to recover the
   ContraForce incident; ignores anything without a `cf|…|…` reference
5. Forwards new ticket notes as comments and mirrors a CW close back to
   ContraForce via the v2 REST API authenticated with a service account

This is the Python equivalent of [Sample 04](../04-inbound-webapp/).

## Configure ConnectWise Callbacks

Same as Sample 04 — register a callback for the `ServiceTicket` entity for at
least `updated`. Each callback posts a JSON body like:

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

The receiver fetches the full ticket from CW (CW doesn't include a diff).
For production, persist the last-seen note id per ticket somewhere durable
(Redis / Cosmos / SQL) — this sample keeps it in memory.

## Run locally

```bash
cd cw_to_cf_inbound
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

cp .env.example .env
# fill in secrets

uvicorn app.main:app --host 0.0.0.0 --port 5081 --reload
```

## Run in a container

```bash
docker build -t cf-to-cw-inbound-py .
docker run --rm -p 5081:8080 \
  -e CW_BASE_URL='…' \
  -e CW_COMPANY_ID='…' \
  -e CW_PUBLIC_KEY='…' \
  -e CW_PRIVATE_KEY='…' \
  -e CW_CLIENT_ID='…' \
  -e CW_CALLBACK_SECRET='…' \
  -e CF_API_BASE_URL='https://prod.platform.contraforce.com/api/v2' \
  -e CF_SERVICE_ACCOUNT_CLIENT_ID='…' \
  -e CF_SERVICE_ACCOUNT_CLIENT_SECRET='…' \
  -e CF_WORKSPACE_ID='…' \
  cf-to-cw-inbound-py
```

## Authenticating the callback

CW callbacks do not sign their payload. For a public endpoint:

- Set `CW_CALLBACK_SECRET` to a strong random string and configure the CW
  callback to send it on `X-Callback-Secret`. The sample rejects callbacks
  without the secret with 401.
- Or restrict ingress at the network layer (VNet / NSG / WAF) to your CW
  cloud's egress range.

## Known limitations / extend as needed

- Only forwards **new ticket notes** and **status changes**. Add more events
  by extending `app/callbacks/change_tracker.py`.
- Keeps "last seen note id" per ticket in memory. For multi-instance
  deployments, swap in Redis / Cosmos / SQL.
- Does not currently forward assignee changes (CW `owner` is a text field
  and mapping to a CF user requires a lookup table specific to your team).
