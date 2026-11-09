<p align="center">
  <img src="assets/logo.png" width="96" height="96" alt="Quillwave icon">
</p>

<h1 align="center">Quillwave for Windows</h1>

<p align="center">
  All your AI news in one sitting: nearly 100 sources in one feed, with an assistant that briefs you in a minute.<br>
  This repository builds and publishes the Windows installer.
</p>

<p align="center">
  <a href="https://quillwave-app.web.app/beta"><b>Get Quillwave</b></a>
  &nbsp;·&nbsp;
  <a href="https://quillwave-app.web.app">Website</a>
  &nbsp;·&nbsp;
  <a href="https://quillwave-app.web.app/beta">Open beta</a>
  &nbsp;·&nbsp;
  <a href="https://quillwave-app.web.app/status">Status</a>
</p>

---

## Install

1. Go to [quillwave-app.web.app/beta](https://quillwave-app.web.app/beta), sign in (free, with Google or your email) and press **Download for Windows**.
2. Run it. If Windows shows *"Windows protected your PC"*, click **More info**, then **Run anyway**. The installer isn't code-signed yet, so SmartScreen is cautious with it.
3. Click **Install**. Quillwave opens with a one-minute setup.

| | |
| --- | --- |
| **Works on** | Windows 10 (1903 or later) and Windows 11, 64-bit |
| **Size** | About 170 MB |
| **Admin rights** | Not needed: it installs for your Windows account only |
| **Updates** | Automatic. Quillwave checks this repository's releases, downloads the new installer, verifies it and restarts itself |
| **Uninstall** | Settings → Apps → Installed apps → Quillwave, or *Uninstall Quillwave* in the Start menu |

> **Which file?** Use `Quillwave-Setup-<version>.exe`. `AI-Pulse-Setup-<version>.exe` is the same installer under the app's old name, kept so very old copies can still update. The `.sha256` files are checksums.

### Check your download (optional)

```powershell
(Get-FileHash .\Quillwave-Setup-1.26.2.exe -Algorithm SHA256).Hash
```

The result should match the matching `.sha256` file on the release page. Every release is also scanned by VirusTotal; the result for your exact copy is in the app under **Settings → Security**.

## What the installer does

The installer is a small Windows program written for Quillwave (WPF on .NET Framework 4.8, which ships with Windows), not a generic setup wizard.

- **Install:** shows where Quillwave goes (you can change it), lets you choose a desktop shortcut, then copies the app and opens it.
- **Update:** started by the app itself. It shows what's new while it works and reopens Quillwave when it's done. Your settings, notes and account stay as they are.
- **Uninstall:** lists exactly what will be removed and what stays, and lets you choose whether to delete your data on this PC.

What it puts on your PC:

| What | Where |
| --- | --- |
| The app, including its news scanner | `%LOCALAPPDATA%\Programs\Quillwave\` |
| Your settings, bookmarks and offline notes | `%APPDATA%\ai-pulse\` |
| Start menu entry, and a desktop shortcut if you chose one | Start menu, Desktop |
| The *Installed apps* entry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\AIPulse` |
| Starting with Windows (only if you turn it on in the app) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Quillwave` |

Nothing is installed system-wide, and there are no add-ons, toolbars or background services. (Some names still say *AI Pulse*, the app's earlier name, so existing installs keep working.)

### Command-line options

| Option | What it does |
| --- | --- |
| *(none)* | Normal install with the window |
| `/S` | Silent install |
| `/D=C:\path\Quillwave` | Install to another folder (must be the last option) |
| `/update /relaunch` | What the in-app updater uses: update in place, then reopen the app |
| `--preview --page welcome\|progress\|done\|update\|update-done\|remove\|remove-done\|error` | Show a screen without installing anything (for design work) |
| `--snap out.png --snap-at 1500` | With `--preview`: save a screenshot of the window after 1.5 s |

## How a release is made

```
Quillwave app release  ──►  app bundle (zip)  ──►  this repository  ──►  Quillwave-Setup-<version>.exe
```

1. Each Quillwave release publishes an app bundle: the unpacked app, its release notes and a version file.
2. [`release.yml`](.github/workflows/release.yml) downloads the newest bundle, builds the uninstaller and the installer (which embeds the app as one compressed payload), installs and uninstalls it silently as a smoke test, and publishes the installer with its checksum. The release page lists that version's *What's new*.
3. It runs when the app is released, on every push to `main`, every 6 hours as a safety net (only for versions without an installer yet), and on demand from the Actions tab.

The installer's version number always matches the app's. This repository has to stay public so installed copies can see new releases.

## Build it yourself

You need Windows, Node.js 22 or newer and the .NET SDK 8 or newer (`winget install Microsoft.DotNet.SDK.8`).

```powershell
.\scripts\build.ps1 -Preview     # look-only installer with a dummy app, for design work
.\scripts\build.ps1 -NoFetch     # build from whatever is already in payload\
.\scripts\build.ps1              # fetch the newest app bundle, then build (needs APP_TOKEN, below)
# -> dist\Quillwave-Setup-<version>.exe
```

Fetching the app bundle needs a GitHub token in `APP_TOKEN` that can read the app's (private) repository. The automatic build uses a repository secret of the same name: a fine-grained token with read-only *Contents* access.

## Layout

```
setup/src/            the installer and uninstaller (one WPF app): MainWindow.xaml, Installer.cs
setup/Setup/          the installer build, with the app payload embedded
setup/Uninstall/      the uninstaller build
assets/               icon.ico and logo.png
scripts/build.ps1     one-command local build
scripts/fetch-bundle.js  downloads and unpacks the newest app bundle into payload/
.github/workflows/    the automatic build and release
```

## Help and contact

- Something not working? Use **Ctrl K → Send feedback** in the app, or email [quillwaveofficial@gmail.com](mailto:quillwaveofficial@gmail.com).
- Is the service up? See the [status page](https://quillwave-app.web.app/status).

## License

The installer in this repository is [MIT licensed](LICENSE), © 2026 Shako.

The Quillwave app itself is not open source. Using it is covered by its [terms of service](https://quillwave-app.web.app/terms) and [privacy policy](https://quillwave-app.web.app/privacy).
