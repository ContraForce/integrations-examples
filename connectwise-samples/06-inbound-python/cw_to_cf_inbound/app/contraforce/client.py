"""
Minimal client for the ContraForce public REST API.
Authenticates with a service-account credential (HTTP Basic).
"""

from __future__ import annotations

import base64
import logging

import httpx

from app.settings import Settings

logger = logging.getLogger(__name__)


class ContraForceClient:
    def __init__(self, settings: Settings, client: httpx.AsyncClient | None = None):
        self._settings = settings
        token = base64.b64encode(
            f"{settings.cf_service_account_client_id}:{settings.cf_service_account_client_secret}".encode("utf-8")
        ).decode("ascii")
        self._client = client or httpx.AsyncClient(
            base_url=settings.cf_api_base_url.rstrip("/") + "/",
            timeout=30.0,
            headers={"Authorization": f"Basic {token}"},
        )

    async def aclose(self) -> None:
        await self._client.aclose()

    async def add_incident_comment(self, incident_id: str, source: str, content: str) -> None:
        path = f"workspaces/{self._settings.cf_workspace_id}/incidents/{incident_id}/comments"
        body = {
            "incidentId": incident_id,
            "commentId": None,
            "content": content,
            "extensionId": None,
            "source": source,
        }
        response = await self._client.post(path, json=body)
        if not response.is_success:
            logger.error(
                "ContraForce call to %s failed: %s — %s",
                path, response.status_code, response.text,
            )
        response.raise_for_status()

    async def close_incident(self, incident_id: str, source: str, closing_comment: str | None) -> None:
        path = f"workspaces/{self._settings.cf_workspace_id}/incidents/{incident_id}/status"
        # Classification + ClassificationReason are required when closing
        # Sentinel incidents — tweak if your board workflow drives different
        # classifications. "Undetermined" / "InaccurateData" are the safest
        # neutral defaults for a CW-driven close.
        body = {
            "incidentId": incident_id,
            "source": source,
            "status": "Closed",
            "comment": closing_comment,
            "classification": "Undetermined",
            "classificationReason": "InaccurateData",
        }
        response = await self._client.put(path, json=body)
        if not response.is_success:
            logger.error(
                "ContraForce call to %s failed: %s — %s",
                path, response.status_code, response.text,
            )
        response.raise_for_status()
