"""
Translates a ContraForce incident into a Halo ticket upsert payload.
Edit this file to fit your Halo workflow — everything else is plumbing.
"""

from __future__ import annotations

from html import escape as h

from app.settings import Settings
from app.webhook.models import IncidentCreatedPayload


def map_priority(severity: str | None) -> int:
    s = (severity or "").upper()
    if s == "HIGH":
        return 1
    if s == "MEDIUM":
        return 2
    return 3


def _truncate(value: str, max_len: int) -> str:
    return value if len(value) <= max_len else value[:max_len]


def _build_details_html(p: IncidentCreatedPayload) -> str:
    parts: list[str] = [
        f"<p><strong>ContraForce incident</strong> {h(p.incident_id)} (#{p.incident_number})</p>",
        "<ul>",
        f"<li><strong>Severity:</strong> {h(p.severity)}</li>",
        f"<li><strong>Source:</strong> {h(p.source_display_name)}</li>",
        f"<li><strong>Created:</strong> {h(p.created_at.isoformat())}</li>",
    ]
    if p.owner is not None:
        parts.append(
            f"<li><strong>Owner:</strong> {h(p.owner.display_name or '')} &lt;{h(p.owner.email or '')}&gt;</li>"
        )
    parts.append("</ul>")

    if p.description:
        parts.append(f"<p>{h(p.description)}</p>")

    if p.alerts:
        parts.append("<p><strong>Alerts</strong></p><ul>")
        parts.extend(
            f"<li>[{h(a.severity)}] {h(a.title)} ({h(a.product_name)})</li>" for a in p.alerts
        )
        parts.append("</ul>")

    if p.entities:
        parts.append("<p><strong>Entities</strong></p><ul>")
        parts.extend(f"<li>{h(e.type)}: {h(e.display_name)}</li>" for e in p.entities)
        parts.append("</ul>")

    return "".join(parts)


def map_to_ticket(
    incident: IncidentCreatedPayload,
    external_reference: str,
    settings: Settings,
    existing_ticket_id: int | None,
) -> dict:
    summary = _truncate(f"[CF #{incident.incident_number}] {incident.title}", 200)

    payload: dict = {
        "summary": summary,
        "details_html": _build_details_html(incident),
        "client_id": settings.halo_default_client_id,
        "tickettype_id": settings.halo_default_tickettype_id,
        "priority_id": map_priority(incident.severity),
    }

    if existing_ticket_id is not None:
        payload["id"] = existing_ticket_id

    if settings.halo_external_ref_field_id is not None:
        payload["customfields"] = [
            {
                "id": settings.halo_external_ref_field_id,
                "value": external_reference,
            }
        ]
    else:
        payload["thirdpartynumber"] = external_reference

    return payload
