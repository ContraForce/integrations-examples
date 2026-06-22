# Sample 03 — ServiceNow incident changes → ContraForce incident (Logic App)

A Consumption Logic App that:

1. Receives a ServiceNow Business Rule callback on an HTTPS trigger
2. Rejects callbacks missing the shared `X-SNow-Secret` header
3. Pulls the full incident from the Table API (the callback only carries the
   `sys_id`)
4. Parses `correlation_id` to recover the ContraForce `incidentId` and
   `source`; ignores incidents that weren't created by samples 01 / 02 / 05
5. Posts the latest customer-visible comment to the matching CF incident, and
   closes the CF incident when the ServiceNow incident reaches the resolved or
   closed state

## Limitations vs. Sample 04

Logic Apps cannot easily maintain per-incident state across runs without an
external store. This sample keeps no per-incident state, so:

- Only the **latest comment** on each callback is forwarded. If two comments
  are added between callbacks, only the newest is mirrored.
- The "already closed — don't re-close" check relies on ContraForce's own
  idempotency: calling `PUT /status` with `Closed` on an already-closed
  incident is a no-op.

If you need loss-free comment mirroring or richer change detection, use
Sample 04 (.NET) or Sample 06 (Python).

## Deploy

```bash
cp azuredeploy.parameters.example.json azuredeploy.parameters.json
az deployment group create \
  --resource-group <rg> \
  --template-file azuredeploy.json \
  --parameters @azuredeploy.parameters.json
```

## Register the callback in ServiceNow

Grab the workflow's trigger URL:

```bash
az rest --method POST --uri "https://management.azure.com$(
  az logic workflow show -g <rg> -n snow-to-cf-inbound --query id -o tsv
)/triggers/snow_callback/listCallbackUrl?api-version=2019-05-01" \
  --query value -o tsv
```

In ServiceNow, create a **Business Rule** on the `Incident [incident]` table:

- **When**: `after`, on **Update** (and **Insert** for create echoes).
- **Condition**: `correlation_id STARTSWITH cf|` so it only fires for
  ContraForce-linked incidents.
- **Advanced → Script**: use `sn_ws.RESTMessageV2` to POST
  `{ "sys_id": current.sys_id.toString(), "number": current.number.toString() }`
  to the URL above, adding the header `X-SNow-Secret` set to the
  `callbackSecret` you deployed with. ServiceNow doesn't sign outbound
  messages, so this header is the auth boundary.

## Secrets

- `snowPassword`, `cfServiceAccountClientSecret`, and `callbackSecret` are
  passed as secure-string parameters and pre-encoded into Basic credentials in
  the workflow definition. Replace them with Key Vault references in
  production.
- Rotate secrets by redeploying the template.

## What "Closed" means in ServiceNow

The `state` field is numeric and tenant-customizable. The defaults treat `6`
(Resolved) and `7` (Closed) as close triggers — set `snowResolvedState` and
`snowClosedState` to match your instance.
