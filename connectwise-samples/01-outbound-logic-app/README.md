# Sample 01 — ContraForce webhook → ConnectWise ticket (Logic App)

A Consumption Logic App that:

1. Receives a ContraForce `incident.created.v1` webhook on an HTTPS trigger
2. Verifies `X-CF-Signature` via an Inline Code (JavaScript) action
3. Looks up an existing CW ticket by `externalReference` so repeat deliveries
   update rather than duplicate
4. Creates or updates a ConnectWise service ticket with mapped fields

## Deploy

```bash
# 1. Copy the parameters file and fill in your values
cp azuredeploy.parameters.example.json azuredeploy.parameters.json

# 2. Deploy the Logic App
az deployment group create \
  --resource-group <your-rg> \
  --template-file azuredeploy.json \
  --parameters @azuredeploy.parameters.json
```

After deployment, grab the workflow's HTTPS trigger URL:

```bash
az logic workflow show \
  --resource-group <your-rg> \
  --name cf-to-cw-outbound \
  --query 'accessEndpoint' -o tsv
```

Call `az rest` against the workflow's `listCallbackUrl` action to get the
signed URL that includes the SAS token. Paste that URL into the ContraForce
portal as the webhook destination.

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

## ConnectWise credentials

The template expects ConnectWise credentials as secure parameters at deploy
time. They're written into the Logic App definition as secure string
parameters — not visible in the portal UI. Rotate them by redeploying.

For stronger secret hygiene, replace the `cwPublicKey` / `cwPrivateKey`
parameters with Key Vault references, matching the pattern already used for
the webhook secret.

## Signature verification

The Inline Code action runs this script — it recomputes the HMAC and compares
in constant time before the flow proceeds to the ConnectWise call:

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

// constant-time compare
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
with option 2 or switch to Sample 02 (the web app).

## Mapping

See `mapping.md` at the repo root. Override the mapping by editing the
`compose_cw_ticket` action in `azuredeploy.json`.
