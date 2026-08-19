from __future__ import annotations

import json
import re
from typing import Any

from langchain_core.messages import HumanMessage, SystemMessage

from enquirysort_agent.llm import build_chat_model
from enquirysort_agent.state import TriageState


def _parse_json(text: str) -> dict[str, Any]:
    text = (text or "").strip()
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        start = text.find("{")
        end = text.rfind("}")
        if start >= 0 and end > start:
            try:
                return json.loads(text[start : end + 1])
            except json.JSONDecodeError:
                return {}
        return {}


def classify_node(state: TriageState) -> dict[str, Any]:
    lists = state.get("mailing_lists") or []
    lists_block = (
        "(no mailing lists configured)"
        if not lists
        else "\n".join(
            f"- name: {item.get('name')}\n  address: {item.get('address')}\n  description: {item.get('description')}"
            for item in lists
        )
    )

    system = """
You are EnquirySort, an email triage assistant. Decide whether an inbound email should be
answered from a FAQ knowledge base, forwarded to an internal team/department mailing list, or ignored.
Return ONLY valid JSON:
{
  "action": "respond" | "route" | "ignore",
  "confidence": number between 0 and 1,
  "reason": string,
  "mailing_list": string | null,
  "customer_question": string | null
}
Use respond for FAQs/how-tos answerable from docs.
Use route for sales, bugs, billing, legal, or anything needing a human team.
Use ignore for spam, OOO, or noise.
""".strip()

    user = (
        f"Configured mailing lists:\n{lists_block}\n\n"
        f"From: {state.get('from_address')}\n"
        f"Subject: {state.get('subject')}\n"
        f"Body:\n{(state.get('body') or '')[:8000]}"
    )

    model = build_chat_model(temperature=0.0)
    response = model.invoke([SystemMessage(content=system), HumanMessage(content=user)])
    raw = getattr(response, "content", "")
    if isinstance(raw, list):
        raw = "".join(str(part.get("text", part) if isinstance(part, dict) else part) for part in raw)
    data = _parse_json(str(raw))

    action = str(data.get("action") or "ignore").strip().lower()
    if action not in {"respond", "route", "ignore"}:
        action = "ignore"

    return {
        "action": action,
        "confidence": float(data.get("confidence") or 0),
        "reason": str(data.get("reason") or "Unparseable model output"),
        "mailing_list": data.get("mailing_list"),
        "customer_question": data.get("customer_question"),
    }


def retrieve_node(state: TriageState) -> dict[str, Any]:
    """Select relevant KB articles (LLM ranking for now; swap for vector RAG later)."""
    catalog = state.get("knowledge_articles") or []
    if not catalog:
        return {"retrieved_articles": []}

    if len(catalog) <= 3:
        return {"retrieved_articles": catalog}

    catalog_block = "\n\n".join(
        f"- id: {a.get('id')}\n  title: {a.get('title')}\n  slug: {a.get('slug')}\n  summary: {(a.get('content') or '')[:280]}"
        for a in catalog
    )
    question = state.get("customer_question") or state.get("subject") or ""

    system = """
You select knowledge-base articles that can answer a customer email.
Return ONLY valid JSON:
{ "article_ids": ["id", "..."], "reason": string }
Pick at most 3 articles. If nothing is relevant, return an empty article_ids array.
Only use ids from the catalog.
""".strip()

    user = (
        f"Customer question summary: {question}\n\n"
        f"Subject: {state.get('subject')}\nBody:\n{(state.get('body') or '')[:4000]}\n\n"
        f"Knowledge catalog:\n{catalog_block}"
    )

    model = build_chat_model(temperature=0.0)
    response = model.invoke([SystemMessage(content=system), HumanMessage(content=user)])
    raw = getattr(response, "content", "")
    if isinstance(raw, list):
        raw = "".join(str(part.get("text", part) if isinstance(part, dict) else part) for part in raw)
    data = _parse_json(str(raw))
    wanted = {str(x) for x in (data.get("article_ids") or [])}
    selected = [a for a in catalog if str(a.get("id")) in wanted][:3]
    return {"retrieved_articles": selected}


_SIGN_OFF_RE = re.compile(
    r"(?:\r?\n|\r)[ \t]*"
    r"(?:best(?:\s+regards)?|kind\s+regards|warm\s+regards|regards|sincerely|"
    r"yours\s+(?:sincerely|faithfully)|thanks(?:\s+again)?|thank\s+you|cheers)"
    r"\s*[,!]?\s*"
    r"(?:(?:\r?\n|\r)[ \t]*[^\r\n]{0,80}){0,3}"
    r"\s*\Z",
    re.IGNORECASE,
)


def _strip_signoff(text: str) -> str:
    text = (text or "").strip()
    match = _SIGN_OFF_RE.search(text)
    if not match:
        return text
    return text[: match.start()].rstrip()


def apply_rules_and_draft_node(state: TriageState) -> dict[str, Any]:
    snippets = state.get("retrieved_articles") or []
    kb_block = (
        "(no knowledge base snippets matched)"
        if not snippets
        else "\n\n---\n\n".join(
            f"# {a.get('title')} ({a.get('slug')})\n{a.get('content')}" for a in snippets
        )
    )
    rules = (state.get("response_rules") or "").strip() or (
        "Be concise and professional. Use only the knowledge base. Include exact URLs from the knowledge base."
    )
    question = state.get("customer_question") or state.get("subject") or ""

    system = f"""
You write helpful, concise customer-support email replies.
Follow these response rules exactly:
{rules}

Use ONLY the provided knowledge base excerpts.
When the knowledge base includes a URL or concrete steps, include them exactly.
Do not invent product URLs, policies, or steps that are not in the excerpts.
If no useful excerpts are provided, say you could not find matching documentation and
that a human will follow up — do not guess a generic how-to.
Plain text only.
Do NOT include any sign-off, closing, or signature. The email signature is appended separately.
""".strip()

    user = (
        f"Customer question summary: {question}\n\n"
        f"Original email:\nFrom: {state.get('from_address')}\n"
        f"Subject: {state.get('subject')}\nBody:\n{(state.get('body') or '')[:6000]}\n\n"
        f"Knowledge base excerpts:\n{kb_block}"
    )

    model = build_chat_model(temperature=0.3)
    response = model.invoke([SystemMessage(content=system), HumanMessage(content=user)])
    raw = getattr(response, "content", "")
    if isinstance(raw, list):
        raw = "".join(str(part.get("text", part) if isinstance(part, dict) else part) for part in raw)
    return {"draft_reply": _strip_signoff(str(raw))}
