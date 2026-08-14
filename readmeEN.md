# DSH Launcher

> 🌐 Language / 语言：**English** · [简体中文](README.md)

A single-file, portable Windows GUI tool to launch, restart, and stop the **DSH (DeepSeek Harness)** web service — no more typing `npx @deepseek-ai/dsh web` every time.

Built with **WPF** (.NET Framework 4.0) — vector-rendered UI, zero third-party dependencies, double-click to run.

---

## ✨ Features

- 🚀 **One-click start** — detects the environment and launches the DSH web service in the background, auto-opens the browser once ready
- 🔄 **One-click restart** — force-stops the current service → waits for the port to be released → restarts
- ⏹ **Stop service** — globally scans all running DSH processes (node processes + port 3080) and force-kills them
- 📦 **One-click install** — runs `npm install -g @deepseek-ai/dsh`
- 🔍 **Environment detection** — real-time status of Node.js / npm / DSH (global / npx cache) / service port, shown as colored-dot cards
- 🎨 **WPF vector UI** — rounded buttons, colored status cards, hover highlight
- 📦 **Single-file portable** — no install, no config files, run from anywhere (e.g. a USB drive)
- 🛡 **Service collision diagnosis** — on startup failure, detects plugin service-name collisions and shows a dialog naming the owner / claimant with one-click uninstall

## 📋 Requirements

| Dependency | Description |
|------|------|
| Windows 7+ | .NET Framework 4.0+ (pre-installed on Windows 10/11) |
| Node.js | includes npm, used to run / install DSH |

## 🚀 Usage

1. Download `DSH-Launcher.exe` from [Releases](../../releases), or build it yourself (see below)
2. Double-click to run
3. On first use, click **Install DSH** (or make sure DSH is already installed globally / cached via npx)
4. Click **Start**, the browser opens `http://127.0.0.1:3080` automatically once ready

> Note: when closing the launcher while DSH is still running, a dialog asks whether to stop it too.

## 🔨 Build

No Visual Studio or SDK required — use the built-in .NET Framework 4.0 compiler (`csc.exe`).

### Option 1: one-click build

```bat
build.bat
```

### Option 2: manual build

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /nologo /codepage:65001 /target:winexe /win32icon:icon.ico ^
  /out:DSH-Launcher.exe ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Management.dll" ^
  DshLauncher.cs
```

> For 32-bit systems, replace `Framework64` with `Framework`.

## 🤖 GitHub Actions

This repo includes a workflow (`.github/workflows/build.yml`) that automatically builds the EXE on every push and uploads it as an artifact. When you push a `v*` tag (e.g. `v1.0.0`), it also creates a GitHub Release with the EXE attached and auto-generated release notes.

### How to release

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow builds `DSH-Launcher.exe` and publishes it to a new Release automatically.

## ⚙️ How it works

| Action | Implementation |
|------|------|
| Start | Prefer global `dsh web`, fallback to `npx --yes @deepseek-ai/dsh web` |
| Stop | WMI scans node processes with `deepseek-ai` in the command line + `netstat` finds the process on port 3080, then `taskkill /F /T` |
| Detect | `node -v` / `npm -v` / `npm config get prefix|cache` / port 3080 connectivity |
| Port | Default `127.0.0.1:3080` |
| Collision diagnosis | Captures `dsh web` output, regex-matches service-name collisions, resolves owner/claimant package names, and shows a dialog with one-click uninstall |

## 📁 Project structure

```
├── .github/workflows/build.yml   # CI: build + release
├── DshLauncher.cs                # full source (single file)
├── icon.ico                      # app icon
├── build.bat                     # one-click build script
├── CHANGELOG.md                  # release notes
├── README.md                     # 中文文档 (Chinese)
├── readmeEN.md                   # English docs (this file)
├── patches/                      # bundled DSH patches
└── .gitignore
```

## 🔧 FAQ

- **"Node.js not detected"**: install [Node.js](https://nodejs.org/) first.
- **Browser still opens after "Stop"**: normal — refresh the page and it becomes unavailable.
- **Icon not showing**: make sure `icon.ico` is in the same directory when building.
- **"Plugin service collision" on startup**: two plugins registered the same Cordis service (commonly `pet`). Uninstall one of them as prompted; see the DSH patch at `patches/dsh-cordis-service-collision-message.patch`.

## 📄 License

MIT
