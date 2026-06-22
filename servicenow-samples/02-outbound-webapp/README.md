# Sample 02 — ContraForce webhook → ServiceNow incident (ASP.NET Core)

A minimal-API web app that:

1. Receives ContraForce webhooks on `POST /cf/webhooks`
2. Verifies `X-CF-Signature` against the **raw request body** (no JSON
   re-serialization — bit-exact)
3. Creates or updates a ServiceNow incident, keyed by `cf|{source}|{incidentId}`
   stored in the native `correlation_id` field
4. Returns 200 on accept, 401 on bad signature, 400 on malformed payload

ServiceNow auth uses HTTP Basic (a dedicated integration user) against the
Table API. To use OAuth 2.0 instead, drop in a token provider like the HaloPSA
sample's and swap the `Authorization` header — see the parent README.

## Run locally

```bash
cd ContraForce.Samples.SnowOutbound
cp appsettings.Example.json appsettings.Development.json
# fill in secrets, or use user-secrets
dotnet run
```

By default listens on `http://localhost:5090`. Point the CF webhook at
`http://localhost:5090/cf/webhooks` (use ngrok for public testing).

## Run in a container

```bash
cd ContraForce.Samples.SnowOutbound
docker build -t cf-to-snow-outbound .
docker run --rm -p 5090:8080 \
  -e CF_WEBHOOK_SECRET='<secret>' \
  -e SNOW_INSTANCE_URL='https://dev12345.service-now.com' \
  -e SNOW_USERNAME='contraforce.integration' \
  -e SNOW_PASSWORD='<password>' \
  -e SNOW_DEFAULT_IMPACT='2' \
  cf-to-snow-outbound
```

## Deployment targets

- **Azure Container Apps** — recommended; secrets via Key Vault references.
- **App Service (Linux container)** — same pattern.
- **Any Kubernetes** — plain container, health probe on `/healthz`.

## Mapping customization

Edit `ServiceNow/IncidentMapper.cs`. All incident → ServiceNow translation
lives there: `short_description`, `description`, and the severity → `urgency`
mapping. `priority` is derived by ServiceNow from the urgency × impact matrix,
so set `ServiceNow:DefaultImpact` to shift the resulting priority.

## Correlation id

The dedupe key `cf|{source}|{incidentId}` is stored in the built-in
`correlation_id` field, which is indexed and easy to filter on
(`GET /api/now/table/incident?sysparm_query=correlation_id=…`). The inbound
samples read it back to map a ServiceNow incident to its CF incident.
