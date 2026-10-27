"""Generic RSS/Atom fetcher plus two curated feed lists:
`blogs` (AI company blogs) and `press` (tech press AI sections).

Each list is a module-like object with `.fetch(limit)` and `.FEEDS`, so the
pipeline can treat them like any other source.
"""

from __future__ import annotations

import logging
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from types import SimpleNamespace

import feedparser

from ..http import get
from ..models import Article, to_iso
from ..relevance import score, tags_for

log = logging.getLogger("aipulse.rss")

BLOG_FEEDS = [
    ("anthropic", "Anthropic", "https://www.anthropic.com/rss.xml"),
    ("openai", "OpenAI", "https://openai.com/news/rss.xml"),
    ("deepmind", "Google DeepMind", "https://deepmind.google/blog/rss.xml"),
    ("google-ai", "Google AI", "https://blog.google/technology/ai/rss/"),
    ("meta-ai", "Meta AI", "https://ai.meta.com/blog/rss/"),
    ("huggingface", "Hugging Face", "https://huggingface.co/blog/feed.xml"),
    ("microsoft-ai", "Microsoft AI", "https://blogs.microsoft.com/ai/feed/"),
    ("nvidia", "NVIDIA", "https://blogs.nvidia.com/feed/"),
]

PRESS_FEEDS = [
    ("techcrunch", "TechCrunch", "https://techcrunch.com/category/artificial-intelligence/feed/"),
    ("verge", "The Verge", "https://www.theverge.com/rss/ai-artificial-intelligence/index.xml"),
    ("mit-tr", "MIT Technology Review", "https://www.technologyreview.com/topic/artificial-intelligence/feed"),
    ("venturebeat", "VentureBeat", "https://venturebeat.com/category/ai/feed/"),
    ("ars", "Ars Technica", "https://feeds.arstechnica.com/arstechnica/technology-lab"),
    ("wired", "WIRED", "https://www.wired.com/feed/tag/ai/latest/rss"),
]


def _dt(entry) -> datetime | None:
    t = entry.get("published_parsed") or entry.get("updated_parsed")
    if not t:
        return None
    return datetime(*t[:6], tzinfo=timezone.utc)


def fetch_feed(key: str, label: str, url: str, category: str, floor: float, per_feed: int) -> list[Article]:
    resp = get(url)
    if not resp:
        return []
    feed = feedparser.parse(resp.content)
    if feed.bozo and not feed.entries:
        log.warning("%s: unparsable feed (%s)", key, getattr(feed, "bozo_exception", ""))
        return []
    items: list[Article] = []
    for e in feed.entries[:per_feed]:
        title = e.get("title", "")
        link = e.get("link", "")
        if not title or not link:
            continue
        summary = e.get("summary") or (e.get("content") or [{}])[0].get("value", "")
        items.append(Article(
            title=title,
            url=link,
            source=key,
            source_label=label,
            category=category,
            published=to_iso(_dt(e)),
            summary=summary,
            author=e.get("author", ""),
            score=score(title, summary, floor=floor),
            tags=tags_for(title, summary),
        ))
    log.info("%s: %d items", key, len(items))
    return items


def _fetch_all(feeds, category: str, floor: float, limit: int) -> list[Article]:
    per_feed = max(5, limit // max(1, len(feeds)) + 3)
    with ThreadPoolExecutor(max_workers=6) as pool:
        results = pool.map(lambda f: fetch_feed(*f, category, floor, per_feed), feeds)
    out = [a for batch in results for a in batch]
    out.sort(key=lambda a: a.published, reverse=True)
    return out[:limit]


blogs = SimpleNamespace(
    FEEDS=BLOG_FEEDS,
    fetch=lambda limit=40: _fetch_all(BLOG_FEEDS, "blog", 50.0, limit),
)
press = SimpleNamespace(
    FEEDS=PRESS_FEEDS,
    fetch=lambda limit=50: _fetch_all(PRESS_FEEDS, "news", 20.0, limit),
)
