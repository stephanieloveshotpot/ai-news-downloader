"""PyInstaller entry point (absolute import; __main__.py can't be used because
PyInstaller runs the entry file as a top-level script)."""
import sys

from aipulse_scraper.cli import main

if __name__ == "__main__":
    sys.exit(main())
