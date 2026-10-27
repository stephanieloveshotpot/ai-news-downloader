# AI Pulse — Scraper

The Python half of [AI Pulse](https://github.com/stephanieloveshotpot/ai-news-desk), a desktop app that shows you AI news. This program goes out to a handful of public websites, collects the latest AI-related stories, and writes them all into one file (`feed.json`) that the desktop app reads and displays.

You don't need to run this yourself. The desktop app ships with it built in. This repo is here so you can see how it works, change the sources, or run it on its own.

## What it collects

| Source group | Where it looks | Notes |
| --- | --- | --- |
| Hacker News | Algolia HN search API | Front page + keyword search over the last 48 h |
| arXiv | arXiv API | New papers in cs.AI, cs.LG, cs.CL |
| Company blogs | RSS | Anthropic, OpenAI, Google DeepMind, Google AI, Meta AI, Hugging Face, Microsoft AI, NVIDIA |
| Tech press | RSS | TechCrunch AI, The Verge AI, MIT Technology Review, VentureBeat AI, Ars Technica, WIRED |

Everything comes from official public APIs or public RSS feeds. Nothing is logged in, nothing is scraped from behind a login, and the program identifies itself with a clear user agent.

## How it works, in plain English

1. **Fetch.** Each source is asked for its latest items, all four groups at the same time (`aipulse_scraper/sources/`).
2. **Normalise.** Every item becomes the same shape, an `Article` with a title, link, source, date, summary and a few tags (`models.py`).
3. **Score.** A keyword scorer rates how AI-related each item is from 0 to 100 (`relevance.py`). Sources that are AI-only by nature (arXiv, company blogs) get a floor so they're never dropped.
4. **Dedupe.** The same link seen on Hacker News and in a blog's RSS is merged into one card, keeping the HN points (`pipeline.py`).
5. **Write.** The result is saved as `feed.json`, newest first.

If one site is down, the others still work; the run reports which source failed in the JSON's `errors` field.

## Run it yourself

```bash
pip install -e .
aipulse-scraper --out feed.json          # all sources
aipulse-scraper --sources hackernews,arxiv --stdout
aipulse-scraper --list-sources
aipulse-scraper --help
```

Python 3.10 or newer.

## Build the standalone executable

```bash
pip install -e ".[dev]"
pyinstaller aipulse-scraper.spec
# -> dist/aipulse-scraper  (or aipulse-scraper.exe on Windows)
```

The GitHub Actions workflow in `.github/workflows/release.yml` does this automatically on Windows whenever a tag like `v1.0.0` is pushed, and attaches the `.exe` to a Release. The desktop app's installer build pulls it from there.

## Change the sources

- Add or remove RSS feeds in `aipulse_scraper/sources/rss.py` (`BLOG_FEEDS`, `PRESS_FEEDS`).
- Change the Hacker News search terms in `sources/hackernews.py` (`QUERIES`).
- Change arXiv categories in `sources/arxiv.py` (`CATEGORIES`).
- Tune what counts as "AI-related" in `relevance.py`.

## Tests

```bash
pytest
```

The tests run fully offline using sample feeds, so they pass even without internet.

## Output format

```json
{
  "version": "1.0.0",
  "generated_at": "2026-10-27T09:00:00+00:00",
  "sources": ["hackernews", "arxiv", "blogs", "press"],
  "errors": {},
  "count": 123,
  "articles": [
    {
      "id": "3f2a…",
      "title": "…",
      "url": "https://…",
      "source": "hackernews",
      "source_label": "Hacker News",
      "category": "news",
      "published": "2026-10-27T08:12:00+00:00",
      "summary": "…",
      "author": "…",
      "score": 72.0,
      "tags": ["llm", "agents"],
      "comments_url": "https://news.ycombinator.com/item?id=…",
      "points": 240
    }
  ]
}
```

## License

MIT, © 2026 Shako. Contact: mandarincat07@gmail.com
