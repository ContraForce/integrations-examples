from uuid import UUID

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    cw_base_url: str = Field(..., alias="CW_BASE_URL")
    cw_company_id: str = Field(..., alias="CW_COMPANY_ID")
    cw_public_key: str = Field(..., alias="CW_PUBLIC_KEY")
    cw_private_key: str = Field(..., alias="CW_PRIVATE_KEY")
    cw_client_id: str = Field(..., alias="CW_CLIENT_ID")
    cw_callback_secret: str = Field(..., alias="CW_CALLBACK_SECRET")
    cw_api_version_header: str = Field(
        "application/vnd.connectwise.com+json; version=2020.1",
        alias="CW_API_VERSION_HEADER",
    )

    cf_api_base_url: str = Field(..., alias="CF_API_BASE_URL")
    cf_service_account_client_id: str = Field(..., alias="CF_SERVICE_ACCOUNT_CLIENT_ID")
    cf_service_account_client_secret: str = Field(..., alias="CF_SERVICE_ACCOUNT_CLIENT_SECRET")
    cf_workspace_id: UUID = Field(..., alias="CF_WORKSPACE_ID")
