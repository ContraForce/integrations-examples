"""ConnectWise callback → ContraForce comment / status (FastAPI)."""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Header, Request
from fastapi.responses import JSONResponse

from app.callbacks.change_tracker import ChangeTracker, TicketState
from app.callbacks.external_reference import try_parse
from app.callbacks.models import CwCallbackPayload
from app.connectwise.client import ConnectWiseClient, CwTicketNote
from app.contraforce.client import ContraForceClient
from app.settings import Settings

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("cw_to_cf_inbound")


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = Settings()
    cw = ConnectWiseClient(settings)
    cf = ContraForceClient(settings)
    app.state.settings = settings
    app.state.cw = cw
    app.state.cf = cf
    app.state.tracker = ChangeTracker()
    try:
        yield
    finally:
        await cw.aclose()
        await cf.aclose()


app = FastAPI(lifespan=lifespan)


def _is_own_echo(note: CwTicketNote, integrator_member_id: str) -> bool:
    if not integrator_member_id:
        return False
    updated_by = note.info.updated_by if note.info else None
    return (updated_by or "").lower() == integrator_member_id.lower()


@app.get("/healthz")
async def healthz() -> dict:
    return {"status": "ok"}


@app.post("/cw/callbacks")
async def cw_callback(
    request: Request,
    callback: CwCallbackPayload,
    x_callback_secret: str | None = Header(default=None, alias="X-Callback-Secret"),
):
    settings: Settings = request.app.state.settings
    cw: ConnectWiseClient = request.app.state.cw
    cf: ContraForceClient = request.app.state.cf
    tracker: ChangeTracker = request.app.state.tracker

    # 1. Reject callbacks missing the shared secret.
    if x_callback_secret != settings.cw_callback_secret:
        logger.warning("CW callback missing/invalid X-Callback-Secret")
        return JSONResponse({"error": "invalid callback secret"}, status_code=401)

    # 2. Only handle service-ticket callbacks.
    if callback.type.lower() != "serviceticket":
        return JSONResponse({"status": "ignored", "reason": "not a ServiceTicket"})

    ticket = await cw.get_ticket(callback.object_id)
    if ticket is None:
        return JSONResponse({"status": "ignored", "reason": "ticket not found"})

    # 3. Only act on tickets our outbound side created.
    parsed = try_parse(ticket.external_reference)
    if parsed is None:
        return JSONResponse({"status": "ignored", "reason": "not a CF-linked ticket"})
    source, incident_id = parsed

    previous = tracker.snapshot(ticket.id)
    notes = await cw.get_ticket_notes(ticket.id, page_size=50)
    latest_note_id = max((n.id for n in notes), default=0)

    # 4. Forward any new notes added since the last time we saw this ticket.
    #    Skip notes authored by our own integrator so we don't echo-loop.
    member_id = callback.member_id or ""
    new_notes = sorted(
        (n for n in notes if n.id > previous.last_note_id and not _is_own_echo(n, member_id)),
        key=lambda n: n.id,
    )

    for note in new_notes:
        text = (note.text or "").strip()
        if not text:
            continue
        author = (note.info.updated_by if note.info else None) or member_id
        body = f"[CW ticket #{ticket.id} note by {author}]\n\n{text}"
        await cf.add_incident_comment(incident_id, source, body)
        logger.info("Forwarded CW note %s to CF incident %s", note.id, incident_id)

    # 5. Close on the CF side if the CW ticket just closed.
    closed_now = ticket.closed_flag and not previous.closed
    if closed_now:
        status_name = ticket.status.name if ticket.status else None
        comment = f"Closed in ConnectWise (ticket #{ticket.id}, status '{status_name}')."
        await cf.close_incident(incident_id, source, comment)
        logger.info("Closed CF incident %s mirroring CW ticket %s", incident_id, ticket.id)

    tracker.record(ticket.id, TicketState(last_note_id=latest_note_id, closed=ticket.closed_flag))
    return JSONResponse(
        {"status": "processed", "notesForwarded": len(new_notes), "closed": closed_now}
    )
