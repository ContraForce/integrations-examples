"""Read-only ConnectWise Manage client for service tickets and notes."""

from __future__ import annotations

import base64
import logging
from datetime import datetime

import httpx
from pydantic import BaseModel, ConfigDict, Field

from app.settings import Settings

logger = logging.getLogger(__name__)


class CwStatus(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    id: int
    name: str | None = None


class CwTicket(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    id: int
    summary: str | None = None
    external_reference: str | None = Field(default=None, alias="externalReference")
    closed_flag: bool = Field(default=False, alias="closedFlag")
    status: CwStatus | None = None


class CwNoteInfo(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    last_updated: datetime | None = Field(default=None, alias="lastUpdated")
    updated_by: str | None = Field(default=None, alias="updatedBy")


class CwTicketNote(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    id: int
    text: str | None = None
    detail_description_flag: bool = Field(default=False, alias="detailDescriptionFlag")
    internal_analysis_flag: bool = Field(default=False, alias="internalAnalysisFlag")
    info: CwNoteInfo | None = Field(default=None, alias="_info")


class ConnectWiseClient:
    def __init__(self, settings: Settings, client: httpx.AsyncClient | None = None):
        token = base64.b64encode(
            f"{settings.cw_company_id}+{settings.cw_public_key}:{settings.cw_private_key}".encode("utf-8")
        ).decode("ascii")
        self._client = client or httpx.AsyncClient(
            base_url=settings.cw_base_url.rstrip("/") + "/",
            timeout=30.0,
            headers={
                "Accept": settings.cw_api_version_header,
                "Authorization": f"Basic {token}",
                "clientId": settings.cw_client_id,
            },
        )

    async def aclose(self) -> None:
        await self._client.aclose()

    async def get_ticket(self, ticket_id: int) -> CwTicket | None:
        response = await self._client.get(f"service/tickets/{ticket_id}")
        if not response.is_success:
            logger.warning("Failed to fetch ticket %s: %s", ticket_id, response.status_code)
            return None
        return CwTicket.model_validate(response.json())

    async def get_ticket_notes(self, ticket_id: int, page_size: int = 50) -> list[CwTicketNote]:
        response = await self._client.get(
            f"service/tickets/{ticket_id}/notes",
            params={"orderBy": "id desc", "pageSize": str(page_size)},
        )
        response.raise_for_status()
        return [CwTicketNote.model_validate(item) for item in response.json()]
