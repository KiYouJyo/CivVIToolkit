# CivVIToolkit

[日本語](README.ja.md) · [English](README.en.md)

CivVIToolkit 是一个面向 Civilization VI 的模块化 Windows 工具箱，当前重点为单机修改器，并为存档管理、地图/游戏信息、Mod 管理和快捷启动等能力预留独立模块。

## 当前状态

- WinUI 3 / .NET 10 / x64 / MSIX
- 自动识别 Steam / Epic Games 安装信息
- 自动识别 DX11 / DX12 运行进程
- 三语基础：简体中文 / 日本語 / English
- Steam + DX12 + Gathering Storm `1.0.12.68 (1023995)` 精确 GameCore Profile
- 22 项单机修改器功能进入真实游戏验收阶段
- v0.2.2 修复运行中 GameContext 根指针暂时为空导致修改器整体失败的问题
- Signed MSIX Acceptance 与 GitHub Release 工作流

## 已验证的基础链

- `GameCore_XP2_FinalRelease.dll` SHA-256 与 13 个 AoB 锚点
- Local Player / Treasury / Religion / Influence 读取链
- Gold / Faith 与游戏 UI 实测数值一致
- PageUp：增加金钱 +10,000 已实机验证有效

当前 Profile 仅针对已验证的精确 Build 开放写入。未知 GameCore SHA 或运行时锚点不匹配时会拒绝修改。

## 模块

- 概览 / 游戏检测
- 单机修改器
- 存档管理（规划）
- 地图与游戏信息（规划）
- Mod 管理（规划）
- 快捷启动（规划）
- 设置

## 开发与发布

仓库包含：

- Windows CI：Restore / Build / Test / 三语资源一致性检查
- 自动签名 Signed MSIX Acceptance 验收包
- 一键安装包
- `release/release.json` 中央发布元数据
- Release Orchestrator 与 GitHub Release 发布工作流

当前仍为 Preview。真实修改器功能必须完成单机实机验收后才会标记为稳定。

## 安全范围

CivVIToolkit 仅面向单机游戏工具与本地辅助能力，不面向多人作弊，也不实现反作弊绕过。

## License

MIT
