"""Loose Halo webhook payload — only the ticket id matters."""

from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field


class HaloWebhookPayload(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="allow")

    id: int | None = None
    ticket_id: int | None = Field(default=None, alias="ticket_id")

    def resolve_ticket_id(self) -> int | None:
        return self.ticket_id or self.id
