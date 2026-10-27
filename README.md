# AI Pulse — Installer

This repo builds the Windows installer for [AI Pulse](https://github.com/stephanieloveshotpot/ai-news-desk), the desktop app that collects the day's AI news and pops up when you sit down at your PC. The app and its news scraper live in the app repo; this repo only contains the custom setup program that puts the app on a user's computer.

## Just want the app?

Go to [Releases](https://github.com/stephanieloveshotpot/ai-news-downloader/releases/latest), download `AI-Pulse-Setup-<version>.exe`, run it. Windows SmartScreen will likely warn because the file isn't code-signed: click **More info → Run anyway**. No Python, no admin password.

## What the installer does

It's our own Windows app (`setup/`, WPF on .NET Framework 4.8, which ships with Windows 10 and 11), not a generic installer: a borderless rounded window with a drifting aurora background, a self-drawing pulse logo, a glowing progress bar with live percentages and an animated finish. One click on **Install** copies the app, then **AI Pulse's own first-run setup** opens: the welcome, terms of service, privacy policy, look (theme, animations, background) and startup choices all happen there, in the app's design. Declining the terms uninstalls again. *Install options* lets you pick the folder and the desktop shortcut. The uninstaller is the same app without the payload, with the same look.

On the PC it creates:

| What | Where |
| --- | --- |
| The app (`AI Pulse.exe`, the bundled scraper, everything it needs) | `%LOCALAPPDATA%\Programs\AI Pulse\` |
| The app's data folder (settings, bookmarks, cached articles) | `%APPDATA%\ai-pulse\` |
| Start menu folder with app + uninstall shortcuts, desktop shortcut | Start menu, Desktop |
| "Installed apps" entry so it can be removed from Windows Settings | `HKCU\...\Uninstall\AIPulse` |
| Startup entry, set by the app when "launch with Windows" is on | `HKCU\...\CurrentVersion\Run\AI Pulse` |

Switches: none = install then open setup; `/update /relaunch` = the in-app updater (same window titled "Updating AI Pulse", keeps everything, reopens the app); `/S` = silent; `/D=C:\path` = another folder (last switch). `--preview [--page welcome|options|progress|done|error|remove]` shows the window without installing anything, and `--snap out.png --snap-at 2000` saves a screenshot of it; `/S --extract-only /D=folder` only unpacks the app (for testing).

The app's built-in updater downloads the newest `AI-Pulse-Setup-<version>.exe` from this repo's latest release, checks it against the `.sha256` file published next to it, shows an "Updating…" screen and runs it with `/update /relaunch`. **This repo must be public** (or at least its Releases must be reachable without a login) for installed apps to see updates. The uninstaller removes everything above and asks whether to delete the settings folder too.

## How it gets the app

The app repo publishes an **app bundle** zip on every release (`AI-Pulse-app-win-x64-<version>.zip`: the unpacked app, the legal texts, and a `VERSION` file). `scripts/fetch-bundle.js` downloads the newest one into `payload/`, and `setup/Setup` zips `payload/app` and embeds it, with the uninstaller, in the setup `.exe`. The installer's version number always equals the app bundle's.

```
ai-news-desk (app + scraper)  --app bundle zip-->  this repo  --setup/ (WPF)-->  AI-Pulse-Setup-<version>.exe
```

## Build it yourself (Windows)

Needs Node.js 22+ and the .NET SDK 8 or newer (`winget install Microsoft.DotNet.SDK.8`). `.\scriptsuild.ps1 -Preview` builds a look-only installer with a dummy app, for design work.

```powershell
$env:APP_TOKEN = "<GitHub token that can read ai-news-desk>"   # the app repo is private
.\scripts\build.ps1
# -> dist\AI-Pulse-Setup-<version>.exe
```

`.\scripts\build.ps1 -NoFetch` reuses whatever is already in `payload\`.

## Automatic builds

`.github/workflows/release.yml` runs on every push to `main`, when the app repo's release workflow starts it, every 6 hours as a safety net (only building app versions that have no installer yet), and on demand (Actions → "Build and publish the installer" → Run workflow). It fetches the newest app bundle, compiles the installer, silently installs and uninstalls it on the runner as a smoke test, and publishes the `.exe` under Releases.

It needs one repository secret, **`APP_TOKEN`**: a fine-grained personal access token with read-only *Contents* permission on `ai-news-desk` (Settings → Secrets and variables → Actions). Without it the download step fails with a 404, because the app repo is private.

## Layout

```
setup/src/                the installer and uninstaller: window (MainWindow.xaml), install logic (Installer.cs)
setup/Setup, setup/Uninstall  the two builds of it
assets/icon.ico           the app icon
scripts/fetch-bundle.js   downloads + unzips the newest app bundle into payload/
scripts/build.ps1         one-command local build
.github/workflows/        the automatic build + publish
```

## License

MIT, © 2026 Shako. Contact: mandarincat07@gmail.com

