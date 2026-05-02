# Sample 03 — HaloPSA ticket changes → ContraForce incident (Logic App)

A Consumption Logic App that:

1. Receives Halo outbound webhooks on an HTTPS trigger
2. Rejects callbacks missing the shared `X-Halo-Secret` header
3. Acquires a Halo bearer token (OAuth2 client_credentials)
4. Pulls the full ticket and its most recent action from Halo
5. Parses the external reference (`thirdpartynumber`) to recover the
   ContraForce `incidentId` and `source`; ignores tickets that weren't
   created by samples 01 / 02 / 05
6. Posts the latest action as a comment on the matching CF incident, and
   closes the CF incident when the Halo ticket reaches the closed status

## Limitations vs. Sample 04

Logic Apps cannot easily maintain per-ticket state across runs without
adding an external data store. This sample keeps no per-ticket state, so:

- Only the **latest action** on each webhook is forwarded. If two actions
  are added between webhook deliveries, only the newest is mirrored.
- The "already closed — don't re-close" check relies on ContraForce's own
  idempotency: calling `PUT /status` with `Closed` on an already-closed
  incident is a no-op.

If you need loss-free action mirroring or richer change detection, use
Sample 04 (.NET) or Sample 06 (Python).

## Deploy

```bash
cp azuredeploy.parameters.example.json azuredeploy.parameters.json
az deployment group create \
  --resource-group <rg> \
  --template-file azuredeploy.json \
  --parameters @azuredeploy.parameters.json
```

## Register the webhook in HaloPSA

Grab the workflow's trigger URL:

```bash
az rest --method POST --uri "https://management.azure.com$(
  az logic workflow show -g <rg> -n halo-to-cf-inbound --query id -o tsv
)/triggers/halo_webhook/listCallbackUrl?api-version=2019-05-01" \
  --query value -o tsv
```

In Halo, **Configuration → Integrations → Webhooks → New**:

- **URL**: paste the URL from above.
- **Method**: POST.
- **Triggers**: `New Ticket Logged`, `Ticket Updated` (action events are
  generally routed via Ticket Updated — verify per-tenant).
- **Headers**: add `X-Halo-Secret` set to the `callbackSecret` value you used
  when deploying the template. Halo doesn't sign payloads, so this is the
  auth boundary.

## Secrets

- `cfServiceAccountClientSecret`, `haloClientSecret`, and `callbackSecret` are
  passed as secure-string parameters. Replace them with Key Vault references
  in production.
- Rotate secrets by redeploying the template.
