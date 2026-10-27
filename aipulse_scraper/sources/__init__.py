"""Each source module exposes `fetch(limit) -> list[Article]`."""

from . import arxiv, hackernews, rss

SOURCES = {
    "hackernews": hackernews,
    "arxiv": arxiv,
    "blogs": rss.blogs,
    "press": rss.press,
}

__all__ = ["SOURCES"]
