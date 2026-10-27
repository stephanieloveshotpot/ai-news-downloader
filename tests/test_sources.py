"""Parse real-shaped feed samples with the network stubbed out."""

from types import SimpleNamespace

from aipulse_scraper.sources import arxiv, hackernews, rss

ATOM = b"""<?xml version="1.0" encoding="UTF-8"?>
<feed xmlns="http://www.w3.org/2005/Atom" xmlns:arxiv="http://arxiv.org/schemas/atom">
  <entry>
    <id>http://arxiv.org/abs/2610.12345v1</id>
    <updated>2026-10-26T17:59:00Z</updated>
    <published>2026-10-26T17:59:00Z</published>
    <title>Scaling Laws for Sparse Mixture-of-Experts Transformers</title>
    <summary>We study transformers with many experts and propose a new benchmark.</summary>
    <author><name>A. Researcher</name></author>
    <author><name>B. Researcher</name></author>
    <link href="http://arxiv.org/abs/2610.12345v1" rel="alternate" type="text/html"/>
    <arxiv:primary_category term="cs.LG"/>
  </entry>
</feed>"""

RSS = b"""<?xml version="1.0"?>
<rss version="2.0"><channel><title>Blog</title>
  <item>
    <title>Introducing our new model</title>
    <link>https://example.com/blog/new-model?utm_source=rss</link>
    <pubDate>Mon, 26 Oct 2026 12:00:00 GMT</pubDate>
    <description><![CDATA[<p>Today we release a <b>multimodal</b> LLM.</p>]]></description>
  </item>
</channel></rss>"""

HN = {"hits": [{
    "objectID": "1", "title": "Anthropic releases Claude", "url": "https://anthropic.com/news/x",
    "points": 500, "author": "pg", "created_at_i": 1792900000, "story_text": None,
}]}


def _resp(content=None, payload=None):
    return SimpleNamespace(content=content, json=lambda: payload)


def test_arxiv_parse(monkeypatch):
    monkeypatch.setattr(arxiv, "get", lambda *a, **k: _resp(ATOM))
    items = arxiv.fetch(5)
    assert len(items) == 1
    a = items[0]
    assert a.source == "arxiv" and a.category == "research" and a.score >= 40
    assert "research" in a.tags and a.published.startswith("2026-10-26")


def test_rss_parse(monkeypatch):
    monkeypatch.setattr(rss, "get", lambda *a, **k: _resp(RSS))
    items = rss.fetch_feed("blog", "Blog", "https://x", "blog", 50.0, 10)
    assert len(items) == 1
    a = items[0]
    assert "utm_source" not in a.url and "<" not in a.summary and a.score >= 50


def test_rss_lists_fetch(monkeypatch):
    monkeypatch.setattr(rss, "get", lambda *a, **k: _resp(RSS))
    assert len(rss.blogs.fetch(10)) >= 1 and len(rss.press.fetch(10)) >= 1


def test_hn_parse(monkeypatch):
    monkeypatch.setattr(hackernews, "get", lambda *a, **k: _resp(payload=HN))
    items = hackernews.fetch(10)
    assert len(items) == 1 and items[0].points == 500 and items[0].score > 50
    assert items[0].comments_url.endswith("item?id=1")


def test_sources_tolerate_network_failure(monkeypatch):
    for mod in (arxiv, hackernews, rss):
        monkeypatch.setattr(mod, "get", lambda *a, **k: None)
    assert arxiv.fetch(5) == [] and hackernews.fetch(5) == [] and rss.blogs.fetch(5) == []
