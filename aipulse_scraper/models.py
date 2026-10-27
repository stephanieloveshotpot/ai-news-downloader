"""The one data shape every source produces: an Article."""

from __future__ import annotations

import hashlib
import re
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from urllib.parse import urlparse, urlunparse, parse_qsl, urlencode

# Query-string keys that only track you and make the same URL look different.
_TRACKING_KEYS = {
    "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
    "fbclid", "gclid", "ref", "ref_src", "mc_cid", "mc_eid", "igshid",
}


def canonical_url(url: str) -> str:
    """Strip tracking parameters and fragments so duplicates match."""
    try:
        p = urlparse(url.strip())
    except ValueError:
        return url
    query = [(k, v) for k, v in parse_qsl(p.query, keep_blank_values=True)
             if k.lower() not in _TRACKING_KEYS]
    netloc = p.netloc.lower()
    if netloc.startswith("www."):
        netloc = netloc[4:]
    path = p.path.rstrip("/") or "/"
    return urlunparse((p.scheme.lower() or "https", netloc, path, "",
                       urlencode(query), ""))


def article_id(url: str) -> str:
    return hashlib.sha1(canonical_url(url).encode("utf-8")).hexdigest()[:16]


_WS = re.compile(r"\s+")


def clean_text(text: str | None, limit: int = 600) -> str:
    """Collapse whitespace, strip HTML tags, cap length."""
    if not text:
        return ""
    text = re.sub(r"<[^>]+>", " ", text)
    text = _WS.sub(" ", text).strip()
    if len(text) > limit:
        text = text[: limit - 1].rsplit(" ", 1)[0] + "…"
    return text


@dataclass
class Article:
    title: str
    url: str
    source: str                 # machine key, e.g. "hackernews", "arxiv"
    source_label: str           # human label, e.g. "Hacker News"
    category: str               # "news" | "research" | "blog"
    published: str              # ISO-8601 UTC
    summary: str = ""
    author: str = ""
    score: float = 0.0          # AI-relevance score, 0-100
    tags: list[str] = field(default_factory=list)
    comments_url: str = ""
    points: int | None = None   # HN points, when known
    id: str = ""

    def __post_init__(self) -> None:
        self.url = canonical_url(self.url)
        self.title = clean_text(self.title, 300)
        self.summary = clean_text(self.summary)
        if not self.id:
            self.id = article_id(self.url)

    def to_dict(self) -> dict:
        return asdict(self)


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def to_iso(dt: datetime | None, fallback: str | None = None) -> str:
    if dt is None:
        return fallback or now_iso()
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc).replace(microsecond=0).isoformat()
