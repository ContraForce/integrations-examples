# Sample 03 — ConnectWise ticket changes → ContraForce incident (Logic App)

A Consumption Logic App that:

1. Receives ConnectWise Callbacks on an HTTPS trigger
2. Rejects callbacks missing the shared `X-Callback-Secret` header
3. Fetches the full ticket + its most recent note from ConnectWise
4. Parses `externalReference` to recover the ContraForce `incidentId` and
   `source`; ignores tickets that weren't created by samples 01 / 02
5. Posts the latest note back as a comment on the matching CF incident, and
   closes the CF incident when the CW ticket closes

## Limitations vs. Sample 04

Logic Apps cannot easily maintain per-ticket state across runs without
adding an external data store (Azure Table, Cosmos, etc). To stay
self-contained:

- This Logic App only forwards the **latest note** on each callback. If two
  notes are added between callback deliveries, only the newest is mirrored.
- It can't detect the "already closed — don't re-close" case, so it uses
  ContraForce's own idempotency: calling `PUT /status` with `Closed` on an
  already-closed incident is a no-op.

If you need loss-free note mirroring or more sophisticated change detection,
use Sample 04 (web app) instead.

## Deploy

```bash
cp azuredeploy.parameters.example.json azuredeploy.parameters.json
az deployment group create \
  --resource-group <rg> \
  --template-file azuredeploy.json \
  --parameters @azuredeploy.parameters.json
```

## Register the callback in ConnectWise

Grab the workflow's trigger URL:

```bash
az rest --method POST --uri "https://management.azure.com$(
  az logic workflow show -g <rg> -n cw-to-cf-inbound --query id -o tsv
)/triggers/cw_callback/listCallbackUrl?api-version=2019-05-01" \
  --query value -o tsv
```

Register a ConnectWise Callback pointing to that URL with these additional
headers:

- `X-Callback-Secret: <value of callbackSecret parameter>`

Configure the callback to fire on `ServiceTicket` `updated` events.

## Secrets

- `CF_SERVICE_ACCOUNT_CLIENT_SECRET` and `cwPrivateKey` are consumed as
  secure string parameters. Replace them with Key Vault references in
  production.
- The callback shared secret (`callbackSecret`) is passed the same way —
  rotate it regularly and redeploy to change.
