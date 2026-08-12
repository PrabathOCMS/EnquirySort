from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

from enquirysort.config import AppConfig, MailingList, Settings
from enquirysort.knowledge import KnowledgeBase
from enquirysort.models import Action, Classification, EmailMessage
from enquirysort.pipeline import EnquiryPipeline


@dataclass
class FakeOpenRouter:
    classification: Classification
    reply_text: str = "Here is how to reset your password."
    classify_calls: list[EmailMessage] = field(default_factory=list)
    draft_calls: list[EmailMessage] = field(default_factory=list)

    def classify(self, message: EmailMessage, app_config: AppConfig) -> Classification:
        self.classify_calls.append(message)
        return self.classification

    def draft_reply(self, message: EmailMessage, snippets, question=None) -> str:
        self.draft_calls.append(message)
        return self.reply_text


@dataclass
class FakeMail:
    replies: list[tuple[EmailMessage, str]] = field(default_factory=list)
    forwards: list[tuple[EmailMessage, str, str]] = field(default_factory=list)
    seen: list[str] = field(default_factory=list)
    moved: list[tuple[str, str]] = field(default_factory=list)

    def send_reply(self, original: EmailMessage, body: str, *, subject_prefix: str = "Re: ") -> None:
        self.replies.append((original, body))

    def forward_to_list(self, original: EmailMessage, list_address: str, *, note: str = "") -> None:
        self.forwards.append((original, list_address, note))

    def mark_seen(self, uid: str) -> None:
        self.seen.append(uid)

    def move_to_folder(self, uid: str, folder: str) -> None:
        self.moved.append((uid, folder))


def _message(uid: str = "1") -> EmailMessage:
    return EmailMessage(
        uid=uid,
        message_id="<msg@example.com>",
        subject="Help",
        from_address="customer@example.com",
        to_addresses=["inbox@example.com"],
        body_text="I need help resetting my password.",
    )


def _app_config(kb: Path) -> AppConfig:
    return AppConfig(
        knowledge_base_dir=kb,
        mailing_lists=[
            MailingList(name="sales", address="sales@example.com", description="Sales"),
            MailingList(name="support", address="support@example.com", description="Support"),
        ],
        respond_confidence_threshold=0.65,
        route_confidence_threshold=0.55,
    )


def test_pipeline_responds_from_knowledge(tmp_path: Path) -> None:
    kb_dir = tmp_path / "kb"
    kb_dir.mkdir()
    (kb_dir / "password_reset.md").write_text(
        "# Password Reset\nVisit /forgot-password\n",
        encoding="utf-8",
    )
    settings = Settings(dry_run=False, openrouter_api_key="test")
    mail = FakeMail()
    openrouter = FakeOpenRouter(
        Classification(
            action=Action.RESPOND,
            confidence=0.9,
            reason="FAQ password reset",
            customer_question="How do I reset my password?",
        )
    )
    pipeline = EnquiryPipeline(
        settings,
        _app_config(kb_dir),
        openrouter,  # type: ignore[arg-type]
        KnowledgeBase(kb_dir),
        mail,
    )

    result = pipeline.process_message(_message("42"))
    assert result.action == Action.RESPOND
    assert result.reply_sent is True
    assert mail.replies and "password" in mail.replies[0][1].lower()
    assert mail.seen == ["42"]
    assert mail.moved == [("42", "EnquirySort/Processed")]


def test_pipeline_routes_to_sales_list(tmp_path: Path) -> None:
    kb_dir = tmp_path / "kb"
    kb_dir.mkdir()
    settings = Settings(dry_run=False)
    mail = FakeMail()
    openrouter = FakeOpenRouter(
        Classification(
            action=Action.ROUTE,
            confidence=0.95,
            reason="Enterprise quote request",
            mailing_list="sales",
        )
    )
    pipeline = EnquiryPipeline(
        settings,
        _app_config(kb_dir),
        openrouter,  # type: ignore[arg-type]
        KnowledgeBase(kb_dir),
        mail,
    )

    result = pipeline.process_message(_message("7"))
    assert result.action == Action.ROUTE
    assert result.routed_to == "sales"
    assert mail.forwards[0][1] == "sales@example.com"
    assert openrouter.draft_calls == []


def test_low_confidence_respond_falls_back_to_route(tmp_path: Path) -> None:
    kb_dir = tmp_path / "kb"
    kb_dir.mkdir()
    settings = Settings(dry_run=False)
    mail = FakeMail()
    openrouter = FakeOpenRouter(
        Classification(
            action=Action.RESPOND,
            confidence=0.4,
            reason="Unsure",
            mailing_list="support",
            customer_question="Something odd",
        )
    )
    pipeline = EnquiryPipeline(
        settings,
        _app_config(kb_dir),
        openrouter,  # type: ignore[arg-type]
        KnowledgeBase(kb_dir),
        mail,
    )

    result = pipeline.process_message(_message("9"))
    assert result.action == Action.ROUTE
    assert result.routed_to == "support"
