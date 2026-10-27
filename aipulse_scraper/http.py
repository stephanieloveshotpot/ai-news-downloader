"""One shared HTTP session with a polite user agent, timeouts and retries."""

from __future__ import annotations

import logging
import time

import requests

log = logging.getLogger("aipulse")

USER_AGENT = "AIPulse/1.0 (+https://github.com/stephanieloveshotpot/ai-news-scraper)"
TIMEOUT = 20
RETRIES = 2

_session = requests.Session()
_session.headers.update({"User-Agent": USER_AGENT, "Accept": "*/*"})


def get(url: str, **kwargs) -> requests.Response | None:
    """GET with retries. Returns None on persistent failure instead of raising,
    so one broken source never takes the whole run down."""
    kwargs.setdefault("timeout", TIMEOUT)
    for attempt in range(RETRIES + 1):
        try:
            resp = _session.get(url, **kwargs)
            if resp.status_code >= 500 and attempt < RETRIES:
                time.sleep(1.5 * (attempt + 1))
                continue
            resp.raise_for_status()
            return resp
        except requests.RequestException as exc:
            if attempt < RETRIES:
                time.sleep(1.5 * (attempt + 1))
                continue
            log.warning("GET %s failed: %s", url, exc)
    return None
