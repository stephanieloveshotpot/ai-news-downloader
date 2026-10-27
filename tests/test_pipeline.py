import json

from aipulse_scraper import pipeline
from aipulse_scraper.models import Article


def _a(title, url, source="x", score=50, points=None, published="2026-01-01T00:00:00+00:00"):
    return Article(title=title, url=url, source=source, source_label=source,
                   category="news", published=published, score=score, points=points)


def test_dedupe_merges_same_url_and_keeps_points():
    items = [
        _a("Story", "https://a.com/1", "press", score=60),
        _a("Story", "https://a.com/1?utm_source=hn", "hackernews", score=40, points=120),
    ]
    out = pipeline._dedupe(items)
    assert len(out) == 1 and out[0].score == 60 and out[0].points == 120


def test_dedupe_near_identical_titles():
    items = [_a("Big AI News!", "https://a.com/1"), _a("big ai news", "https://b.com/2")]
    assert len(pipeline._dedupe(items)) == 1


def test_run_filters_and_survives_broken_source(monkeypatch, tmp_path):
    class Good:
        @staticmethod
        def fetch(limit):
            return [_a("AI thing", "https://a.com/ai", score=80),
                    _a("Bike thing", "https://a.com/bike", score=1)]

    class Bad:
        @staticmethod
        def fetch(limit):
            raise RuntimeError("boom")

    monkeypatch.setattr(pipeline, "SOURCES", {"good": Good, "bad": Bad})
    result = pipeline.run(["good", "bad"])
    assert result["count"] == 1 and "bad" in result["errors"]

    path = tmp_path / "feed.json"
    pipeline.write(result, path)
    assert json.loads(path.read_text())["count"] == 1
