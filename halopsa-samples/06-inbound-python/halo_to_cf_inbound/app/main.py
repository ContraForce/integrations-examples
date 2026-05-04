"""Halo webhook → ContraForce comment / status (FastAPI)."""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Header, Request
from fastapi.responses import JSONResponse

from app.contraforce.client import ContraForceClient
from app.halo.client import HaloActionRecord, HaloClient
from app.halo.token_provider import HaloTokenProvider
from app.settings import Settings
from app.webhook.change_tracker import ChangeTracker, TicketState
from app.webhook.external_reference import try_parse
from app.webhook.models import HaloWebhookPayload

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("halo_to_cf_inbound")


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = Settings()
    tokens = HaloTokenProvider(settings)
    halo = HaloClient(settings, tokens)
    cf = ContraForceClient(settings)
    app.state.settings = settings
    app.state.tokens = tokens
    app.state.halo = halo
    app.state.cf = cf
    app.state.tracker = ChangeTracker()
    try:
        yield
    finally:
        await halo.aclose()
        await tokens.aclose()
        await cf.aclose()


app = FastAPI(lifespan=lifespan)


def _is_own_echo(action: HaloActionRecord, integration_agent_id: int | None) -> bool:
    return integration_agent_id is not None and action.agent_id == integration_agent_id


@app.get("/healthz")
async def healthz() -> dict:
    return {"status": "ok"}


@app.post("/halo/webhooks")
async def halo_webhook(
    request: Request,
    payload: HaloWebhookPayload,
    x_halo_secret: str | None = Header(default=None, alias="X-Halo-Secret"),
):
    settings: Settings = request.app.state.settings
    halo: HaloClient = request.app.state.halo
    cf: ContraForceClient = request.app.state.cf
    tracker: ChangeTracker = request.app.state.tracker

    # 1. Reject webhooks missing the shared secret.
    if x_halo_secret != settings.halo_webhook_secret:
        logger.warning("Halo webhook missing/invalid X-Halo-Secret")
        return JSONResponse({"error": "invalid callback secret"}, status_code=401)

    ticket_id = payload.resolve_ticket_id()
    if ticket_id is None:
        return JSONResponse({"status": "ignored", "reason": "no ticket id in payload"})

    # 2. Pull the full ticket — Halo's payload varies by trigger.
    ticket = await halo.get_ticket(ticket_id)
    if ticket is None:
        return JSONResponse({"status": "ignored", "reason": "ticket not found"})

    # 3. Only act on tickets our outbound side created.
    parsed = try_parse(halo.extract_external_reference(ticket))
    if parsed is None:
        return JSONResponse({"status": "ignored", "reason": "not a CF-linked ticket"})
    source, incident_id = parsed

    previous = tracker.snapshot(ticket.id)
    actions = await halo.get_ticket_actions(ticket.id)
    latest_action_id = max((a.id for a in actions), default=0)

    # 4. Forward new public actions; skip private / own-agent / empty.
    new_actions = sorted(
        (
            a
            for a in actions
            if a.id > previous.last_action_id
            and not a.hidden_from_user
            and not _is_own_echo(a, settings.halo_integration_agent_id)
        ),
        key=lambda a: a.id,
    )

    for action in new_actions:
        text = (action.note_html or action.note or "").strip()
        if not text:
            continue
        who = action.who or "Halo"
        body = f"[Halo ticket #{ticket.id} note by {who}]\n\n{text}"
        await cf.add_incident_comment(incident_id, source, body)
        logger.info("Forwarded Halo action %s to CF incident %s", action.id, incident_id)

    # 5. Close on the CF side if the Halo ticket just closed.
    is_closed = ticket.status_id == settings.halo_closed_status_id
    closed_now = is_closed and not previous.closed
    if closed_now:
        comment = f"Closed in HaloPSA (ticket #{ticket.id}, status '{ticket.status_name or 'Closed'}')."
        await cf.close_incident(incident_id, source, comment)
        logger.info("Closed CF incident %s mirroring Halo ticket %s", incident_id, ticket.id)

    tracker.record(ticket.id, TicketState(last_action_id=latest_action_id, closed=is_closed))
    return JSONResponse(
        {
            "status": "processed",
            "actionsForwarded": len(new_actions),
            "closed": closed_now,
        }
    )
