from __future__ import annotations

import logging
from typing import Protocol

from enquirysort.config import AppConfig, Settings
from enquirysort.email_client import EmailClient
from enquirysort.knowledge import KnowledgeBase
from enquirysort.models import Action, Classification, EmailMessage, ProcessResult
from enquirysort.openrouter import OpenRouterClient

logger = logging.getLogger(__name__)


class MailTransport(Protocol):
    def send_reply(self, original: EmailMessage, body: str, *, subject_prefix: str = "Re: ") -> None: ...
    def forward_to_list(self, original: EmailMessage, list_address: str, *, note: str = "") -> None: ...
    def mark_seen(self, uid: str) -> None: ...
    def move_to_folder(self, uid: str, folder: str) -> None: ...


class EnquiryPipeline:
    def __init__(
        self,
        settings: Settings,
        app_config: AppConfig,
        openrouter: OpenRouterClient,
        knowledge: KnowledgeBase,
        mail: MailTransport,
    ) -> None:
        self.settings = settings
        self.app_config = app_config
        self.openrouter = openrouter
        self.knowledge = knowledge
        self.mail = mail

    def process_message(self, message: EmailMessage) -> ProcessResult:
        classification = self.openrouter.classify(message, self.app_config)
        classification = self._apply_thresholds(classification)

        if classification.action == Action.RESPOND:
            return self._handle_respond(message, classification)
        if classification.action == Action.ROUTE:
            return self._handle_route(message, classification)

        detail = f"Ignored: {classification.reason}"
        self._finalize(message)
        return ProcessResult(uid=message.uid, action=Action.IGNORE, detail=detail)

    def process_many(self, messages: list[EmailMessage]) -> list[ProcessResult]:
        results: list[ProcessResult] = []
        for message in messages:
            try:
                result = self.process_message(message)
            except Exception as exc:
                logger.exception("Failed processing uid=%s", message.uid)
                results.append(
                    ProcessResult(
                        uid=message.uid,
                        action=Action.IGNORE,
                        detail=f"Error: {exc}",
                    )
                )
                continue
            results.append(result)
        return results

    def _apply_thresholds(self, classification: Classification) -> Classification:
        if (
            classification.action == Action.RESPOND
            and classification.confidence < self.app_config.respond_confidence_threshold
        ):
            logger.info(
                "Respond confidence %.2f below threshold; falling back to route/ignore",
                classification.confidence,
            )
            if classification.mailing_list:
                classification.action = Action.ROUTE
            else:
                # Prefer routing to first list if unsure but not spam-like
                if (
                    self.app_config.mailing_lists
                    and classification.confidence >= self.app_config.route_confidence_threshold
                ):
                    classification.action = Action.ROUTE
                    classification.mailing_list = self.app_config.mailing_lists[0].name
                    classification.reason += " (low respond confidence; routed)"
                else:
                    classification.action = Action.IGNORE
                    classification.reason += " (below confidence thresholds)"
        elif (
            classification.action == Action.ROUTE
            and classification.confidence < self.app_config.route_confidence_threshold
        ):
            classification.action = Action.IGNORE
            classification.reason += " (route confidence below threshold)"
        return classification

    def _handle_respond(
        self, message: EmailMessage, classification: Classification
    ) -> ProcessResult:
        query = classification.customer_question or f"{message.subject}\n{message.body_text}"
        snippets = self.knowledge.search(query, top_k=3)
        reply = self.openrouter.draft_reply(message, snippets, question=classification.customer_question)
        self.mail.send_reply(message, reply)
        self._finalize(message)
        return ProcessResult(
            uid=message.uid,
            action=Action.RESPOND,
            detail=classification.reason,
            reply_sent=True,
        )

    def _handle_route(
        self, message: EmailMessage, classification: Classification
    ) -> ProcessResult:
        list_name = classification.mailing_list
        address = self._resolve_list_address(list_name)
        if not address:
            # Fallback: first configured list, else ignore
            if self.app_config.mailing_lists:
                address = self.app_config.mailing_lists[0].address
                list_name = self.app_config.mailing_lists[0].name
            else:
                detail = "Route requested but no mailing lists configured"
                self._finalize(message)
                return ProcessResult(uid=message.uid, action=Action.IGNORE, detail=detail)

        self.mail.forward_to_list(message, address, note=classification.reason)
        self._finalize(message)
        return ProcessResult(
            uid=message.uid,
            action=Action.ROUTE,
            detail=classification.reason,
            routed_to=list_name or address,
        )

    def _resolve_list_address(self, list_name: str | None) -> str | None:
        if not list_name:
            return None
        needle = list_name.strip().lower()
        for ml in self.app_config.mailing_lists:
            if ml.name.lower() == needle or ml.address.lower() == needle:
                return ml.address
        # Fuzzy: substring match on name
        for ml in self.app_config.mailing_lists:
            if needle in ml.name.lower():
                return ml.address
        return None

    def _finalize(self, message: EmailMessage) -> None:
        if self.settings.dry_run:
            logger.info("[dry-run] Would mark seen / move uid=%s", message.uid)
            return
        # Local/eml UIDs are not real IMAP UIDs
        if message.uid.startswith("local-"):
            return
        try:
            self.mail.mark_seen(message.uid)
            if self.app_config.processed_folder:
                self.mail.move_to_folder(message.uid, self.app_config.processed_folder)
        except Exception:
            logger.exception("Failed to finalize uid=%s", message.uid)


def run_inbox_once(settings: Settings, app_config: AppConfig) -> list[ProcessResult]:
    knowledge = KnowledgeBase(app_config.knowledge_base_dir)
    openrouter = OpenRouterClient(settings)
    with EmailClient(settings) as mail:
        messages = mail.fetch_unread()
        logger.info("Fetched %d unread message(s)", len(messages))
        pipeline = EnquiryPipeline(settings, app_config, openrouter, knowledge, mail)
        return pipeline.process_many(messages)
