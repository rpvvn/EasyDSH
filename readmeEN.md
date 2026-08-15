<div align="center">

<img src="docs/EasyDSH.png" alt="EasyDSH — WPF one-click launcher for DeepSeek Harness" width="100%">

<br>

# DSH One-Click Launcher

**Skip manual CLI commands: One-click lightweight launcher for DeepSeek DSH**
> 🌐 Language: [简体中文](README.md) · **English**
---
</div>

## ✨ Key Features
- 🚀 **One-Click Start**: Automatically detect the runtime environment, launch the DSH Web service in the background, and open the browser automatically once the service is ready.
- 🔄 **One-Click Restart**: Force terminate the running service → wait for the port to be released → restart the service.
- ⏹ **Stop Service**: Globally scan all running DSH processes (Node.js processes + processes occupying port 3080) and force terminate them.
- 📦 **One-Click Install**: Run `npm install -g @deepseek-ai/dsh` to complete global installation of DSH.
- 🔍 **Environment Detection**: Detect missing environment and provide downloads,Real-time status cards with colored indicator dots showing the status of Node.js, npm, DSH (global installation / npx cache), and service port connectivity.
- 🎨 **WPF Vector UI**: Rounded buttons, color-coded status panels, and hover highlight effects.
- 📦 **Single-File Portable Release**: No installation or configuration files needed; you can store it on a USB drive and run it anywhere.
- 🛡 **Service Conflict Diagnostics**: Automatically identify plugin service name conflicts when startup fails. A popup window will show which package occupies the service and which one causes the conflict, with a one-click uninstall option for either package.
- 🔌 **Flexible Close**: When closing the window, choose "Stop & Close" or "Close Window Only" to keep the DSH service running in the background without force-stopping it.
- 🚫 **Single Instance**: Detect an already-running instance at startup and show "Already running — please do not run it again", then exit to prevent duplicate launches.
- ℹ️ **Extra Support**: NPM Domestic Mirror Source Switching, Local Download Proxy Switching, DSH Version Management

---

## 📋 Prerequisites
| Dependency | Description |
|------|------|
| Windows 7 or later | Requires .NET Framework 4.0 or higher (pre-installed on Windows 10 / 11) |
| Node.js | Bundled with npm, required for installing and running DSH |

---

## 🚀 Usage Instructions
1. Download `DSH-Launcher.exe` from [Releases](../../releases), or compile the executable manually following the guide below.
2. Double-click the file to launch the launcher.
3. Click **Install DSH** on your first launch (or confirm DSH is available via npx cache or global installation).
4. Click **One-Click Start**. Once the service is ready, your browser will automatically open and navigate to `http://127.0.0.1:3080`.

> Tip: If you close the launcher window while DSH is still running, a popup prompt lets you choose between "Stop & Close", "Close Window Only" (the service keeps running in the background), or "Cancel".

---

## 🔨 Build Guide
You do not need Visual Studio or any extra SDK. The built-in .NET Framework 4.0 compiler `csc.exe` on Windows can complete compilation.

---

### Option 1: One-Click Build
```bat
build.bat
```

---

### Option 2: Manual compile

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

> For 32‑bit Windows, replace `Framework64` with `Framework` in all file paths.

---

### Cut a new release

```bash
git tag v1.0.0
git push origin v1.0.0
```

After pushing the tag to remote repository, the CI workflow will automatically compile `DSH-Launcher.exe` and attach it to a new GitHub Release.

---

## ⚙️ How It Works

| Action | Implementation Details |
| --- | --- |
| Start | Prefer globally installed `dsh web`; fallback to `npx --yes @deepseek‑ai/dsh web`. |
| Stop | Use WMI to scan node processes whose command line contains `deepseek‑ai`, pair with netstat to locate processes bound to port 3080, then execute `taskkill /F /T` for forced termination. |
| Environment Check | Run `node -v` / `npm -v` / `npm config get prefix\|cache` / port 3080 connectivity check |
| Listening Port | Default: `127.0.0.1:3080` |
| Conflict Diagnosis | Capture runtime output from `dsh web`. Use regex to identify Cordis service‑name collision, parse conflicting package names, display pop‑up alert with one‑click uninstall options. |

---

## 📁 Repository Structure

```
├── .github/workflows/build.yml   # Automated build & release workflow
├── DshLauncher.cs                # Full source code
├── icon.ico                      # Application icon
├── build.bat                     # One‑click build script
├── CHANGELOG.md                  # Version changelog
├── README.md                     # Chinese documentation
├── readmeEN.md                   # English documentation (this file)
├── patches/                      # Supplementary DSH patch files
└── .gitignore
```

---

## 🔧 Troubleshooting

- **Node.js not detected**: Install [Node.js](https://nodejs.org/) first.
- **Browser page still accessible after clicking Stop Service**: Expected behavior. Refresh your browser and the service will be unreachable.
- **Icon missing**: Ensure `icon.ico` sits in the same directory as source files during compilation.
- **Launch fails with "plugin service conflict"**: Two plugins register identical Cordis service names (common example: `pet`). Uninstall one of the conflicting packages following the pop‑up instructions. Corresponding DSH error‑message improvement patch is available at `patches/dsh‑cordis‑service‑collision‑message.patch`.

  

---
<div align="center">

### Community [Linux.do](https://linux.do)
</div>

<div align="center">

**MIT License**
</div>

