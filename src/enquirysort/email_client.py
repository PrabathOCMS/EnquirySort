from __future__ import annotations

import email
import imaplib
import logging
import re
import smtplib
from email.header import decode_header, make_header
from email.message import EmailMessage as StdEmailMessage
from email.utils import parseaddr

from enquirysort.config import Settings
from enquirysort.models import EmailMessage

logger = logging.getLogger(__name__)


def _decode_header_value(value: str | None) -> str:
    if not value:
        return ""
    try:
        return str(make_header(decode_header(value)))
    except Exception:
        return value


def _extract_text(msg: email.message.Message) -> str:
    if msg.is_multipart():
        parts: list[str] = []
        for part in msg.walk():
            content_type = part.get_content_type()
            disposition = str(part.get("Content-Disposition", ""))
            if content_type == "text/plain" and "attachment" not in disposition:
                payload = part.get_payload(decode=True) or b""
                charset = part.get_content_charset() or "utf-8"
                parts.append(payload.decode(charset, errors="replace"))
        if parts:
            return "\n".join(parts).strip()
        # Fallback: strip tags from first HTML part
        for part in msg.walk():
            if part.get_content_type() == "text/html":
                payload = part.get_payload(decode=True) or b""
                charset = part.get_content_charset() or "utf-8"
                html = payload.decode(charset, errors="replace")
                return re.sub(r"<[^>]+>", " ", html)
        return ""

    payload = msg.get_payload(decode=True) or b""
    charset = msg.get_content_charset() or "utf-8"
    text = payload.decode(charset, errors="replace")
    if msg.get_content_type() == "text/html":
        return re.sub(r"<[^>]+>", " ", text)
    return text


