from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    cf_webhook_secret: str = Field(..., alias="CF_WEBHOOK_SECRET")
    cf_max_skew_seconds: int = Field(300, alias="CF_MAX_SKEW_SECONDS")

    snow_instance_url: str = Field(..., alias="SNOW_INSTANCE_URL")
    snow_username: str = Field(..., alias="SNOW_USERNAME")
    snow_password: str = Field(..., alias="SNOW_PASSWORD")
    snow_default_impact: int = Field(2, alias="SNOW_DEFAULT_IMPACT")
    snow_assignment_group_sys_id: str | None = Field(None, alias="SNOW_ASSIGNMENT_GROUP_SYS_ID")
    snow_caller_sys_id: str | None = Field(None, alias="SNOW_CALLER_SYS_ID")
