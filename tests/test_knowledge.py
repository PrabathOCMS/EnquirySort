from pathlib import Path

from enquirysort.knowledge import KnowledgeBase


def test_knowledge_search_finds_password_article(tmp_path: Path) -> None:
    kb_dir = tmp_path / "kb"
    kb_dir.mkdir()
    (kb_dir / "password_reset.md").write_text(
        "# Resetting Your Password\n\nUse the forgot-password page to reset.\n",
        encoding="utf-8",
    )
    (kb_dir / "pricing.md").write_text(
        "# Pricing Overview\n\nStarter is $49 per month.\n",
        encoding="utf-8",
    )

    kb = KnowledgeBase(kb_dir)
    results = kb.search("I forgot my password and cannot log in")
    assert results
    assert "password" in results[0].title.lower() or "password" in results[0].path
