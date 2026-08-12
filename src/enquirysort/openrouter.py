from __future__ import annotations

import json
import logging
import re
from typing import Any

import httpx

from enquirysort.config import AppConfig, Settings
from enquirysort.models import Action, Classification, EmailMessage, KnowledgeSnippet

logger = logging.getLogger(__name__)


class OpenRouterClient:
    def __init__(self, settings: Settings) -> None:
        self.settings = settings

    def _headers(self) -> dict[str, str]:
        return {
            "Authorization": f"Bearer {self.settings.openrouter_api_key}",
            "Content-Type": "application/json",
            "HTTP-Referer": self.settings.openrouter_site_url,
            "X-Title": self.settings.openrouter_app_name,
        }

    def chat(
        self,
        messages: list[dict[str, str]],
        *,
        temperature: float = 0.2,
        response_format: dict[str, Any] | None = None,
    ) -> str:
        payload: dict[str, Any] = {
            "model": self.settings.openrouter_model,
            "messages": messages,
            "temperature": temperature,
        }
        if response_format is not None:
            payload["response_format"] = response_format

        url = f"{self.settings.openrouter_base_url.rstrip('/')}/chat/completions"
        with httpx.Client(timeout=90.0) as client:
            response = client.post(url, headers=self._headers(), json=payload)
            response.raise_for_status()
            data = response.json()

        try:
            return data["choices"][0]["message"]["content"]
        except (KeyError, IndexError, TypeError) as exc:
            raise RuntimeError(f"Unexpected OpenRouter response: {data}") from exc

    def classify(self, message: EmailMessage, app_config: AppConfig) -> Classification:
        lists_block = "\n".join(
            f"- name: {ml.name}\n  address: {ml.address}\n  description: {ml.description}"
            for ml in app_config.mailing_lists
        ) or "(no mailing lists configured)"

        system = (
            "You are EnquirySort, an email triage assistant for a CMS/support company. "
            "Decide whether an inbound email should be answered automatically from a "
            "knowledge base, forwarded to an internal mailing list, or ignored "
            "(spam/noise/out-of-office).\n\n"
            "Return ONLY valid JSON with this schema:\n"
            "{\n"
            '  "action": "respond" | "route" | "ignore",\n'
            '  "confidence": number between 0 and 1,\n'
            '  "reason": string,\n'
            '  "mailing_list": string | null,  // mailing list name when action=route\n'
            '  "customer_question": string | null  // concise question when action=respond\n'
            "}\n\n"
            "Use action=respond for FAQs, product/how-to questions, pricing basics, "
            "and other questions answerable from documentation.\n"
            "Use action=route for sales opportunities, custom quotes, bugs, billing "
            "disputes, legal, or anything needing a human team.\n"
            "Pick the best mailing_list name from the configured list when routing."
        )
        user = (
            f"Configured mailing lists:\n{lists_block}\n\n"
            f"From: {message.from_address}\n"
            f"Subject: {message.subject}\n"
            f"Body:\n{message.body_text[: app_config.max_body_chars]}"
        )
        content = self.chat(
            [
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
            temperature=0.1,
        )
        data = _parse_json_object(content)
        classification = Classification.from_dict(data)
        logger.info(
            "Classified uid=%s action=%s confidence=%.2f list=%s",
            message.uid,
            classification.action.value,
            classification.confidence,
            classification.mailing_list,
        )
        return classification

    def draft_reply(
        self,
        message: EmailMessage,
        snippets: list[KnowledgeSnippet],
        question: str | None = None,
    ) -> str:
        kb_block = "\n\n---\n\n".join(
            f"# {snip.title} ({snip.path})\n{snip.content}" for snip in snippets
        ) or "(no knowledge base snippets matched)"

        system = (
            "You write helpful, concise customer-support email replies. "
            "Use only the provided knowledge base. If the knowledge base does not "
            "contain enough information, say so briefly and offer that a human will follow up. "
            "Do not invent product facts. Write plain text only — no markdown fences. "
            "Sign off as EnquirySort Support."
        )
        user = (
            f"Customer question summary: {question or message.subject}\n\n"
            f"Original email:\nFrom: {message.from_address}\n"
            f"Subject: {message.subject}\n"
            f"Body:\n{message.body_text[:6000]}\n\n"
            f"Knowledge base excerpts:\n{kb_block}"
        )
        return self.chat(
            [
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
            temperature=0.3,
        ).strip()


def _parse_json_object(text: str) -> dict[str, Any]:
    text = text.strip()
    try:
        data = json.loads(text)
        if isinstance(data, dict):
            return data
    except json.JSONDecodeError:
        pass

    match = re.search(r"\{.*\}", text, flags=re.DOTALL)
    if not match:
        return {
            "action": Action.IGNORE.value,
            "confidence": 0.0,
            "reason": f"Unparseable model output: {text[:200]}",
            "mailing_list": None,
            "customer_question": None,
        }
    try:
        data = json.loads(match.group(0))
        if isinstance(data, dict):
            return data
    except json.JSONDecodeError:
        pass
    return {
        "action": Action.IGNORE.value,
        "confidence": 0.0,
        "reason": f"Unparseable model output: {text[:200]}",
        "mailing_list": None,
        "customer_question": None,
    }
