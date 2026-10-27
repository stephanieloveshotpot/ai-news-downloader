"""arXiv new submissions in cs.AI, cs.LG and cs.CL via the official Atom API."""

from __future__ import annotations

import logging
from datetime import datetime, timezone

import feedparser

from ..http import get
from ..models import Article, to_iso
from ..relevance import score, tags_for

log = logging.getLogger("aipulse.arxiv")

API = "https://export.arxiv.org/api/query"
CATEGORIES = ["cs.AI", "cs.LG", "cs.CL"]


def _dt(entry) -> datetime | None:
    t = entry.get("published_parsed") or entry.get("updated_parsed")
    if not t:
        return None
    return datetime(*t[:6], tzinfo=timezone.utc)


def fetch(limit: int = 40) -> list[Article]:
    query = " OR ".join(f"cat:{c}" for c in CATEGORIES)
    resp = get(API, params={
        "search_query": query,
        "sortBy": "submittedDate",
        "sortOrder": "descending",
        "max_results": limit,
    })
    if not resp:
        return []
    feed = feedparser.parse(resp.content)
    items: list[Article] = []
    for e in feed.entries:
        title = e.get("title", "")
        summary = e.get("summary", "")
        authors = ", ".join(a.get("name", "") for a in e.get("authors", [])[:3])
        if len(e.get("authors", [])) > 3:
            authors += " et al."
        primary = ""
        try:
            primary = e.get("arxiv_primary_category", {}).get("term", "")
        except AttributeError:
            pass
        items.append(Article(
            title=title,
            url=e.get("link", ""),
            source="arxiv",
            source_label="arXiv",
            category="research",
            published=to_iso(_dt(e)),
            summary=summary,
            author=authors,
            score=score(title, summary, floor=40),  # arXiv cs.* is AI by construction
            tags=list(dict.fromkeys(["research", *tags_for(title, summary), primary.lower()]))[:4] if primary else tags_for(title, summary),
        ))
    log.info("arxiv: %d papers", len(items))
    return items
