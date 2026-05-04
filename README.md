# ContraForce integration examples

Reference implementations you can fork and adapt when wiring ContraForce into
your existing ticketing, automation, or notification stack.

Every sample is **fully runnable** (not pseudocode) and uses only the public
ContraForce webhook envelope and v2 REST API. Secrets are always pulled from
configuration — never baked in.

## Samples

| Folder | Scenario |
|--------|----------|
| [`connectwise-samples/`](./connectwise-samples) | Bi-directional integration with ConnectWise Manage PSA. Six drop-in implementations: Logic App, ASP.NET Core, and Python (FastAPI) variants for both inbound and outbound flows. |
| [`halopsa-samples/`](./halopsa-samples) | Bi-directional integration with HaloPSA. Six drop-in implementations: Logic App, ASP.NET Core, and Python (FastAPI) variants for both inbound and outbound flows. Halo auth uses OAuth2 `client_credentials`. |

More scenarios (ServiceNow, Jira Service Management, Microsoft Teams,
PagerDuty, Slack…) may land here over time.

## What you get from ContraForce

Each sample is built around two public surfaces:

- **Outbound webhooks.** ContraForce POSTs signed JSON events to a URL you
  own — e.g. `incident.created.v1`. Events are HMAC-SHA256 signed and
  include an event id you can use for idempotency.
- **Inbound REST API.** You authenticate with a service account (HTTP Basic
  over TLS) and call endpoints under
  `/api/v2/workspaces/{workspaceId}/…` to add comments, close incidents,
  change assignees, etc.

See each sample's own README for the exact headers, payload shapes, and
endpoints it relies on.

## Conventions

- Samples use **.NET 8 LTS** for the web app variants so they run on any
  supported .NET 8 host (App Service, Container Apps, AKS, or plain
  containers).
- Python samples use **Python 3.12** + **FastAPI** + **httpx** + **pydantic
  v2** and ship with a Dockerfile so they run on the same hosts.
- Logic App samples target **Consumption SKU** Azure Logic Apps.
- All configurable values live in `appsettings.Example.json` /
  `azuredeploy.parameters.example.json` / `.env.example` files. Copy them
  to a local, git-ignored equivalent before running.
- No secret is ever committed — every `REPLACE_…` string is a placeholder.

## Contributing

PRs welcome — especially for new destinations (ServiceNow, Jira, Teams,
PagerDuty). Keep samples small and focused: one scenario per folder, a
clear README, no inherited dependencies from unrelated samples.

## License

MIT — see [LICENSE](./LICENSE).
