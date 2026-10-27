# AI Pulse — Installer

This repo builds the Windows installer for [AI Pulse](https://github.com/stephanieloveshotpot/ai-news-desk), the desktop app that collects the day's AI news and pops up when you sit down at your PC. The app and its news scraper live in the app repo; this repo only contains the custom setup program that puts the app on a user's computer.

## Just want the app?

Go to [Releases](https://github.com/stephanieloveshotpot/ai-news-scraper/releases/latest), download `AI-Pulse-Setup-<version>.exe`, run it. Windows SmartScreen will likely warn because the file isn't code-signed: click **More info → Run anyway**. No Python, no admin password.

## What the installer does

It's a hand-written [NSIS](https://nsis.sourceforge.io) script (`installer/AIPulse.nsi`) with these pages:

1. Welcome
2. Terms of service (must tick "I accept")
3. Privacy policy (must tick "I accept")
4. Startup options: launch with Windows; pop up on login / every 4 hours / never
5. Install location (defaults to the per-user programs folder, so no admin rights)
6. Install, then Finish with an "Open AI Pulse now" option

On the PC it creates:

| What | Where |
| --- | --- |
| The app (`AI Pulse.exe`, the bundled scraper, everything it needs) | `%LOCALAPPDATA%\Programs\AI Pulse\` |
| The app's data folder, with `installer.json` carrying the startup choices for first launch | `%APPDATA%\ai-pulse\` |
| Start menu folder with app + uninstall shortcuts, desktop shortcut | Start menu, Desktop |
| "Installed apps" entry so it can be removed from Windows Settings | `HKCU\...\Uninstall\AIPulse` |
| Startup entry (only if ticked) | `HKCU\...\CurrentVersion\Run\AI Pulse` |

Re-running a newer installer upgrades in place (it closes the running app first). The uninstaller removes everything above and asks whether to delete the settings/bookmarks folder too.

## How it gets the app

The app repo publishes an **app bundle** zip on every release (`AI-Pulse-app-win-x64-<version>.zip`: the unpacked app, the legal texts, and a `VERSION` file). `scripts/fetch-bundle.js` downloads the newest one into `payload/`, and NSIS packs `payload/app` into the setup `.exe`. The installer's version number always equals the app bundle's.

```
ai-news-desk (app + scraper)  --app bundle zip-->  this repo  --NSIS-->  AI-Pulse-Setup-<version>.exe
```

## Build it yourself (Windows)

Needs Node.js 22+ and NSIS 3 (`makensis` on PATH; `winget install NSIS.NSIS`).

```powershell
$env:APP_TOKEN = "<GitHub token that can read ai-news-desk>"   # the app repo is private
.\scripts\build.ps1
# -> dist\AI-Pulse-Setup-<version>.exe
```

`.\scripts\build.ps1 -NoFetch` reuses whatever is already in `payload\`.

## Automatic builds

`.github/workflows/release.yml` runs on every push to `main`, every Monday, and on demand (Actions → "Build and publish the installer" → Run workflow). It fetches the newest app bundle, compiles the installer, silently installs and uninstalls it on the runner as a smoke test, and publishes the `.exe` under Releases.

It needs one repository secret, **`APP_TOKEN`**: a fine-grained personal access token with read-only *Contents* permission on `ai-news-desk` (Settings → Secrets and variables → Actions). Without it the download step fails with a 404, because the app repo is private.

## Layout

```
installer/AIPulse.nsi     the installer: pages, files, folders, shortcuts, registry, uninstaller
assets/                   icon.ico and the sidebar image shown on the welcome/finish pages
scripts/fetch-bundle.js   downloads + unzips the newest app bundle into payload/
scripts/build.ps1         one-command local build
.github/workflows/        the automatic build + publish
```

## License

MIT, © 2026 Shako. Contact: mandarincat07@gmail.com
