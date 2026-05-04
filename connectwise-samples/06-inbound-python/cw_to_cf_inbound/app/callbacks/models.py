from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field


class CwCallbackPayload(BaseModel):
    """Minimal shape of a ConnectWise Manage Callback payload."""

    model_config = ConfigDict(populate_by_name=True)

    id: int = Field(alias="ID")
    type: str = Field(alias="Type")
    action: str = Field(alias="Action")
    member_id: str | None = Field(default=None, alias="MemberID")
    object_id: int = Field(alias="ObjectID")
