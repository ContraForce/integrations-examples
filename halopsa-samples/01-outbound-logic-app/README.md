# Sample 01 — ContraForce webhook → HaloPSA ticket (Logic App)

A Consumption Logic App that:

1. Receives a ContraForce `incident.created.v1` webhook on an HTTPS trigger
2. Verifies `X-CF-Signature` via an Inline Code (JavaScript) action
3. Acquires a HaloPSA bearer token via OAuth2 client_credentials
4. Looks up an existing Halo ticket by `thirdpartynumber` so repeat deliveries
   update rather than duplicate
5. Creates or updates a HaloPSA ticket with the mapped fields

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
  az logic workflow show -g <your-rg> -n cf-to-halo-outbound --query id -o tsv
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

## HaloPSA credentials

The template expects Halo OAuth credentials as secure parameters. They're
written into the Logic App definition as secure string parameters — not
visible in the portal UI. Rotate them by redeploying.

For stronger secret hygiene, replace the `haloClientSecret` parameter with a
Key Vault reference matching the pattern already used for the CF webhook
secret.

## Signature verification

The Inline Code action runs this script — it recomputes the HMAC and compares
in constant time before the flow proceeds to the Halo call:

```javascript
const crypto = require('crypto');
const secret      = workflowContext.actions.get_cf_secret.outputs.body.value;
const timestamp   = workflowContext.trigger.outputs.headers['X-CF-Timestamp'];
const rawBody     = workflowContext.trigger.outputs.body;
const received    = workflowContext.trigger.outputs.headers['X-CF-Signature'];

const message  = `${timestamp}.${JSON.stringify(rawBody)}`;
const expected = 'sha256=' + crypto
    .createHmac('sha256', secret)
    .update(message)
    .digest('hex');

const a = Buffer.from(expected);
const b = Buffer.from(received ?? '');
const valid = a.length === b.length && crypto.timingSafeEqual(a, b);
return { valid };
```

⚠️ **Raw-body caveat.** Logic Apps parses JSON triggers and exposes the
structured body — `JSON.stringify` of the parsed object is not byte-identical
to the original request body, so the signature will fail. If CF has signed
the exact raw bytes, you have two options:

1. **Declare the trigger as non-JSON** (the template does this by omitting
   `schema`) so the body is exposed as a string — this preserves the raw
   bytes for signing but you lose the convenient `@triggerBody()` expression
   syntax.
2. **Move signature verification to a small Azure Function** in front of the
   Logic App, then have the Function forward verified events.

Option 1 is used here for simplicity. For high-throughput production use, go
with option 2 or switch to Sample 02 (the .NET web app) or Sample 05 (Python).

## Mapping

Edit the `compose_halo_ticket` action in `azuredeploy.json` to fit your
Halo workflow. Default mapping is described in [`../README.md`](../README.md).
