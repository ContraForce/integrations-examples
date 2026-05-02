from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    cf_webhook_secret: str = Field(..., alias="CF_WEBHOOK_SECRET")
    cf_max_skew_seconds: int = Field(300, alias="CF_MAX_SKEW_SECONDS")

    halo_auth_url: str = Field(..., alias="HALO_AUTH_URL")
    halo_api_base_url: str = Field(..., alias="HALO_API_BASE_URL")
    halo_client_id: str = Field(..., alias="HALO_CLIENT_ID")
    halo_client_secret: str = Field(..., alias="HALO_CLIENT_SECRET")
    halo_tenant: str | None = Field(None, alias="HALO_TENANT")
    halo_scope: str = Field("all", alias="HALO_SCOPE")
    halo_default_tickettype_id: int = Field(..., alias="HALO_DEFAULT_TICKETTYPE_ID")
    halo_default_client_id: int = Field(..., alias="HALO_DEFAULT_CLIENT_ID")
    halo_external_ref_field_id: int | None = Field(None, alias="HALO_EXTERNAL_REF_FIELD_ID")
