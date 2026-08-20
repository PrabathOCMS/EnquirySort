from __future__ import annotations

from typing import Any, Literal, TypedDict


class MailingListInfo(TypedDict, total=False):
    name: str
    address: str
    description: str


class KnowledgeArticleInfo(TypedDict, total=False):
    id: str
    title: str
    slug: str
    content: str


class TriageState(TypedDict, total=False):
    subject: str
    body: str
    from_address: str
    mailing_lists: list[MailingListInfo]
    knowledge_articles: list[KnowledgeArticleInfo]
    response_rules: str

    action: Literal["respond", "route", "ignore"]
    confidence: float
    reason: str
    mailing_list: str | None
    customer_question: str | None

    retrieved_articles: list[KnowledgeArticleInfo]
    draft_reply: str | None
    error: str | None


def empty_result() -> dict[str, Any]:
    return {
        "action": "ignore",
        "confidence": 0.0,
        "reason": "No classification produced",
        "mailing_list": None,
        "customer_question": None,
        "retrieved_articles": [],
        "draft_reply": None,
        "error": None,
    }
