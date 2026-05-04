# Sample 05 — ContraForce webhook → ConnectWise ticket (Python / FastAPI)

A small FastAPI app that:

1. Receives ContraForce webhooks on `POST /cf/webhooks`
2. Verifies `X-CF-Signature` against the **raw request body** — no JSON
   re-serialization, so the bytes used to sign and the bytes received are
   identical
3. Creates or updates a ConnectWise service ticket, keyed by
   `X-CF-Event-Id` via the ticket's `externalReference` field
4. Returns 200 on accept, 401 on bad signature, 400 on malformed payload

This is the Python equivalent of [Sample 02](../02-outbound-webapp/) — pick
whichever language you'd rather operate.

## Run locally

```bash
cd cf_to_cw_outbound
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

cp .env.example .env
# edit .env with your secrets

uvicorn app.main:app --host 0.0.0.0 --port 5080 --reload
```

Listens on `http://localhost:5080`. Point the CF webhook at
`http://localhost:5080/cf/webhooks` (use an ngrok tunnel for public testing).

## Run in a container

```bash
cd cf_to_cw_outbound
docker build -t cf-to-cw-outbound-py .
docker run --rm -p 5080:8080 \
  -e CF_WEBHOOK_SECRET='<secret>' \
  -e CW_BASE_URL='https://na.myconnectwise.net/v4_6_release/apis/3.0' \
  -e CW_COMPANY_ID='yourcompanyid' \
  -e CW_PUBLIC_KEY='<pub>' \
  -e CW_PRIVATE_KEY='<priv>' \
  -e CW_CLIENT_ID='00000000-0000-0000-0000-000000000000' \
  -e CW_DEFAULT_BOARD_ID='1' \
  -e CW_DEFAULT_COMPANY_IDENTIFIER='endcustomer' \
  cf-to-cw-outbound-py
```

## Deployment targets

- **Azure Container Apps** — drop in the image, set secrets via Key Vault
  references.
- **Azure App Service (Linux container)** — same flow.
- **Azure Functions (Python, custom container or Flex Consumption)** — works
  if you adapt `app/main.py` to a Function trigger; for plain HTTP a
  containerized FastAPI app is simpler.
- **Any Kubernetes** — plain container, health probe on `/healthz`.

## Mapping customization

Edit `cf_to_cw_outbound/app/connectwise/mapping.py`. All incident → ticket
translation lives there — keep the webhook endpoint thin.
