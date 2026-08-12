# EnquirySort

AI-powered inbox triage for customer enquiries. EnquirySort reads unread mail from your IMAP inbox, classifies each message with [OpenRouter](https://openrouter.ai), then either:

1. **Responds** to the customer using your local markdown knowledge base, or
2. **Routes** the message to the right internal mailing list (sales, support, billing, …)

```text
Inbox (IMAP)
    │
    ▼
OpenRouter classifier ──► respond | route | ignore
    │                         │        │
    │                         ▼        ▼
    │                   Knowledge   Mailing list
    │                   base reply    forward
    ▼                         │        │
Mark seen / move to Processed folder
```

## Quick start

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -e ".[dev]"

cp .env.example .env
# Edit .env with EMAIL_ADDRESS, EMAIL_PASSWORD, OPENROUTER_API_KEY
# Edit config.yaml mailing list addresses
```

### Process the live inbox once

```bash
enquirysort --once
```

### Keep polling

```bash
enquirysort --poll
```

### Dry-run against sample emails (no send / no IMAP writes)

```bash
# Still needs OPENROUTER_API_KEY
export OPENROUTER_API_KEY=sk-or-v1-...
enquirysort --dry-run --eml samples/faq_password.eml --eml samples/sales_quote.eml
```

## Configuration

| Source | Purpose |
|--------|---------|
| `.env` | IMAP/SMTP credentials and OpenRouter API key |
| `config.yaml` | Mailing lists, confidence thresholds, knowledge base path |
| `knowledge_base/*.md` | Documents used when drafting automatic replies |

### Classification actions

| Action | When | What happens |
|--------|------|----------------|
| `respond` | FAQ / how-to answerable from docs | Retrieve KB snippets → draft reply via OpenRouter → SMTP reply |
| `route` | Needs a human team | Forward original to the matching mailing list |
| `ignore` | Spam, OOO, noise, or low confidence | Mark processed without contacting the customer |

Confidence thresholds in `config.yaml` prevent weak `respond` decisions from auto-replying; they fall back to routing when a list is available.

## Project layout

```text
src/enquirysort/   # application package
knowledge_base/    # markdown knowledge base
samples/           # example .eml files
config.yaml        # mailing lists + thresholds
tests/             # unit tests
```

## Tests

```bash
pytest
```

## Notes for Gmail / Google Workspace

Use an [App Password](https://myaccount.google.com/apppasswords) (with 2FA enabled) as `EMAIL_PASSWORD`. IMAP must be enabled in Gmail settings.
