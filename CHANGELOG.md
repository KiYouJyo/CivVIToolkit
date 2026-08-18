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

## [0.3.0] - 2026-08-18

### Added
- 按 Figma v2 设计重建的浅色 WinUI 3 主界面、48 px 自定义标题栏与 220 px 可折叠侧栏。
- 首页中的 Steam / Epic、DX11 / DX12、PID、版本、路径、重新扫描与快捷启动接线。
- 第一版 22 项修改器的四类自动分组、分类切换与搜索。
- 仅包含 22 项修改器入口的置顶紧凑窗口。
- 独立的“预设与诊断”和“设置与关于”页面。

### Changed
- 导航按最新 Figma 收束为：首页、修改器、存档管理、Mod 管理、百科，以及底部固定的预设与诊断、设置与关于。
- 存档管理、Mod 管理、百科保持为空入口，不展示尚未实现的模拟数据。
- Trainer 未通过运行时签名验收前继续保持只读 / 禁用写入。
- 应用、MSIX 与 Release Contract 统一为 0.3.0 / 0.3.0.0。

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
