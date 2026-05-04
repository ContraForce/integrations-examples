from uuid import UUID

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    halo_auth_url: str = Field(..., alias="HALO_AUTH_URL")
    halo_api_base_url: str = Field(..., alias="HALO_API_BASE_URL")
    halo_client_id: str = Field(..., alias="HALO_CLIENT_ID")
    halo_client_secret: str = Field(..., alias="HALO_CLIENT_SECRET")
    halo_tenant: str | None = Field(None, alias="HALO_TENANT")
    halo_scope: str = Field("all", alias="HALO_SCOPE")
    halo_webhook_secret: str = Field(..., alias="HALO_WEBHOOK_SECRET")
    halo_closed_status_id: int = Field(..., alias="HALO_CLOSED_STATUS_ID")
    halo_external_ref_field_id: int | None = Field(None, alias="HALO_EXTERNAL_REF_FIELD_ID")
    halo_integration_agent_id: int | None = Field(None, alias="HALO_INTEGRATION_AGENT_ID")

    cf_api_base_url: str = Field(..., alias="CF_API_BASE_URL")
    cf_service_account_client_id: str = Field(..., alias="CF_SERVICE_ACCOUNT_CLIENT_ID")
    cf_service_account_client_secret: str = Field(..., alias="CF_SERVICE_ACCOUNT_CLIENT_SECRET")
    cf_workspace_id: UUID = Field(..., alias="CF_WORKSPACE_ID")
