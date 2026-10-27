"""Command-line entry point.

    aipulse-scraper --out feed.json
    aipulse-scraper --sources hackernews,arxiv --stdout
    aipulse-scraper --list-sources
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
from pathlib import Path

from . import __version__
from .pipeline import run, write
from .relevance import MIN_SCORE
from .sources import SOURCES


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="aipulse-scraper",
                                description="Collect AI news into one JSON feed.")
    p.add_argument("--out", type=Path, default=Path("feed.json"),
                   help="where to write the JSON feed (default: feed.json)")
    p.add_argument("--stdout", action="store_true", help="print JSON to stdout instead")
    p.add_argument("--sources", default=",".join(SOURCES),
                   help="comma-separated subset of: " + ", ".join(SOURCES))
    p.add_argument("--min-score", type=float, default=MIN_SCORE,
                   help=f"drop articles scoring below this (default {MIN_SCORE})")
    p.add_argument("--limit", type=int, default=None,
                   help="max items per source (overrides per-source defaults)")
    p.add_argument("--list-sources", action="store_true")
    p.add_argument("-v", "--verbose", action="store_true")
    p.add_argument("--version", action="version", version=__version__)
    args = p.parse_args(argv)

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(levelname)s %(name)s: %(message)s",
        stream=sys.stderr,
    )

    if args.list_sources:
        for k in SOURCES:
            print(k)
        return 0

    sources = [s.strip() for s in args.sources.split(",") if s.strip()]
    unknown = [s for s in sources if s not in SOURCES]
    if unknown:
        p.error(f"unknown source(s): {', '.join(unknown)}")

    limits = {s: args.limit for s in sources} if args.limit else None
    result = run(sources, limits, args.min_score)

    if args.stdout:
        json.dump(result, sys.stdout, ensure_ascii=False)
        sys.stdout.write("\n")
    else:
        write(result, args.out)
        logging.getLogger("aipulse").info("wrote %d articles to %s", result["count"], args.out)

    # exit 2 if every source failed, 0 otherwise (partial failures are fine)
    return 2 if result["errors"] and len(result["errors"]) == len(sources) else 0


if __name__ == "__main__":
    sys.exit(main())
