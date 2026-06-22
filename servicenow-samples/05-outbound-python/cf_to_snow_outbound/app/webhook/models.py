from __future__ import annotations

from datetime import datetime
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class Owner(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    display_name: str | None = Field(default=None, alias="displayName")
    email: str | None = None


class Alert(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    title: str
    severity: str
    product_name: str = Field(alias="productName")
    vendor_name: str = Field(alias="vendorName")


class Entity(BaseModel):
    type: str
    display_name: str = Field(alias="displayName")

    model_config = ConfigDict(populate_by_name=True)


class IncidentCreatedPayload(BaseModel):
    """Payload shape for `incident.created.v1`."""

    model_config = ConfigDict(populate_by_name=True)

    account_id: UUID = Field(alias="accountId")
    account_name: str = Field(alias="accountName")
    incident_id: str = Field(alias="incidentId")
    incident_number: int = Field(alias="incidentNumber")
    title: str
    description: str | None = None
    severity: str
    source: str
    source_display_name: str = Field(alias="sourceDisplayName")
    owner: Owner | None = None
    created_at: datetime = Field(alias="createdAt")
    last_activity_at: datetime = Field(alias="lastActivityAt")
    occurred_at: datetime = Field(alias="occurredAt")
    alert_product_names: list[str] = Field(default_factory=list, alias="alertProductNames")
    alerts: list[Alert] = Field(default_factory=list)
    entities: list[Entity] = Field(default_factory=list)


class WebhookEnvelope(BaseModel):
    """Top-level envelope ContraForce sends for every webhook event."""

    model_config = ConfigDict(populate_by_name=True)

    type: str
    timestamp: datetime
    is_test: bool = Field(alias="isTest")
    occurred_at: datetime = Field(alias="occurredAt")
    data: IncidentCreatedPayload | None = None
