from __future__ import annotations

from pathlib import Path
from typing import Any

import yaml
from pydantic import BaseModel, Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class MailingList(BaseModel):
    name: str
    address: str
    description: str = ""


class AppConfig(BaseModel):
    mailing_lists: list[MailingList] = Field(default_factory=list)
    respond_confidence_threshold: float = 0.65
    route_confidence_threshold: float = 0.55
    processed_folder: str = "EnquirySort/Processed"
    knowledge_base_dir: Path = Path("knowledge_base")
    max_body_chars: int = 8000
    poll_interval_seconds: int = 60

    @field_validator("knowledge_base_dir", mode="before")
    @classmethod
    def _as_path(cls, value: Any) -> Path:
        return Path(value)


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # IMAP / SMTP
    imap_host: str = "imap.gmail.com"
    imap_port: int = 993
    smtp_host: str = "smtp.gmail.com"
    smtp_port: int = 587
    email_address: str = ""
    email_password: str = ""
    mailbox: str = "INBOX"

    # OpenRouter
    openrouter_api_key: str = ""
    openrouter_model: str = "openai/gpt-4o-mini"
    openrouter_base_url: str = "https://openrouter.ai/api/v1"
    openrouter_site_url: str = "https://github.com/PrabathOCMS/EnquirySort"
    openrouter_app_name: str = "EnquirySort"

    # Behaviour
    dry_run: bool = False
    config_path: Path = Path("config.yaml")
    once: bool = False

    def require_credentials(self) -> None:
        missing = [
            name
            for name, value in (
                ("EMAIL_ADDRESS", self.email_address),
                ("EMAIL_PASSWORD", self.email_password),
                ("OPENROUTER_API_KEY", self.openrouter_api_key),
            )
            if not value
        ]
        if missing:
            raise ValueError(
                "Missing required settings: " + ", ".join(missing)
            )


def load_app_config(path: Path) -> AppConfig:
    if not path.exists():
        return AppConfig()
    raw = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    return AppConfig.model_validate(raw)
