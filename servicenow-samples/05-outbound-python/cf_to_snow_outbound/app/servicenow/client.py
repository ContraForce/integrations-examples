"""Thin ServiceNow Table API client for the subset used by this sample."""

from __future__ import annotations

import base64
import logging
from typing import Any

import httpx

from app.settings import Settings

logger = logging.getLogger(__name__)


class ServiceNowClient:
    def __init__(self, settings: Settings, client: httpx.AsyncClient | None = None):
        self._settings = settings
        token = base64.b64encode(
            f"{settings.snow_username}:{settings.snow_password}".encode("utf-8")
        ).decode("ascii")
        self._client = client or httpx.AsyncClient(
            base_url=settings.snow_instance_url.rstrip("/") + "/api/now/table/",
            timeout=30.0,
            headers={
                "Authorization": f"Basic {token}",
                "Accept": "application/json",
            },
        )

    async def aclose(self) -> None:
        await self._client.aclose()

    async def find_incident_by_correlation_id(self, correlation_id: str) -> str | None:
        """Return the sys_id of an existing incident, or None."""
        params = {
            "sysparm_limit": "1",
            "sysparm_exclude_reference_link": "true",
            "sysparm_fields": "sys_id,number,state",
            "sysparm_query": f"correlation_id={correlation_id}",
        }
        response = await self._client.get("incident", params=params)
        response.raise_for_status()

        result = response.json().get("result", [])
        if not result:
            return None
        return result[0]["sys_id"]

    async def create_incident(self, incident: dict[str, Any]) -> dict[str, Any]:
        params = {
            "sysparm_exclude_reference_link": "true",
            "sysparm_fields": "sys_id,number,state",
        }
        response = await self._client.post("incident", params=params, json=incident)
        if not response.is_success:
            logger.error("ServiceNow create_incident failed: %s — %s", response.status_code, response.text)
        response.raise_for_status()

        created = response.json()["result"]
        logger.info(
            "Created ServiceNow incident %s (%s) correlation_id %s",
            created.get("number"),
            created.get("sys_id"),
            incident.get("correlation_id"),
        )
        return created

    async def add_work_note(self, sys_id: str, note: str) -> None:
        """PATCH work_notes — ServiceNow journals the value, so it appends."""
        params = {"sysparm_fields": "sys_id"}
        response = await self._client.patch(
            f"incident/{sys_id}", params=params, json={"work_notes": note}
        )
        if not response.is_success:
            logger.error("ServiceNow add_work_note failed: %s — %s", response.status_code, response.text)
        response.raise_for_status()
