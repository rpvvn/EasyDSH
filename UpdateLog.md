# Changelog

本文件记录本项目的所有重要变更，并作为 GitHub Release 的更新说明（Release notes）。
All notable changes to this project will be documented in this file, which is also used as the GitHub Release notes.

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，本项目遵循[语义化版本](https://semver.org/lang/zh-CN/)。
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-08-16

### Added

- 新增一些BUG

### Changed

- 提升交互感，重构页面语言逻辑

### Fixed

- 修复了一些bug

## [2.0.0] - 2026-08-15

### Added

- 标题栏快捷按钮：国内镜像源（切换 npmmirror）、检查更新（对比最新版本）、本地代理、关于
- Node.js 未安装时自动弹窗提示，可一键跳转官方下载页
- 本地代理按钮：向 PowerShell `$PROFILE` 写入 `ep`（开代理）/ `dp`（关代理）函数，并附删除说明
- 环境检测日志新增「DSH版本」字段

### Changed

- 检查更新由关于窗口移至标题栏，关于窗口精简（移除版本显示）
- 更新文档统一为 `UpdateLog.md`（Keep a Changelog 格式），并作为 Release notes 自动填充；删除 `CHANGELOG.md`

### Fixed

- 插件服务冲突检测提供「卸载冲突方 / 卸载占用方」双选项（此前仅显示卸载冲突方）
- 间接依赖无法直接卸载时，自动回退到其直接依赖再卸载
- 修复 pnpm 输出中文乱码（统一使用 UTF-8 解码子进程输出）

## [1.0.0] - 2026-08-15

### Added

- 一键启动 / 重启 / 停止 DSH Web 服务
- 环境检测：Node.js / npm / DSH（全局 / npx 缓存）/ 服务端口，彩色圆点卡片展示
- 一键全局安装 DSH（`npm install -g @deepseek-ai/dsh`）
- 停止服务：全局扫描 node 进程 + 3080 端口，`taskkill /F /T` 强制结束
- WPF 矢量界面：圆角按钮、悬停高亮、彩色状态圆点
- 插件服务冲突检测
- DSH 版本检测（`dsh --version`）
- 单实例运行保护（Mutex）
- GitHub Actions 自动编译与 Release 发布

### Changed

- 界面由 WinForms 迁移至 WPF（矢量渲染，解决圆角/阴影绘制异常）
