# Sample 02 — ContraForce webhook → ConnectWise ticket (ASP.NET Core)

A minimal-API web app that:

1. Receives ContraForce webhooks on `POST /cf/webhooks`
2. Verifies `X-CF-Signature` against the **raw request body** (no JSON
   re-serialization — unlike the Logic App sample this is bit-exact)
3. Creates or updates a ConnectWise service ticket, keyed by
   `X-CF-Event-Id` via the ticket's `externalReference` field
4. Returns 200 on accept, 401 on bad signature, 400 on malformed payload

## Run locally

```bash
cd ContraForce.Samples.CwOutbound
cp appsettings.Example.json appsettings.Development.json
# edit appsettings.Development.json with your secrets (or use user-secrets)
dotnet run
```

By default listens on `http://localhost:5080`. Point the CF webhook at
`http://localhost:5080/cf/webhooks` (use an ngrok tunnel for public testing).

## Run in a container

```bash
cd ContraForce.Samples.CwOutbound
docker build -t cf-to-cw-outbound .
docker run --rm -p 5080:8080 \
  -e CF_WEBHOOK_SECRET='<secret>' \
  -e CW_BASE_URL='https://na.myconnectwise.net/v4_6_release/apis/3.0' \
  -e CW_COMPANY_ID='yourcompanyid' \
  -e CW_PUBLIC_KEY='<pub>' \
  -e CW_PRIVATE_KEY='<priv>' \
  -e CW_CLIENT_ID='00000000-0000-0000-0000-000000000000' \
  -e CW_DEFAULT_BOARD_ID='1' \
  -e CW_DEFAULT_COMPANY_IDENTIFIER='endcustomer' \
  cf-to-cw-outbound
```

## Deployment targets

- **Azure Container Apps** — recommended if you're already Azure-heavy; set
  secrets via Key Vault references.
- **App Service (Linux container)** — works the same; use Key Vault
  references for all `CW_*` / `CF_*` secrets.
- **Any Kubernetes** — plain container, health probe on `/healthz`.

## Mapping customization

Edit `ConnectWise/TicketMapper.cs`. All incident → ticket translation lives
there — keep the webhook endpoint itself thin.
