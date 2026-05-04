"""Thin HaloPSA REST client for the subset used by this sample."""

from __future__ import annotations

import logging
from typing import Any

import httpx

from app.halo.token_provider import HaloTokenProvider
from app.settings import Settings

logger = logging.getLogger(__name__)


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

    async def find_ticket_by_external_reference(self, external_reference: str) -> int | None:
        """Return the id of an existing ticket, or None."""
        if self._settings.halo_external_ref_field_id is not None:
            params = {
                "count": "1",
                f"searchcustomfield_{self._settings.halo_external_ref_field_id}": external_reference,
            }
        else:
            params = {"count": "1", "thirdpartynumber": external_reference}

        response = await self._client.get("Tickets", params=params, headers=await self._auth_headers())
        response.raise_for_status()

        payload = response.json()
        # Halo returns either an array directly or {"tickets": [...]} depending
        # on query params; handle both.
        items = payload if isinstance(payload, list) else payload.get("tickets", [])
        if not items:
            return None
        return int(items[0]["id"])

    async def upsert_ticket(self, ticket: dict[str, Any]) -> int:
        """POST /api/Tickets accepts an array body even for a single ticket."""
        response = await self._client.post(
            "Tickets",
            json=[ticket],
            headers=await self._auth_headers(),
        )
        if not response.is_success:
            logger.error("Halo upsert_ticket failed: %s — %s", response.status_code, response.text)
        response.raise_for_status()

        payload = response.json()
        items = payload if isinstance(payload, list) else [payload]
        if not items:
            raise RuntimeError("Halo returned empty array for ticket upsert")
        ticket_id = int(items[0]["id"])
        logger.info(
            "Halo ticket %s (extRef %s) -> id %s",
            "updated" if "id" in ticket else "created",
            ticket.get("thirdpartynumber"),
            ticket_id,
        )
        return ticket_id

    async def add_private_note(self, ticket_id: int, note_html: str) -> None:
        action = {
            "ticket_id": ticket_id,
            "outcome": "Private Note",
            "note_html": note_html,
            "hiddenfromuser": True,
        }
        response = await self._client.post(
            "Actions",
            json=[action],
            headers=await self._auth_headers(),
        )
        if not response.is_success:
            logger.error("Halo add_private_note failed: %s — %s", response.status_code, response.text)
        response.raise_for_status()
