"""Run every source, merge, dedupe, filter by relevance, and write JSON."""

from __future__ import annotations

import json
import logging
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

from . import __version__
from .models import Article, now_iso
from .relevance import MIN_SCORE
from .sources import SOURCES

log = logging.getLogger("aipulse")

DEFAULT_LIMITS = {"hackernews": 60, "arxiv": 40, "blogs": 40, "press": 50}


def _dedupe(articles: list[Article]) -> list[Article]:
    """Merge by canonical URL; when two sources carry the same link keep the
    higher-scoring one, and keep HN points/comments if either side had them."""
    by_id: dict[str, Article] = {}
    for a in articles:
        if a.id in by_id:
            keep, other = by_id[a.id], a
            if other.score > keep.score:
                keep, other = other, keep
            if keep.points is None and other.points is not None:
                keep.points, keep.comments_url = other.points, other.comments_url
            by_id[a.id] = keep
        else:
            by_id[a.id] = a
    # second pass: near-identical titles across different URLs
    seen_titles: set[str] = set()
    out: list[Article] = []
    for a in sorted(by_id.values(), key=lambda x: x.score, reverse=True):
        key = "".join(ch for ch in a.title.lower() if ch.isalnum())[:80]
        if key in seen_titles:
            continue
        seen_titles.add(key)
        out.append(a)
    return out


def run(sources: list[str] | None = None, limits: dict[str, int] | None = None,
        min_score: float = MIN_SCORE) -> dict:
    sources = sources or list(SOURCES)
    limits = {**DEFAULT_LIMITS, **(limits or {})}
    collected: list[Article] = []
    errors: dict[str, str] = {}

    with ThreadPoolExecutor(max_workers=len(sources)) as pool:
        futures = {pool.submit(SOURCES[s].fetch, limits.get(s, 40)): s for s in sources}
        for fut in as_completed(futures):
            name = futures[fut]
            try:
                collected.extend(fut.result())
            except Exception as exc:  # one bad source must not kill the run
                log.exception("source %s failed", name)
                errors[name] = str(exc)

    kept = [a for a in _dedupe(collected) if a.score >= min_score]
    kept.sort(key=lambda a: a.published, reverse=True)

    return {
        "version": __version__,
        "generated_at": now_iso(),
        "sources": sources,
        "errors": errors,
        "count": len(kept),
        "articles": [a.to_dict() for a in kept],
    }


def write(result: dict, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)  # atomic on the same filesystem
