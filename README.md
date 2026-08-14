# DSH Launcher · DSH 一键启动器

A single-file, portable Windows GUI tool to launch, restart, and stop the **DSH (DeepSeek Harness)** web service — no more typing `npx @deepseek-ai/dsh web` every time.

一个单文件、绿色便携的 Windows GUI 小工具，用于启动、重启、停止 **DSH（DeepSeek Harness）** Web 服务，省去每次手动输入 `npx @deepseek-ai/dsh web` 的麻烦。

Built with **WPF** (.NET Framework 4.0) — vector-rendered UI, zero third-party dependencies, double-click to run.

基于 **WPF**（.NET Framework 4.0）构建，矢量渲染界面，无需任何第三方依赖，双击即用。

---

## ✨ Features · 功能特性

- 🚀 **One-click start** — detects the environment and launches the DSH web service in the background, auto-opens the browser once ready
  **一键启动**：自动检测环境并后台启动 DSH Web 服务，服务就绪后自动打开浏览器
- 🔄 **One-click restart** — force-stops the current service → waits for the port to be released → restarts
  **一键重启**：强制停止当前服务 → 等待端口释放 → 重新启动
- ⏹ **Stop service** — globally scans all running DSH processes (node processes + port 3080) and force-kills them
  **停止服务**：全局扫描所有运行中的 DSH 进程（node 进程 + 3080 端口），强制结束
- 📦 **One-click install** — runs `npm install -g @deepseek-ai/dsh`
  **一键安装**：执行 `npm install -g @deepseek-ai/dsh` 全局安装
- 🔍 **Environment detection** — real-time status of Node.js / npm / DSH (global / npx cache) / service port, shown as colored-dot cards
  **环境检测**：实时展示 Node.js / npm / DSH（全局 / npx 缓存）/ 服务端口 状态，彩色圆点卡片
- 🎨 **WPF vector UI** — rounded buttons, colored status cards, hover highlight
  **WPF 矢量界面**：圆角按钮、彩色状态卡片、悬停高亮
- 📦 **Single-file portable** — no install, no config files, run from anywhere (e.g. a USB drive)
  **单文件绿色版**：无需安装、无需配置文件，可放入 U 盘随处运行

## 📋 Requirements · 环境依赖

| Dependency · 依赖 | Description · 说明 |
|------|------|
| Windows 7+ | .NET Framework 4.0+ (pre-installed on Windows 10/11) · 需 .NET Framework 4.0+（Windows 10/11 已自带） |
| Node.js | includes npm, used to run/install DSH · 含 npm，用于运行/安装 DSH |

## 🚀 Usage · 使用方法

1. Download `DSH-Launcher.exe` from [Releases](../../releases), or build it yourself (see below)
   从 [Release](../../releases) 下载 `DSH-Launcher.exe`，或按下方说明自行编译
2. Double-click to run
   双击运行
3. On first use, click **Install DSH** (or make sure DSH is already installed globally / cached via npx)
   首次使用点「**安装 DSH**」（或确保 DSH 已通过 `npx` 缓存 / 全局安装）
4. Click **Start**, the browser opens `http://127.0.0.1:3080` automatically once ready
   点「**一键启动**」，服务就绪后自动打开浏览器访问 `http://127.0.0.1:3080`

> Note: when closing the launcher while DSH is still running, a dialog asks whether to stop it too.
> 提示：关闭启动器窗口时，若 DSH 仍在运行会弹窗询问是否一并停止。

## 🔨 Build · 编译

No Visual Studio or SDK required — use the built-in .NET Framework 4.0 compiler (`csc.exe`).
无需安装 Visual Studio 或任何 SDK，Windows 自带的 .NET Framework 4.0 编译器（`csc.exe`）即可编译。

### Option 1: one-click build · 方式一：一键编译

```bat
build.bat
```

### Option 2: manual build · 方式二：手动编译

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
> 32 位系统请把路径中的 `Framework64` 替换为 `Framework`。

## 🤖 GitHub Actions · 自动编译

This repo includes a workflow (`.github/workflows/build.yml`) that automatically builds the EXE on every push and uploads it as an artifact. When you push a `v*` tag (e.g. `v1.0.0`), it also creates a GitHub Release with the EXE attached and auto-generated release notes.

本仓库包含 GitHub Actions 工作流（`.github/workflows/build.yml`），每次 push 会自动编译 EXE 并上传为构建产物；当你推送 `v*` 标签（如 `v1.0.0`）时，还会自动创建 GitHub Release，附带 EXE 并自动生成更新说明。

### How to release · 如何发布版本

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow builds `DSH-Launcher.exe` and publishes it to a new Release automatically.
推送后，工作流会自动编译 `DSH-Launcher.exe` 并发布到新的 Release。

## ⚙️ How it works · 工作原理

| Action · 动作 | Implementation · 实现 |
|------|------|
| Start · 启动 | Prefer global `dsh web`, fallback to `npx --yes @deepseek-ai/dsh web` · 优先全局 `dsh web`，否则 `npx --yes @deepseek-ai/dsh web` |
| Stop · 停止 | WMI scans node processes with `deepseek-ai` in the command line + `netstat` finds the process on port 3080, then `taskkill /F /T` · WMI 扫描命令行含 `deepseek-ai` 的 node 进程 + `netstat` 找 3080 端口进程，`taskkill /F /T` 强制结束 |
| Detect · 检测 | `node -v` / `npm -v` / `npm config get prefix|cache` / port 3080 connectivity · 端口 3080 连通性检测 |
| Port · 端口 | Default `127.0.0.1:3080` (same as DSH Web GUI) · 默认 `127.0.0.1:3080` |

## 📁 Project structure · 目录结构

```
├── .github/workflows/build.yml   # CI: build + release · 自动编译与发布
├── DshLauncher.cs                # full source (single file, code-only UI, no XAML) · 全部源码
├── icon.ico                      # app icon · 应用图标
├── build.bat                     # one-click build script · 一键编译脚本
├── CHANGELOG.md                  # release notes · 更新说明
├── README.md
└── .gitignore
```

## 🔧 FAQ · 常见问题

- **"Node.js not detected" / 提示"未检测到 Node.js"**: install [Node.js](https://nodejs.org/) first · 请先安装 Node.js。
- **Browser still opens after "Stop" / 点「停止服务」后浏览器仍能打开**: normal — refresh the page and it becomes unavailable · 属正常现象，刷新页面后即失效。
- **Icon not showing / 图标不显示**: make sure `icon.ico` is in the same directory when building · 编译时确保 `icon.ico` 与源码在同一目录。

## 📄 License · 许可证

MIT
