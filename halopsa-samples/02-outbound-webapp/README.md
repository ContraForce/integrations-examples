# Sample 02 — ContraForce webhook → HaloPSA ticket (ASP.NET Core)

A minimal-API web app that:

1. Receives ContraForce webhooks on `POST /cf/webhooks`
2. Verifies `X-CF-Signature` against the **raw request body** (no JSON
   re-serialization — bit-exact)
3. Creates or updates a HaloPSA ticket, keyed by `cf|{source}|{incidentId}`
   stored in `thirdpartynumber` (or a configurable custom field)
4. Returns 200 on accept, 401 on bad signature, 400 on malformed payload

Halo auth uses OAuth2 `client_credentials` — the app caches the bearer token
in memory and refreshes it 60s before expiry.

## Run locally

```bash
cd ContraForce.Samples.HaloOutbound
cp appsettings.Example.json appsettings.Development.json
# fill in secrets, or use user-secrets
dotnet run
```

By default listens on `http://localhost:5090`. Point the CF webhook at
`http://localhost:5090/cf/webhooks` (use ngrok for public testing).

## Run in a container

```bash
cd ContraForce.Samples.HaloOutbound
docker build -t cf-to-halo-outbound .
docker run --rm -p 5090:8080 \
  -e CF_WEBHOOK_SECRET='<secret>' \
  -e HALO_AUTH_URL='https://yourname.halopsa.com/auth' \
  -e HALO_API_BASE_URL='https://yourname.halopsa.com/api' \
  -e HALO_CLIENT_ID='<guid>' \
  -e HALO_CLIENT_SECRET='<secret>' \
  -e HALO_DEFAULT_TICKETTYPE_ID='29' \
  -e HALO_DEFAULT_CLIENT_ID='1' \
  cf-to-halo-outbound
```

## Deployment targets

- **Azure Container Apps** — recommended; secrets via Key Vault references.
- **App Service (Linux container)** — same pattern.
- **Any Kubernetes** — plain container, health probe on `/healthz`.

## Mapping customization

Edit `Halo/TicketMapper.cs`. All incident → ticket translation lives there.

## External reference field

Set `Halo:ExternalRefFieldId` to use an "External Reference" **custom field**
instead of `thirdpartynumber`. The dedupe lookup will switch to the custom
field's id. If both are set, the custom field takes precedence.
