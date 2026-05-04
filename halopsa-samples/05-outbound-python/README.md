# Sample 05 — ContraForce webhook → HaloPSA ticket (Python / FastAPI)

A FastAPI app that:

1. Receives ContraForce webhooks on `POST /cf/webhooks`
2. Verifies `X-CF-Signature` against the **raw request body** — bit-exact
3. Acquires a Halo bearer token (OAuth2 client_credentials), cached and
   refreshed 60s before expiry
4. Creates or updates a Halo ticket, keyed by `cf|{source}|{incidentId}`
   stored in `thirdpartynumber` (or a configurable custom field)
5. Returns 200 on accept, 401 on bad signature, 400 on malformed payload

This is the Python equivalent of [Sample 02](../02-outbound-webapp/).

## Run locally

```bash
cd cf_to_halo_outbound
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

cp .env.example .env
# fill in secrets

uvicorn app.main:app --host 0.0.0.0 --port 5090 --reload
```

## Run in a container

```bash
docker build -t cf-to-halo-outbound-py .
docker run --rm -p 5090:8080 \
  -e CF_WEBHOOK_SECRET='<secret>' \
  -e HALO_AUTH_URL='https://yourname.halopsa.com/auth' \
  -e HALO_API_BASE_URL='https://yourname.halopsa.com/api' \
  -e HALO_CLIENT_ID='<guid>' \
  -e HALO_CLIENT_SECRET='<secret>' \
  -e HALO_DEFAULT_TICKETTYPE_ID='29' \
  -e HALO_DEFAULT_CLIENT_ID='1' \
  cf-to-halo-outbound-py
```

## External reference field

Set `HALO_EXTERNAL_REF_FIELD_ID` to use an "External Reference" custom field
instead of `thirdpartynumber`. The dedupe lookup will switch to the custom
field's id. Leave unset to use `thirdpartynumber`.

## Mapping customization

Edit `app/halo/mapping.py`. All incident → ticket translation lives there.
