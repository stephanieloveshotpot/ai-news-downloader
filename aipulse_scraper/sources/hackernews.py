"""Hacker News via the public Algolia search API (no key needed).

We pull the current front page plus a keyword search over the last 48 hours,
then let the relevance scorer decide what is actually about AI.
"""

from __future__ import annotations

import logging
import time
from datetime import datetime, timezone

from ..http import get
from ..models import Article, to_iso
from ..relevance import score, tags_for

log = logging.getLogger("aipulse.hn")

API = "https://hn.algolia.com/api/v1"
QUERIES = ["AI", "LLM", "OpenAI", "Anthropic", "machine learning", "GPT", "Claude"]


def _parse(hit: dict) -> Article | None:
    url = hit.get("url") or f"https://news.ycombinator.com/item?id={hit.get('objectID')}"
    title = hit.get("title") or hit.get("story_title")
    if not title or not url:
        return None
    created = hit.get("created_at_i")
    dt = datetime.fromtimestamp(created, tz=timezone.utc) if created else None
    summary = hit.get("story_text") or ""
    return Article(
        title=title,
        url=url,
        source="hackernews",
        source_label="Hacker News",
        category="news",
        published=to_iso(dt),
        summary=summary,
        author=hit.get("author") or "",
        points=hit.get("points"),
        comments_url=f"https://news.ycombinator.com/item?id={hit.get('objectID')}",
        score=score(title, summary),
        tags=tags_for(title, summary),
    )


def fetch(limit: int = 60) -> list[Article]:
    since = int(time.time()) - 48 * 3600
    seen: dict[str, Article] = {}

    def ingest(resp):
        if not resp:
            return
        for hit in resp.json().get("hits", []):
            a = _parse(hit)
            if a and a.id not in seen:
                seen[a.id] = a

    ingest(get(f"{API}/search", params={"tags": "front_page", "hitsPerPage": 40}))
    for q in QUERIES:
        ingest(get(f"{API}/search_by_date", params={
            "query": q, "tags": "story",
            "numericFilters": f"created_at_i>{since},points>5",
            "hitsPerPage": 30,
        }))
        if len(seen) >= limit * 3:
            break

    items = sorted(seen.values(), key=lambda a: (a.score, a.points or 0), reverse=True)
    log.info("hackernews: %d candidates", len(items))
    return items[:limit]
