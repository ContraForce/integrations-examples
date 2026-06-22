# Sample 05 — ContraForce webhook → ServiceNow incident (Python / FastAPI)

A FastAPI app that:

1. Receives ContraForce webhooks on `POST /cf/webhooks`
2. Verifies `X-CF-Signature` against the **raw request body** — bit-exact
3. Authenticates to the ServiceNow Table API with HTTP Basic (a dedicated
   integration user)
4. Creates or updates a ServiceNow incident, keyed by `cf|{source}|{incidentId}`
   stored in the native `correlation_id` field
5. Returns 200 on accept, 401 on bad signature, 400 on malformed payload

This is the Python equivalent of [Sample 02](../02-outbound-webapp/).

## Run locally

```bash
cd cf_to_snow_outbound
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

cp .env.example .env
# fill in secrets

uvicorn app.main:app --host 0.0.0.0 --port 5090 --reload
```

## Run in a container

```bash
docker build -t cf-to-snow-outbound-py .
docker run --rm -p 5090:8080 \
  -e CF_WEBHOOK_SECRET='<secret>' \
  -e SNOW_INSTANCE_URL='https://dev12345.service-now.com' \
  -e SNOW_USERNAME='contraforce.integration' \
  -e SNOW_PASSWORD='<password>' \
  -e SNOW_DEFAULT_IMPACT='2' \
  cf-to-snow-outbound-py
```

## Mapping customization

Edit `app/servicenow/mapping.py`. All incident → ServiceNow translation lives
there. `priority` is derived by ServiceNow from the urgency × impact matrix, so
set `SNOW_DEFAULT_IMPACT` to shift the resulting priority.

## OAuth instead of Basic

The Table API also accepts OAuth 2.0 bearer tokens. To switch, add a token
provider like the HaloPSA sample's (`app/halo/token_provider.py`) and set the
`Authorization` header to `Bearer <token>` instead of Basic — see the parent
README.
