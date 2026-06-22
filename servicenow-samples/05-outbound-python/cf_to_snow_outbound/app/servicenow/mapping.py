"""
Translates a ContraForce incident into a ServiceNow incident create payload.
Edit this file to fit your instance — everything else is plumbing.
"""

from __future__ import annotations

from app.settings import Settings
from app.webhook.models import IncidentCreatedPayload


def map_urgency(severity: str | None) -> int:
    """ServiceNow urgency: 1 = High, 2 = Medium, 3 = Low. Priority is derived
    from the urgency × impact matrix on the instance."""
    s = (severity or "").upper()
    if s == "HIGH":
        return 1
    if s == "MEDIUM":
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
        lines.append(f"Owner:    {p.owner.display_name or ''} <{p.owner.email or ''}>")
    lines.append("")

    if p.description:
        lines.append(p.description)
        lines.append("")

    if p.alerts:
        lines.append("Alerts:")
        lines.extend(f"  - [{a.severity}] {a.title} ({a.product_name})" for a in p.alerts)
        lines.append("")

    if p.entities:
        lines.append("Entities:")
        lines.extend(f"  - {e.type}: {e.display_name}" for e in p.entities)

    return "\n".join(lines)


def map_to_incident(
    incident: IncidentCreatedPayload,
    correlation_id: str,
    settings: Settings,
) -> dict:
    payload: dict = {
        "short_description": _truncate(
            f"[CF #{incident.incident_number}] {incident.title}", 160
        ),
        "description": _build_description(incident),
        "urgency": str(map_urgency(incident.severity)),
        "impact": str(settings.snow_default_impact),
        "correlation_id": correlation_id,
    }

    if settings.snow_assignment_group_sys_id:
        payload["assignment_group"] = settings.snow_assignment_group_sys_id
    if settings.snow_caller_sys_id:
        payload["caller_id"] = settings.snow_caller_sys_id

    return payload
