"""ServiceNow incident changes → ContraForce comment / status (FastAPI)."""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Header, Request
from fastapi.responses import JSONResponse

from app.callbacks.change_tracker import ChangeTracker, IncidentState
from app.callbacks.external_reference import try_parse
from app.callbacks.models import SnowCallbackPayload
from app.contraforce.client import ContraForceClient
from app.servicenow.client import ServiceNowClient, SnowJournalEntry
from app.settings import Settings

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("snow_to_cf_inbound")


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = Settings()
    snow = ServiceNowClient(settings)
    cf = ContraForceClient(settings)
    app.state.settings = settings
    app.state.snow = snow
    app.state.cf = cf
    app.state.tracker = ChangeTracker()
    try:
        yield
    finally:
        await snow.aclose()
        await cf.aclose()


app = FastAPI(lifespan=lifespan)


def _is_own_echo(entry: SnowJournalEntry, integration_user: str | None) -> bool:
    return integration_user is not None and entry.sys_created_by == integration_user


def _is_forwardable(entry: SnowJournalEntry, forward_work_notes: bool) -> bool:
    if entry.element == "comments":
        return True
    return forward_work_notes and entry.element == "work_notes"


@app.get("/healthz")
async def healthz() -> dict:
    return {"status": "ok"}


@app.post("/snow/callbacks")
async def snow_callback(
    request: Request,
    payload: SnowCallbackPayload,
    x_snow_secret: str | None = Header(default=None, alias="X-SNow-Secret"),
):
    settings: Settings = request.app.state.settings
    snow: ServiceNowClient = request.app.state.snow
    cf: ContraForceClient = request.app.state.cf
    tracker: ChangeTracker = request.app.state.tracker

    # 1. Reject callbacks missing the shared secret.
    if x_snow_secret != settings.snow_callback_secret:
        logger.warning("ServiceNow callback missing/invalid X-SNow-Secret")
        return JSONResponse({"error": "invalid callback secret"}, status_code=401)

    if not payload.sys_id:
        return JSONResponse({"status": "ignored", "reason": "no sys_id in payload"})

    # 2. Pull the full incident — the Business Rule payload only carries the
    #    sys_id, so re-fetch to get correlation_id and current state.
    incident = await snow.get_incident(payload.sys_id)
    if incident is None:
        return JSONResponse({"status": "ignored", "reason": "incident not found"})

    # 3. Only act on incidents our outbound side created.
    parsed = try_parse(incident.correlation_id)
    if parsed is None:
        return JSONResponse({"status": "ignored", "reason": "not a CF-linked incident"})
    source, incident_id = parsed

    previous = tracker.snapshot(incident.sys_id)
    journal = await snow.get_journal_entries(incident.sys_id)

    # Advance the watermark past every entry we've now seen, forwarded or not.
    latest_created_on = max(
        (e.sys_created_on or "" for e in journal),
        default=previous.last_journal_created_on,
    )
    latest_created_on = max(latest_created_on, previous.last_journal_created_on)

    # 4. Forward new journal entries. Customer-visible comments by default
    #    (work notes too when configured); skip our own integration user's
    #    entries to avoid echo loops.
    new_entries = sorted(
        (
            e
            for e in journal
            if _is_forwardable(e, settings.snow_forward_work_notes)
            and (e.sys_created_on or "") > previous.last_journal_created_on
            and not _is_own_echo(e, settings.snow_integration_user)
        ),
        key=lambda e: e.sys_created_on or "",
    )

    for entry in new_entries:
        text = (entry.value or "").strip()
        if not text:
            continue
        author = entry.sys_created_by or "ServiceNow"
        kind = "work note" if entry.element == "work_notes" else "comment"
        body = f"[ServiceNow {incident.number} {kind} by {author}]\n\n{text}"
        await cf.add_incident_comment(incident_id, source, body)
        logger.info("Forwarded ServiceNow journal entry %s to CF incident %s", entry.sys_id, incident_id)

    # 5. Close on the CF side if the incident just resolved/closed.
    try:
        state_value = int(incident.state) if incident.state is not None else -1
    except ValueError:
        state_value = -1
    is_closed = state_value in (settings.snow_resolved_state, settings.snow_closed_state)
    closed_now = is_closed and not previous.closed
    if closed_now:
        detail = incident.close_notes or incident.close_code or ""
        comment = f"Closed in ServiceNow (incident {incident.number}, state {incident.state})."
        if detail:
            comment = f"{comment} {detail}"
        await cf.close_incident(incident_id, source, comment)
        logger.info("Closed CF incident %s mirroring ServiceNow incident %s", incident_id, incident.number)

    tracker.record(
        incident.sys_id,
        IncidentState(last_journal_created_on=latest_created_on, closed=is_closed),
    )
    return JSONResponse(
        {
            "status": "processed",
            "commentsForwarded": len(new_entries),
            "closed": closed_now,
        }
    )