class EmailClient:
    """IMAP inbox reader + SMTP sender."""

    def __init__(self, settings: Settings) -> None:
        self.settings = settings
        self._imap: imaplib.IMAP4_SSL | None = None

    def connect(self) -> None:
        logger.info("Connecting to IMAP %s:%s", self.settings.imap_host, self.settings.imap_port)
        self._imap = imaplib.IMAP4_SSL(self.settings.imap_host, self.settings.imap_port)
        self._imap.login(self.settings.email_address, self.settings.email_password)
        self._imap.select(self.settings.mailbox)

    def close(self) -> None:
        if self._imap is None:
            return
        try:
            self._imap.close()
        except Exception:
            pass
        try:
            self._imap.logout()
        except Exception:
            pass
        self._imap = None

    def __enter__(self) -> EmailClient:
        self.connect()
        return self

    def __exit__(self, *args: object) -> None:
        self.close()

    def fetch_unread(self, limit: int = 20) -> list[EmailMessage]:
        if self._imap is None:
            raise RuntimeError("IMAP not connected")

        status, data = self._imap.uid("search", None, "UNSEEN")
        if status != "OK":
            raise RuntimeError(f"IMAP search failed: {status}")

        uids = data[0].split() if data and data[0] else []
        messages: list[EmailMessage] = []
        for uid in uids[-limit:]:
            status, fetched = self._imap.uid("fetch", uid, "(RFC822)")
            if status != "OK" or not fetched or not fetched[0]:
                continue
            raw = fetched[0][1]
            if not isinstance(raw, (bytes, bytearray)):
                continue
            parsed = email.message_from_bytes(raw)
            _, from_addr = parseaddr(_decode_header_value(parsed.get("From")))
            to_raw = _decode_header_value(parsed.get("To"))
            to_addrs = [parseaddr(part)[1] for part in to_raw.split(",") if parseaddr(part)[1]]
            messages.append(
                EmailMessage(
                    uid=uid.decode() if isinstance(uid, bytes) else str(uid),
                    message_id=_decode_header_value(parsed.get("Message-ID")),
                    subject=_decode_header_value(parsed.get("Subject")) or "(no subject)",
                    from_address=from_addr,
                    to_addresses=to_addrs,
                    body_text=_extract_text(parsed),
                    raw_headers={
                        "In-Reply-To": _decode_header_value(parsed.get("In-Reply-To")),
                        "References": _decode_header_value(parsed.get("References")),
                    },
                )
            )
        return messages

    def mark_seen(self, uid: str) -> None:
        if self._imap is None:
            raise RuntimeError("IMAP not connected")
        self._imap.uid("store", uid, "+FLAGS", "(\\Seen)")

    def ensure_folder(self, folder: str) -> None:
        if self._imap is None:
            raise RuntimeError("IMAP not connected")
        # Create nested folders one segment at a time when possible
        status, _ = self._imap.list(directory=folder)
        if status == "OK":
            # LIST may succeed even if folder missing depending on server; try create.
            pass
        typ, data = self._imap.create(folder)
        if typ == "OK":
            logger.info("Created IMAP folder %s", folder)
        elif data and b"exists" in b" ".join(d for d in data if isinstance(d, bytes)).lower():
            return
        # Ignore "already exists" style errors from other servers
        if typ != "OK":
            logger.debug("Folder create response for %s: %s %s", folder, typ, data)

    def move_to_folder(self, uid: str, folder: str) -> None:
        if self._imap is None:
            raise RuntimeError("IMAP not connected")
        self.ensure_folder(folder)
        # Prefer MOVE when supported, else COPY + delete
        try:
            typ, _ = self._imap.uid("MOVE", uid, folder)
            if typ == "OK":
                return
        except Exception:
            pass
        typ, _ = self._imap.uid("COPY", uid, folder)
        if typ == "OK":
            self._imap.uid("store", uid, "+FLAGS", "(\\Deleted)")
            self._imap.expunge()

    def send_reply(
        self,
        original: EmailMessage,
        body: str,
        *,
        subject_prefix: str = "Re: ",
    ) -> None:
        subject = original.subject
        if not subject.lower().startswith("re:"):
            subject = f"{subject_prefix}{subject}"

        msg = StdEmailMessage()
        msg["From"] = self.settings.email_address
        msg["To"] = original.from_address
        msg["Subject"] = subject
        if original.message_id:
            msg["In-Reply-To"] = original.message_id
            refs = original.raw_headers.get("References") or original.message_id
            msg["References"] = refs
        msg.set_content(body)
        self._smtp_send(msg)

    def forward_to_list(
        self,
        original: EmailMessage,
        list_address: str,
        *,
        note: str = "",
    ) -> None:
        msg = StdEmailMessage()
        msg["From"] = self.settings.email_address
        msg["To"] = list_address
        msg["Subject"] = f"[Routed] {original.subject}"
        msg["Reply-To"] = original.from_address
        content_parts = [
            "This enquiry was automatically routed by EnquirySort.",
            f"Original From: {original.from_address}",
            f"Original Subject: {original.subject}",
        ]
        if note:
            content_parts.append(f"Classifier note: {note}")
        content_parts.extend(["", "----- Original Message -----", original.body_text])
        msg.set_content("\n".join(content_parts))
        self._smtp_send(msg)

    def _smtp_send(self, msg: StdEmailMessage) -> None:
        if self.settings.dry_run:
            logger.info(
                "[dry-run] Would send email To=%s Subject=%s",
                msg["To"],
                msg["Subject"],
            )
            return
        logger.info("Sending email To=%s Subject=%s", msg["To"], msg["Subject"])
        with smtplib.SMTP(self.settings.smtp_host, self.settings.smtp_port) as smtp:
            smtp.ehlo()
            smtp.starttls()
            smtp.ehlo()
            smtp.login(self.settings.email_address, self.settings.email_password)
            smtp.send_message(msg)


def parse_eml_bytes(raw: bytes, uid: str = "local-0") -> EmailMessage:
    """Parse a raw .eml for offline / dry-run processing."""
    parsed = email.message_from_bytes(raw)
    _, from_addr = parseaddr(_decode_header_value(parsed.get("From")))
    to_raw = _decode_header_value(parsed.get("To"))
    to_addrs = [parseaddr(part)[1] for part in to_raw.split(",") if parseaddr(part)[1]]
    return EmailMessage(
        uid=uid,
        message_id=_decode_header_value(parsed.get("Message-ID")),
        subject=_decode_header_value(parsed.get("Subject")) or "(no subject)",
        from_address=from_addr,
        to_addresses=to_addrs,
        body_text=_extract_text(parsed),
        raw_headers={
            "In-Reply-To": _decode_header_value(parsed.get("In-Reply-To")),
            "References": _decode_header_value(parsed.get("References")),
        },
    )


def load_eml_file(path: str, uid: str = "local-0") -> EmailMessage:
    with open(path, "rb") as fh:
        return parse_eml_bytes(fh.read(), uid=uid)
