from __future__ import annotations

import argparse
import logging
import sys
import time
from pathlib import Path

from enquirysort.config import Settings, load_app_config
from enquirysort.email_client import EmailClient, load_eml_file
from enquirysort.knowledge import KnowledgeBase
from enquirysort.openrouter import OpenRouterClient
from enquirysort.pipeline import EnquiryPipeline, run_inbox_once


def _configure_logging(verbose: bool) -> None:
    level = logging.DEBUG if verbose else logging.INFO
    logging.basicConfig(
        level=level,
        format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="enquirysort",
        description=(
            "Read inbox email, classify with OpenRouter, then either reply from a "
            "knowledge base or forward to a mailing list."
        ),
    )
    parser.add_argument(
        "--once",
        action="store_true",
        help="Process unread mail once and exit (default when not polling).",
    )
    parser.add_argument(
        "--poll",
        action="store_true",
        help="Keep polling the inbox using poll_interval_seconds from config.",
    )
    parser.add_argument(
        "--eml",
        action="append",
        default=[],
        help="Process a local .eml file instead of IMAP (can repeat).",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Classify and draft actions without sending mail or altering IMAP.",
    )
    parser.add_argument(
        "--config",
        type=Path,
        default=None,
        help="Path to config.yaml (default: CONFIG_PATH or ./config.yaml).",
    )
    parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Enable debug logging.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    _configure_logging(args.verbose)

    settings = Settings()
    if args.config is not None:
        settings.config_path = args.config
    if args.dry_run:
        settings.dry_run = True
    if args.once:
        settings.once = True

    app_config = load_app_config(settings.config_path)
    logger = logging.getLogger("enquirysort")

    if args.eml:
        if not settings.openrouter_api_key:
            logger.error("OPENROUTER_API_KEY is required")
            return 2
        knowledge = KnowledgeBase(app_config.knowledge_base_dir)
        openrouter = OpenRouterClient(settings)
        # For local eml we still need a transport for dry-run send logging
        mail = EmailClient(settings)
        pipeline = EnquiryPipeline(settings, app_config, openrouter, knowledge, mail)
        messages = [load_eml_file(path, uid=f"local-{idx}") for idx, path in enumerate(args.eml)]
        results = pipeline.process_many(messages)
        for result in results:
            logger.info(
                "Result uid=%s action=%s reply=%s routed_to=%s detail=%s",
                result.uid,
                result.action.value,
                result.reply_sent,
                result.routed_to,
                result.detail,
            )
        return 0

    try:
        settings.require_credentials()
    except ValueError as exc:
        logger.error("%s", exc)
        return 2

    if args.poll:
        logger.info(
            "Polling inbox every %s seconds (dry_run=%s)",
            app_config.poll_interval_seconds,
            settings.dry_run,
        )
        while True:
            try:
                results = run_inbox_once(settings, app_config)
                for result in results:
                    logger.info(
                        "Result uid=%s action=%s reply=%s routed_to=%s detail=%s",
                        result.uid,
                        result.action.value,
                        result.reply_sent,
                        result.routed_to,
                        result.detail,
                    )
            except Exception:
                logger.exception("Poll cycle failed")
            time.sleep(app_config.poll_interval_seconds)
        return 0

    results = run_inbox_once(settings, app_config)
    for result in results:
        logger.info(
            "Result uid=%s action=%s reply=%s routed_to=%s detail=%s",
            result.uid,
            result.action.value,
            result.reply_sent,
            result.routed_to,
            result.detail,
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
