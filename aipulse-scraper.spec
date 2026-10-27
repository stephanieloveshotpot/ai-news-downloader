# PyInstaller spec: builds a single-file executable with no Python needed
# on the target machine.  Run:  pyinstaller aipulse-scraper.spec
# Output: dist/aipulse-scraper(.exe)

block_cipher = None

a = Analysis(
    ["aipulse_scraper/__main__.py"],
    pathex=["."],
    binaries=[],
    datas=[],
    hiddenimports=["feedparser", "sgmllib3k", "requests", "charset_normalizer", "certifi"],
    hookspath=[],
    runtime_hooks=[],
    excludes=["tkinter", "unittest", "pydoc", "doctest"],
    cipher=block_cipher,
)
pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)
exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name="aipulse-scraper",
    debug=False,
    strip=False,
    upx=False,
    console=True,
    icon=None,
)
