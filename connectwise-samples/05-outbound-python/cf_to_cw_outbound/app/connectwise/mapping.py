"""
Translates a ContraForce incident into a ConnectWise ticket create payload.
Edit this file to fit your board schema — everything else in the sample is
plumbing.
"""

from __future__ import annotations

from app.settings import Settings
from app.webhook.models import IncidentCreatedPayload


def map_priority(severity: str | None) -> int:
    severity_upper = (severity or "").upper()
    if severity_upper == "HIGH":
        return 1
    if severity_upper == "MEDIUM":
        return 2
    return 3


def _truncate(value: str, max_len: int) -> str:
    return value if len(value) <= max_len else value[:max_len]


def _build_description(p: IncidentCreatedPayload) -> str:
    lines: list[str] = [
        f"ContraForce incident {p.incident_id} (#{p.incident_number})",
        "",
        f"Severity: {p.severity}",
        f"Source:   {p.source_display_name}",
        f"Created:  {p.created_at.isoformat()}",
    ]
    if p.owner is not None:
        lines.append(f"Owner:    {p.owner.display_name} <{p.owner.email}>")
    lines.append("")

    if p.description:
        lines.append(p.description)
        lines.append("")

    if p.alerts:
        lines.append("Alerts:")
        lines.extend(
            f"  - [{alert.severity}] {alert.title} ({alert.product_name})" for alert in p.alerts
        )
        lines.append("")

    if p.entities:
        lines.append("Entities:")
        lines.extend(f"  - {entity.type}: {entity.display_name}" for entity in p.entities)

    return "\n".join(lines)


def map_to_ticket(
    incident: IncidentCreatedPayload,
    external_reference: str,
    settings: Settings,
) -> dict:
    summary = _truncate(f"[CF #{incident.incident_number}] {incident.title}", 100)
    return {
        "summary": summary,
        "initialDescription": _build_description(incident),
        "board": {"id": settings.cw_default_board_id},
        "company": {"identifier": settings.cw_default_company_identifier},
        "priority": {"id": map_priority(incident.severity)},
        "externalReference": external_reference,
    }
