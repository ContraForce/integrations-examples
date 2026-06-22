# Sample 01 — ContraForce webhook → ServiceNow incident (Logic App)

A Consumption Logic App that:

1. Receives a ContraForce `incident.created.v1` webhook on an HTTPS trigger
2. Verifies `X-CF-Signature` via an Inline Code (JavaScript) action
3. Looks up an existing ServiceNow incident by `correlation_id` so repeat
   deliveries update rather than duplicate
4. Creates the incident, or appends a work note if it already exists

ServiceNow auth is HTTP Basic — the template builds the `Authorization` header
from the integration user's username + password, so there's no token-exchange
step.

## Deploy

```bash
cp azuredeploy.parameters.example.json azuredeploy.parameters.json
az deployment group create \
  --resource-group <your-rg> \
  --template-file azuredeploy.json \
  --parameters @azuredeploy.parameters.json
```

After deployment, grab the workflow's HTTPS trigger callback URL:

```bash
az rest --method POST --uri "https://management.azure.com$(
  az logic workflow show -g <your-rg> -n cf-to-snow-outbound --query id -o tsv
)/triggers/cf_webhook/listCallbackUrl?api-version=2019-05-01" \
  --query value -o tsv
```

Paste that URL into the ContraForce portal as the webhook destination.

## Signing secret

The webhook signing secret is pulled at runtime from Key Vault. The template
provisions a Managed Identity for the Logic App and grants it
`Key Vault Secrets User`. You must separately create the secret
`cf-webhook-secret` in the referenced Key Vault.

```bash
az keyvault secret set \
  --vault-name <kv-name> \
  --name cf-webhook-secret \
  --value "<paste secret from CF portal>"
```

## ServiceNow credentials

The integration user's username and password are passed as secure-string
parameters and written into the Logic App definition as a pre-encoded Basic
credential — not visible in the portal UI. Rotate them by redeploying. For
stronger secret hygiene, move them to Key Vault references matching the pattern
used for the CF webhook secret.

## Signature verification

The Inline Code action recomputes the HMAC and compares in constant time
before the flow proceeds. The same **raw-body caveat** as the HaloPSA and
ConnectWise Logic App samples applies — Logic Apps parses JSON triggers, and
`JSON.stringify` of the parsed object is not byte-identical to the original
request body. The template declares the trigger with an empty schema so the
body is exposed as a string, preserving the raw bytes for signing. For
high-throughput production use, verify in a small Azure Function in front of
the Logic App, or switch to Sample 02 (.NET) / Sample 05 (Python).

## Mapping

Edit the `compose_snow_incident` action in `azuredeploy.json` to fit your
instance. Default mapping is described in [`../README.md`](../README.md).
ServiceNow derives `priority` from the urgency × impact matrix, so adjust
`snowDefaultImpact` to shift the resulting priority.
