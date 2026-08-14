# Changelog · 更新说明

所有重要的项目变更都会记录在此文件中。
All notable changes to this project will be documented in this file.

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。
Format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.0.0] - 2026-08-15

### Added · 新增

- 🚀 一键启动 / 重启 / 停止 DSH Web 服务
  One-click start / restart / stop for the DSH web service
- 🔍 环境检测：Node.js / npm / DSH（全局 / npx 缓存）/ 服务端口，彩色圆点状态卡片
  Environment detection: Node.js / npm / DSH (global / npx cache) / service port, shown as colored-dot status cards
- 📦 一键全局安装 DSH（`npm install -g @deepseek-ai/dsh`）
  One-click global install of DSH
- ⏹ 停止服务：全局扫描 node 进程 + 3080 端口，`taskkill /F /T` 强制结束
  Stop service: globally scan node processes + port 3080, force-kill via `taskkill /F /T`
- 🎨 WPF 矢量界面：圆角按钮、悬停高亮、彩色圆点
  WPF vector UI: rounded buttons, hover highlight, colored dots
- 📦 单文件绿色版，文件图标 + 窗口图标
  Single-file portable build with file + window icon
- 🤖 GitHub Actions 自动编译与 Release 发布
  GitHub Actions for automatic build and release
