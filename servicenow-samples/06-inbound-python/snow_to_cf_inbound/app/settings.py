from uuid import UUID

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    snow_instance_url: str = Field(..., alias="SNOW_INSTANCE_URL")
    snow_username: str = Field(..., alias="SNOW_USERNAME")
    snow_password: str = Field(..., alias="SNOW_PASSWORD")
    snow_callback_secret: str = Field(..., alias="SNOW_CALLBACK_SECRET")
    snow_resolved_state: int = Field(6, alias="SNOW_RESOLVED_STATE")
    snow_closed_state: int = Field(7, alias="SNOW_CLOSED_STATE")
    snow_forward_work_notes: bool = Field(False, alias="SNOW_FORWARD_WORK_NOTES")
    snow_integration_user: str | None = Field(None, alias="SNOW_INTEGRATION_USER")

    cf_api_base_url: str = Field(..., alias="CF_API_BASE_URL")
    cf_service_account_client_id: str = Field(..., alias="CF_SERVICE_ACCOUNT_CLIENT_ID")
    cf_service_account_client_secret: str = Field(..., alias="CF_SERVICE_ACCOUNT_CLIENT_SECRET")
    cf_workspace_id: UUID = Field(..., alias="CF_WORKSPACE_ID")
