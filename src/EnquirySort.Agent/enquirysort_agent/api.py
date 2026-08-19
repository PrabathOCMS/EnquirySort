from __future__ import annotations

from typing import Any, Literal

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from enquirysort_agent.config import settings
from enquirysort_agent.graph import triage_graph
from enquirysort_agent.state import empty_result

app = FastAPI(title="EnquirySort Agent", version="0.1.0")


class MailingListDto(BaseModel):
    name: str
    address: str = ""
    description: str | None = None


class KnowledgeArticleDto(BaseModel):
    id: str
    title: str
    slug: str = ""
    content: str = ""


class TriageRequest(BaseModel):
    subject: str = ""
    body: str = ""
    from_address: str = ""
    mailing_lists: list[MailingListDto] = Field(default_factory=list)
    knowledge_articles: list[KnowledgeArticleDto] = Field(default_factory=list)
    response_rules: str | None = None


class TriageResponse(BaseModel):
    action: Literal["respond", "route", "ignore"]
    confidence: float
    reason: str
    mailing_list: str | None = None
    customer_question: str | None = None
    draft_reply: str | None = None
    retrieved_article_ids: list[str] = Field(default_factory=list)
    error: str | None = None


@app.get("/health")
def health() -> dict[str, str]:
    return {
        "status": "ok",
        "provider": "bedrock",
        "model": settings.bedrock_model_id,
        "region": settings.aws_region,
    }


@app.post("/triage", response_model=TriageResponse)
def triage(req: TriageRequest) -> TriageResponse:
    initial: dict[str, Any] = {
        **empty_result(),
        "subject": req.subject,
        "body": req.body,
        "from_address": req.from_address,
        "mailing_lists": [m.model_dump() for m in req.mailing_lists],
        "knowledge_articles": [a.model_dump() for a in req.knowledge_articles],
        "response_rules": req.response_rules or "",
    }

    try:
        final = triage_graph.invoke(initial)
    except Exception as ex:  # noqa: BLE001 - surface Bedrock/auth errors to API caller
        raise HTTPException(status_code=502, detail=f"Agent triage failed: {ex}") from ex

    retrieved = final.get("retrieved_articles") or []
    action = str(final.get("action") or "ignore")
    if action not in {"respond", "route", "ignore"}:
        action = "ignore"

    return TriageResponse(
        action=action,  # type: ignore[arg-type]
        confidence=float(final.get("confidence") or 0),
        reason=str(final.get("reason") or ""),
        mailing_list=final.get("mailing_list"),
        customer_question=final.get("customer_question"),
        draft_reply=final.get("draft_reply"),
        retrieved_article_ids=[str(a.get("id")) for a in retrieved if a.get("id")],
        error=final.get("error"),
    )


def main() -> None:
    import uvicorn

    uvicorn.run(
        "enquirysort_agent.api:app",
        host=settings.agent_host,
        port=settings.agent_port,
        reload=False,
    )


if __name__ == "__main__":
    main()
