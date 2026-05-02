"""
Parses the `cf|{source}|{incidentId}` format the outbound samples write into
`thirdpartynumber` (or a configured custom field).
"""

from __future__ import annotations


def try_parse(external_reference: str | None) -> tuple[str, str] | None:
    if not external_reference:
        return None
    parts = external_reference.split("|", 2)
    if len(parts) != 3 or parts[0] != "cf":
        return None
    source, incident_id = parts[1], parts[2]
    if not source or not incident_id:
        return None
    return source, incident_id
