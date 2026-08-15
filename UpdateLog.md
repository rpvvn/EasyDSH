# Changelog

本文件记录本项目的所有重要变更，并作为 GitHub Release 的更新说明（Release notes）。
All notable changes to this project will be documented in this file, which is also used as the GitHub Release notes.

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，本项目遵循[语义化版本](https://semver.org/lang/zh-CN/)。
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-08-15

### Added

- 一键启动 / 重启 / 停止 DSH Web 服务
- 环境检测：Node.js / npm / DSH（全局 / npx 缓存）/ 服务端口，彩色圆点卡片展示
- 一键全局安装 DSH（`npm install -g @deepseek-ai/dsh`）
- 停止服务：全局扫描 node 进程 + 3080 端口，`taskkill /F /T` 强制结束
- WPF 矢量界面：圆角按钮、悬停高亮、彩色状态圆点
- 标题栏快捷按钮：国内镜像源 / 检查更新 / 本地代理 / 关于
- 插件服务冲突检测，提供「卸载冲突方 / 卸载占用方」双选项
- DSH 版本检测（`dsh --version`）与最新版本对比
- 本地代理按钮：写入 PowerShell 配置文件，提供 `ep`（开代理）/ `dp`（关代理）函数
- 单实例运行保护（Mutex）
- GitHub Actions 自动编译与 Release 发布

### Changed

- 界面由 WinForms 迁移至 WPF（矢量渲染，解决圆角/阴影绘制异常）

### Fixed

- 修复 pnpm 输出中文乱码（统一使用 UTF-8 解码）
- 修复间接依赖无法直接卸载的问题（自动回退到直接依赖）
