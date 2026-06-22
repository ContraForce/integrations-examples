"""Read-only ServiceNow client — incident + journal for the inbound flow."""

from __future__ import annotations

import base64
import logging

import httpx
from pydantic import BaseModel, ConfigDict, Field

from app.settings import Settings

logger = logging.getLogger(__name__)


class SnowIncident(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="allow")

    sys_id: str
    number: str | None = None
    state: str | None = None
    correlation_id: str | None = None
    short_description: str | None = None
    close_code: str | None = None
    close_notes: str | None = None


class SnowJournalEntry(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="allow")

    sys_id: str
    element: str | None = None
    value: str | None = None
    sys_created_on: str | None = None
    sys_created_by: str | None = None


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

    async def get_incident(self, sys_id: str) -> SnowIncident | None:
        params = {
            "sysparm_exclude_reference_link": "true",
            "sysparm_fields": "sys_id,number,state,correlation_id,short_description,close_code,close_notes",
        }
        response = await self._client.get(f"incident/{sys_id}", params=params)
        if not response.is_success:
            logger.warning("ServiceNow GET /incident/%s failed: %s", sys_id, response.status_code)
            return None
        return SnowIncident.model_validate(response.json()["result"])

    async def get_journal_entries(self, sys_id: str) -> list[SnowJournalEntry]:
        """Comments + work notes for an incident, oldest first."""
        params = {
            "sysparm_fields": "sys_id,element,value,sys_created_on,sys_created_by",
            "sysparm_query": f"element_id={sys_id}^ORDERBYsys_created_on",
        }
        response = await self._client.get("sys_journal_field", params=params)
        response.raise_for_status()
        return [SnowJournalEntry.model_validate(e) for e in response.json().get("result", [])]
