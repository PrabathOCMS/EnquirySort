from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class Action(str, Enum):
    RESPOND = "respond"
    ROUTE = "route"
    IGNORE = "ignore"


@dataclass(slots=True)
class EmailMessage:
    uid: str
    message_id: str
    subject: str
    from_address: str
    to_addresses: list[str]
    body_text: str
    raw_headers: dict[str, str] = field(default_factory=dict)

    def preview(self, limit: int = 500) -> str:
        body = self.body_text.strip().replace("\r\n", "\n")
        if len(body) > limit:
            body = body[:limit] + "…"
        return body


@dataclass(slots=True)
class Classification:
    action: Action
    confidence: float
    reason: str
    mailing_list: str | None = None
    customer_question: str | None = None

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> Classification:
        action_raw = str(data.get("action", "ignore")).lower().strip()
        try:
            action = Action(action_raw)
        except ValueError:
            action = Action.IGNORE
        return cls(
            action=action,
            confidence=float(data.get("confidence", 0.0)),
            reason=str(data.get("reason", "")),
            mailing_list=data.get("mailing_list"),
            customer_question=data.get("customer_question"),
        )


@dataclass(slots=True)
class KnowledgeSnippet:
    path: str
    title: str
    content: str
    score: float = 0.0


@dataclass(slots=True)
class ProcessResult:
    uid: str
    action: Action
    detail: str
    reply_sent: bool = False
    routed_to: str | None = None
