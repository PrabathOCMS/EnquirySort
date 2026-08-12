from __future__ import annotations

import logging
import math
import re
from collections import Counter
from pathlib import Path

from enquirysort.models import KnowledgeSnippet

logger = logging.getLogger(__name__)

_TOKEN_RE = re.compile(r"[a-z0-9]{2,}")


def _tokenize(text: str) -> list[str]:
    return _TOKEN_RE.findall(text.lower())


def _title_from_markdown(path: Path, content: str) -> str:
    for line in content.splitlines():
        stripped = line.strip()
        if stripped.startswith("# "):
            return stripped[2:].strip()
    return path.stem.replace("_", " ").replace("-", " ").title()


class KnowledgeBase:
    """Simple local markdown knowledge base with TF-IDF retrieval."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.documents: list[KnowledgeSnippet] = []
        self._df: Counter[str] = Counter()
        self._doc_tf: list[Counter[str]] = []
        self.reload()

    def reload(self) -> None:
        self.documents = []
        self._doc_tf = []
        self._df = Counter()
        if not self.root.exists():
            logger.warning("Knowledge base directory missing: %s", self.root)
            return

        for path in sorted(self.root.rglob("*")):
            if path.suffix.lower() not in {".md", ".txt"}:
                continue
            if not path.is_file():
                continue
            content = path.read_text(encoding="utf-8").strip()
            if not content:
                continue
            rel = str(path.relative_to(self.root))
            snippet = KnowledgeSnippet(
                path=rel,
                title=_title_from_markdown(path, content),
                content=content,
            )
            tokens = _tokenize(f"{snippet.title}\n{content}")
            tf = Counter(tokens)
            self.documents.append(snippet)
            self._doc_tf.append(tf)
            for term in tf:
                self._df[term] += 1

        logger.info("Loaded %d knowledge base documents from %s", len(self.documents), self.root)

    def search(self, query: str, *, top_k: int = 3) -> list[KnowledgeSnippet]:
        if not self.documents:
            return []
        q_tokens = _tokenize(query)
        if not q_tokens:
            return []

        q_tf = Counter(q_tokens)
        n_docs = len(self.documents)
        scores: list[tuple[float, int]] = []

        for idx, doc_tf in enumerate(self._doc_tf):
            score = 0.0
            for term, q_weight in q_tf.items():
                if term not in doc_tf:
                    continue
                idf = math.log((1 + n_docs) / (1 + self._df[term])) + 1.0
                score += q_weight * doc_tf[term] * idf
            # Light boost when query terms appear in title
            title_tokens = set(_tokenize(self.documents[idx].title))
            score += 0.5 * sum(1 for t in q_tokens if t in title_tokens)
            if score > 0:
                scores.append((score, idx))

        scores.sort(reverse=True)
        results: list[KnowledgeSnippet] = []
        for score, idx in scores[:top_k]:
            doc = self.documents[idx]
            results.append(
                KnowledgeSnippet(
                    path=doc.path,
                    title=doc.title,
                    content=doc.content,
                    score=score,
                )
            )
        return results
