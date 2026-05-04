"""Thin ConnectWise Manage REST client for the subset used by this sample."""

from __future__ import annotations

import base64
import logging
from typing import Any

import httpx

from app.settings import Settings

logger = logging.getLogger(__name__)


class ConnectWiseClient:
    def __init__(self, settings: Settings, client: httpx.AsyncClient | None = None):
        self._settings = settings
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

    async def find_ticket_by_external_reference(self, external_reference: str) -> int | None:
        """Return ticket id matching `externalReference`, or None."""
        escaped = external_reference.replace('"', '\\"')
        params = {
            "pageSize": "1",
            "conditions": f'externalReference = "{escaped}"',
        }
        response = await self._client.get("service/tickets", params=params)
        response.raise_for_status()
        tickets = response.json()
        if tickets and isinstance(tickets, list):
            return int(tickets[0]["id"])
        return None

    async def create_ticket(self, payload: dict[str, Any]) -> int:
        response = await self._client.post("service/tickets", json=payload)
        if not response.is_success:
            logger.error("CW create_ticket failed: %s %s", response.status_code, response.text)
        response.raise_for_status()
        body = response.json()
        ticket_id = int(body["id"])
        logger.info(
            "Created ConnectWise ticket %s with externalReference %s",
            ticket_id,
            payload.get("externalReference"),
        )
        return ticket_id

    async def add_note(self, ticket_id: int, text: str) -> None:
        body = {
            "text": text,
            "detailDescriptionFlag": True,
            "customerUpdatedFlag": False,
            "internalAnalysisFlag": True,
        }
        response = await self._client.post(f"service/tickets/{ticket_id}/notes", json=body)
        if not response.is_success:
            logger.error("CW add_note failed: %s %s", response.status_code, response.text)
        response.raise_for_status()
