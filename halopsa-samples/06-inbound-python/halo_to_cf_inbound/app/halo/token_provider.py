"""Caches the Halo OAuth2 client_credentials token; refreshes 60s before expiry."""

from __future__ import annotations

import asyncio
import logging
import time

import httpx

from app.settings import Settings

logger = logging.getLogger(__name__)


class HaloTokenProvider:
    def __init__(self, settings: Settings, client: httpx.AsyncClient | None = None):
        self._settings = settings
        self._client = client or httpx.AsyncClient(timeout=30.0)
        self._token: str | None = None
        self._expires_at: float = 0.0
        self._lock = asyncio.Lock()

    async def aclose(self) -> None:
        await self._client.aclose()

    async def get_token(self) -> str:
        if self._token is not None and time.time() < self._expires_at - 60:
            return self._token

        async with self._lock:
            if self._token is not None and time.time() < self._expires_at - 60:
                return self._token

            url = self._settings.halo_auth_url.rstrip("/") + "/token"
            form = {
                "grant_type": "client_credentials",
                "client_id": self._settings.halo_client_id,
                "client_secret": self._settings.halo_client_secret,
                "scope": self._settings.halo_scope,
            }
            if self._settings.halo_tenant:
                form["tenant"] = self._settings.halo_tenant

            response = await self._client.post(
                url,
                data=form,
                headers={"Content-Type": "application/x-www-form-urlencoded"},
            )
            if not response.is_success:
                logger.error("Halo token request failed: %s — %s", response.status_code, response.text)
            response.raise_for_status()

            payload = response.json()
            self._token = payload["access_token"]
            expires_in = int(payload.get("expires_in", 3600))
            self._expires_at = time.time() + expires_in
            return self._token
