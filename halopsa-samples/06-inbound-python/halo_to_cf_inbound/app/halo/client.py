"""Read-only Halo client — ticket + actions for the inbound flow."""

from __future__ import annotations

import logging
from datetime import datetime

import httpx
from pydantic import BaseModel, ConfigDict, Field

from app.halo.token_provider import HaloTokenProvider
from app.settings import Settings

logger = logging.getLogger(__name__)


class HaloCustomFieldRef(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    id: int
    name: str | None = None
    value: str | None = None


class HaloTicket(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="allow")

    id: int
    summary: str | None = None
    third_party_number: str | None = Field(default=None, alias="thirdpartynumber")
    status_id: int | None = Field(default=None, alias="status_id")
    status_name: str | None = Field(default=None, alias="status_name")
    customfields: list[HaloCustomFieldRef] | None = None


class HaloActionRecord(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="allow")

    id: int
    ticket_id: int
    outcome: str | None = None
    note: str | None = None
    note_html: str | None = None
    hidden_from_user: bool = Field(default=False, alias="hiddenfromuser")
    agent_id: int | None = None
    who: str | None = None
    datetime: datetime | None = None


class HaloClient:
    def __init__(
        self,
        settings: Settings,
        tokens: HaloTokenProvider,
        client: httpx.AsyncClient | None = None,
    ):
        self._settings = settings
        self._tokens = tokens
        self._client = client or httpx.AsyncClient(
            base_url=settings.halo_api_base_url.rstrip("/") + "/",
            timeout=30.0,
        )

    async def aclose(self) -> None:
        await self._client.aclose()

    async def _auth_headers(self) -> dict[str, str]:
        token = await self._tokens.get_token()
        return {"Authorization": f"Bearer {token}"}

    async def get_ticket(self, ticket_id: int) -> HaloTicket | None:
        response = await self._client.get(
            f"Tickets/{ticket_id}", headers=await self._auth_headers()
        )
        if not response.is_success:
            logger.warning("Halo GET /Tickets/%s failed: %s", ticket_id, response.status_code)
            return None
        return HaloTicket.model_validate(response.json())

    async def get_ticket_actions(self, ticket_id: int) -> list[HaloActionRecord]:
        response = await self._client.get(
            "Actions",
            params={"ticket_id": ticket_id},
            headers=await self._auth_headers(),
        )
        response.raise_for_status()
        payload = response.json()
        items = payload if isinstance(payload, list) else payload.get("actions", [])
        return [HaloActionRecord.model_validate(item) for item in items]

    def extract_external_reference(self, ticket: HaloTicket) -> str | None:
        if self._settings.halo_external_ref_field_id is not None:
            target = self._settings.halo_external_ref_field_id
            for field in ticket.customfields or []:
                if field.id == target:
                    return field.value
            return None
        return ticket.third_party_number
