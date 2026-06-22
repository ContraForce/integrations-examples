"""
Parses the `cf|{source}|{incidentId}` format the outbound samples write into
the ServiceNow `correlation_id` field.
"""

from __future__ import annotations


def try_parse(correlation_id: str | None) -> tuple[str, str] | None:
    if not correlation_id:
        return None
    parts = correlation_id.split("|", 2)
    if len(parts) != 3 or parts[0] != "cf":
        return None
    source, incident_id = parts[1], parts[2]
    if not source or not incident_id:
        return None
    return source, incident_id
