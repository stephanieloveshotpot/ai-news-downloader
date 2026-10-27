from aipulse_scraper.models import Article, article_id, canonical_url, clean_text


def test_canonical_strips_tracking_and_www():
    a = canonical_url("https://www.Example.com/post/?utm_source=x&id=2#frag")
    b = canonical_url("https://example.com/post?id=2")
    assert a == b


def test_article_id_stable():
    assert article_id("https://a.com/x?utm_medium=y") == article_id("https://a.com/x")


def test_clean_text_strips_html_and_caps():
    s = clean_text("<p>Hello   <b>world</b></p>" + " word" * 300, limit=50)
    assert "<" not in s and s.endswith("…") and len(s) <= 50


def test_article_defaults():
    a = Article(title=" T ", url="https://a.com/x/", source="s", source_label="S",
                category="news", published="2026-01-01T00:00:00+00:00")
    assert a.title == "T" and a.url == "https://a.com/x" and a.id
