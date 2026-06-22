"""Minimal ServiceNow Business Rule callback payload — only the sys_id matters."""

from __future__ import annotations

from pydantic import BaseModel, ConfigDict


class SnowCallbackPayload(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="allow")

    sys_id: str | None = None
    number: str | None = None
