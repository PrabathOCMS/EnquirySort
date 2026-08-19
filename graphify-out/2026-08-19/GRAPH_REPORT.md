# Graph Report - workspace  (2026-08-19)

## Corpus Check
- 18 files · ~4,176 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 134 nodes · 318 edges · 16 communities (10 shown, 6 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 41 edges (avg confidence: 0.95)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `3e603d47`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- pipeline.py
- EmailMessage
- cli.py
- EmailClient
- Settings
- EnquirySort
- AppConfig
- email_client.py
- EnquiryPipeline
- MailTransport
- Getting Started with OracleCMS
- custom_domains.md
- password_reset.md
- pricing.md
- __init__.py
- enquirysort

## God Nodes (most connected - your core abstractions)
1. `EmailMessage` - 30 edges
2. `EnquiryPipeline` - 23 edges
3. `EmailClient` - 20 edges
4. `Settings` - 18 edges
5. `OpenRouterClient` - 17 edges
6. `KnowledgeBase` - 16 edges
7. `Classification` - 15 edges
8. `AppConfig` - 14 edges
9. `Action` - 12 edges
10. `main()` - 11 edges

## Surprising Connections (you probably didn't know these)
- `_app_config()` --uses--> `MailingList`  [INFERRED]
  tests/test_pipeline.py → src/enquirysort/config.py
- `FakeOpenRouter` --uses--> `AppConfig`  [INFERRED]
  tests/test_pipeline.py → src/enquirysort/config.py
- `test_low_confidence_respond_falls_back_to_route()` --uses--> `Settings`  [INFERRED]
  tests/test_pipeline.py → src/enquirysort/config.py
- `test_pipeline_responds_from_knowledge()` --uses--> `Settings`  [INFERRED]
  tests/test_pipeline.py → src/enquirysort/config.py
- `test_pipeline_routes_to_sales_list()` --uses--> `Settings`  [INFERRED]
  tests/test_pipeline.py → src/enquirysort/config.py

## Import Cycles
- None detected.

## Communities (16 total, 6 thin omitted)

### Community 0 - "pipeline.py"
Cohesion: 0.21
Nodes (11): Enum, Action, KnowledgeSnippet, _parse_json_object(), Any, str, Path, test_knowledge_search_finds_password_article() (+3 more)

### Community 1 - "EmailMessage"
Cohesion: 0.29
Nodes (9): EmailMessage, _app_config(), FakeMail, FakeOpenRouter, _message(), Path, test_low_confidence_respond_falls_back_to_route(), test_pipeline_responds_from_knowledge() (+1 more)

### Community 2 - "cli.py"
Cohesion: 0.23
Nodes (10): ArgumentParser, build_parser(), _configure_logging(), main(), KnowledgeBase, Path, Simple local markdown knowledge base with TF-IDF retrieval., _title_from_markdown() (+2 more)

### Community 3 - "EmailClient"
Cohesion: 0.24
Nodes (3): EmailClient, IMAP inbox reader + SMTP sender., StdEmailMessage

### Community 4 - "Settings"
Cohesion: 0.23
Nodes (4): BaseSettings, Settings, Any, OpenRouterClient

### Community 5 - "EnquirySort"
Cohesion: 0.18
Nodes (10): Classification actions, Configuration, Dry-run against sample emails (no send / no IMAP writes), EnquirySort, Keep polling, Notes for Gmail / Google Workspace, Process the live inbox once, Project layout (+2 more)

### Community 6 - "AppConfig"
Cohesion: 0.27
Nodes (7): BaseModel, field_validator, AppConfig, load_app_config(), MailingList, Any, Path

### Community 7 - "email_client.py"
Cohesion: 0.31
Nodes (7): Message, _decode_header_value(), _extract_text(), load_eml_file(), parse_eml_bytes(), Parse a raw .eml for offline / dry-run processing., test_load_sample_eml()

### Community 8 - "EnquiryPipeline"
Cohesion: 0.53
Nodes (3): Classification, ProcessResult, EnquiryPipeline

### Community 10 - "Getting Started with OracleCMS"
Cohesion: 0.40
Nodes (4): Creating your first site, Getting Started with OracleCMS, Inviting teammates, Supported browsers

## Knowledge Gaps
- **14 isolated node(s):** `enquirysort`, `Process the live inbox once`, `Keep polling`, `Dry-run against sample emails (no send / no IMAP writes)`, `Classification actions` (+9 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EmailMessage` connect `EmailMessage` to `pipeline.py`, `EmailClient`, `Settings`, `AppConfig`, `email_client.py`, `EnquiryPipeline`, `MailTransport`?**
  _High betweenness centrality (0.177) - this node is a cross-community bridge._
- **Why does `EmailClient` connect `EmailClient` to `pipeline.py`, `EmailMessage`, `cli.py`, `Settings`, `email_client.py`?**
  _High betweenness centrality (0.120) - this node is a cross-community bridge._
- **Why does `KnowledgeBase` connect `cli.py` to `pipeline.py`, `EnquiryPipeline`, `MailTransport`, `EmailMessage`?**
  _High betweenness centrality (0.077) - this node is a cross-community bridge._
- **Are the 7 inferred relationships involving `EmailMessage` (e.g. with `EmailClient` and `load_eml_file()`) actually correct?**
  _`EmailMessage` has 7 INFERRED edges - model-reasoned connections that need verification._
- **Are the 11 inferred relationships involving `EnquiryPipeline` (e.g. with `AppConfig` and `Settings`) actually correct?**
  _`EnquiryPipeline` has 11 INFERRED edges - model-reasoned connections that need verification._
- **Are the 2 inferred relationships involving `EmailClient` (e.g. with `Settings` and `EmailMessage`) actually correct?**
  _`EmailClient` has 2 INFERRED edges - model-reasoned connections that need verification._
- **Are the 7 inferred relationships involving `Settings` (e.g. with `EmailClient` and `OpenRouterClient`) actually correct?**
  _`Settings` has 7 INFERRED edges - model-reasoned connections that need verification._