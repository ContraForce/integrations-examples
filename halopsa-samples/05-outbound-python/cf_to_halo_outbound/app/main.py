"""ContraForce webhook → HaloPSA ticket (FastAPI)."""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request, Response
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from app.halo.client import HaloClient
from app.halo.mapping import map_to_ticket
from app.halo.token_provider import HaloTokenProvider
from app.settings import Settings
from app.webhook import signature
from app.webhook.models import WebhookEnvelope

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("cf_to_halo_outbound")


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = Settings()
    tokens = HaloTokenProvider(settings)
    halo = HaloClient(settings, tokens)
    app.state.settings = settings
    app.state.tokens = tokens
    app.state.halo = halo
    try:
        yield
    finally:
        await halo.aclose()
        await tokens.aclose()


app = FastAPI(lifespan=lifespan)


@app.get("/healthz")
async def healthz() -> dict:
    return {"status": "ok"}


@app.post("/cf/webhooks")
async def cf_webhook(request: Request) -> Response:
    settings: Settings = request.app.state.settings
    halo: HaloClient = request.app.state.halo

    raw_body = await request.body()

    sig_header = request.headers.get("X-CF-Signature")
    ts_header = request.headers.get("X-CF-Timestamp")
    event_id = request.headers.get("X-CF-Event-Id")
    schema = request.headers.get("X-CF-Schema")

    if not event_id:
        logger.warning("Webhook missing X-CF-Event-Id")
        return JSONResponse({"error": "missing X-CF-Event-Id"}, status_code=400)

    if not signature.verify(
        secret=settings.cf_webhook_secret,
        signature_header=sig_header,
        timestamp_header=ts_header,
        raw_body=raw_body,
        max_skew_seconds=settings.cf_max_skew_seconds,
    ):
        logger.warning("Webhook signature verification failed for event %s", event_id)
        return JSONResponse({"error": "invalid signature"}, status_code=401)

    if (schema or "").lower() != "incident.created.v1":
        logger.info("Ignoring unhandled webhook schema %s", schema)
        return JSONResponse({"status": "ignored", "reason": "unhandled schema"})

    try:
        envelope = WebhookEnvelope.model_validate_json(raw_body)
    except ValidationError as ex:
        logger.warning("Webhook payload validation failed: %s", ex)
        return JSONResponse({"error": "malformed payload"}, status_code=400)

    incident = envelope.data
    if incident is None:
        return JSONResponse({"error": "malformed payload"}, status_code=400)

    external_reference = f"cf|{incident.source}|{incident.incident_id}"
    existing_id = await halo.find_ticket_by_external_reference(external_reference)
    if existing_id is not None:
        await halo.add_private_note(
            existing_id,
            note_html=f"<p>ContraForce re-delivered event <code>{event_id}</code></p>",
        )
        return JSONResponse({"status": "updated", "ticketId": existing_id})

    ticket_payload = map_to_ticket(incident, external_reference, settings, existing_ticket_id=None)
    new_id = await halo.upsert_ticket(ticket_payload)
    return JSONResponse({"status": "created", "ticketId": new_id})
