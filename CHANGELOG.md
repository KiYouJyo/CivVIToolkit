# Changelog

本项目使用语义化版本思路记录用户可见变化。开发阶段的验收构建可能继续使用同一 Preview 版本号，但正式 Release 必须对应明确的版本标签。

## [Unreleased]

### Added
- x64 MSIX 单项目打包基础、Package Identity、`runFullTrust` 与三语 Manifest 资源声明。
- Signed MSIX Acceptance：自动构建、签名、完整验签、包内 Manifest 校验与 SHA-256 验收产物。
- one-click 安装 ZIP：当前用户证书信任、`Add-AppxPackage` 安装辅助与三语说明。
- `release/release.json` 版本 / 发布通道元数据。
- 中 / 日 / 英三语 Release Notes 结构。
- `Publish GitHub Release` 正式发布工作流与 dry-run 模式。
- `Release Orchestrator` 一键发布入口，负责不可变版本 Tag 和正式发布工作流调度。

### Changed
- Signed Acceptance 从“解压后直接运行的 self-contained WinUI 3 目录”迁移为签名 MSIX。
- Release 结构对齐 UrbanPlanToolbox：版本元数据、签名产物、one-click 包、SHA-256、三语说明和幂等 Release reconciliation。

## [0.1.0] - 2026-08-17

### Added
- WinUI 3 工具箱基础与模块化导航。
- Steam / Epic Games 安装自动发现。
- DX11 / DX12 运行时自动识别。
- Civilization VI 进程监测与版本信息读取。
- 进程内存读写与 AoB 特征码扫描基础。
- 经典 22 项 Trainer 功能目录、快捷键与 Signature gating 契约。
- 只读运行时 Build Fingerprint / Diagnostics。
- Save Manager、Maps & Game Info、Mod Manager、Quick Launch 的模块边界。
