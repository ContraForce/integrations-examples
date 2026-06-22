"""ContraForce webhook → ServiceNow incident (FastAPI)."""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request, Response
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from app.servicenow.client import ServiceNowClient
from app.servicenow.mapping import map_to_incident
from app.settings import Settings
from app.webhook import signature
from app.webhook.models import WebhookEnvelope

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("cf_to_snow_outbound")


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = Settings()
    snow = ServiceNowClient(settings)
    app.state.settings = settings
    app.state.snow = snow
    try:
        yield
    finally:
        await snow.aclose()


app = FastAPI(lifespan=lifespan)


@app.get("/healthz")
async def healthz() -> dict:
    return {"status": "ok"}


@app.post("/cf/webhooks")
async def cf_webhook(request: Request) -> Response:
    settings: Settings = request.app.state.settings
    snow: ServiceNowClient = request.app.state.snow

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

    correlation_id = f"cf|{incident.source}|{incident.incident_id}"
    existing_sys_id = await snow.find_incident_by_correlation_id(correlation_id)
    if existing_sys_id is not None:
        await snow.add_work_note(
            existing_sys_id,
            f"ContraForce re-delivered event {event_id}",
        )
        return JSONResponse({"status": "updated", "sysId": existing_sys_id})

    incident_payload = map_to_incident(incident, correlation_id, settings)
    created = await snow.create_incident(incident_payload)
    return JSONResponse(
        {"status": "created", "sysId": created.get("sys_id"), "number": created.get("number")}
    )
