from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    cf_webhook_secret: str = Field(..., alias="CF_WEBHOOK_SECRET")
    cf_max_skew_seconds: int = Field(300, alias="CF_MAX_SKEW_SECONDS")

    cw_base_url: str = Field(..., alias="CW_BASE_URL")
    cw_company_id: str = Field(..., alias="CW_COMPANY_ID")
    cw_public_key: str = Field(..., alias="CW_PUBLIC_KEY")
    cw_private_key: str = Field(..., alias="CW_PRIVATE_KEY")
    cw_client_id: str = Field(..., alias="CW_CLIENT_ID")
    cw_default_board_id: int = Field(..., alias="CW_DEFAULT_BOARD_ID")
    cw_default_company_identifier: str = Field(..., alias="CW_DEFAULT_COMPANY_IDENTIFIER")
    cw_api_version_header: str = Field(
        "application/vnd.connectwise.com+json; version=2020.1",
        alias="CW_API_VERSION_HEADER",
    )
