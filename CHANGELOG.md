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

## [0.3.5] - 2026-08-18

### Added
- “增加影响力”和“增加金钱”在主修改器与紧凑修改器中新增明确的“应用”按钮；输入值与全局快捷键继续共享同一配置。

### Fixed
- 修改器 22 个条目改为稳定 Observable ViewModel，只创建一次；后台轮询只原位更新属性，不再周期性重新设置 `ItemsSource`。
- 主修改器与紧凑修改器共享同一批 ViewModel，紧凑窗口刷新状态时不再重建列表。

### Changed
- Trainer 行使用 `INotifyPropertyChanged` 更新名称、状态、开关、可用性和输入值；分类或搜索由用户主动变化时才重新筛选列表。
- 保留 v0.3.4 的进程 / GameCore 轮询去抖作为会话层稳定保护。

## [0.3.4] - 2026-08-18

### Fixed
- 修复游戏运行时 2 秒轮询偶发模块枚举失败造成 Trainer 重复 Attach / Detach、修改器列表周期性跳动的问题。
- 同一 PID 已确认的 Gathering Storm GameCore 对短暂探测失败进行有限去抖；真实退出或持续缺失仍正常断开。

## [0.3.3] - 2026-08-18

### Fixed
- 继续修复主修改器与紧凑修改器中原生 `ToggleSwitch` 默认模板横向溢出、压到快捷键区域的问题。
- 紧凑修改器改为两层行布局，将功能名/状态与快捷键/操作控件分离，避免高 DPI 下横向竞争。
- 紧凑窗口逻辑宽度提升到 580 DIP，并继续根据 `RasterizationScale` 换算物理像素。
- 金钱与影响力 NumberBox 使用独立操作区域，不再与开关或快捷键共享占位。

### Changed
- 为 Trainer 使用的原生 ToggleSwitch 设置明确的 52 DIP 布局占位，同时保留 WinUI 视觉模板和交互状态。
- 保持 v0.3.2 的 Trainer 自动重试、实时语言切换、精确 Profile 和 fail-closed 写入保护。

## [0.3.2] - 2026-08-18

### Fixed
- 修复对局初始化期间 `Player::Manager` 暂时为空后，22 项修改器全部永久显示“运行错误”的连接竞态；GameCore 已加载但 Trainer 未就绪时自动重试精确 Profile。
- 修复主修改器在高 DPI / 较窄内容宽度下 NumberBox、ToggleSwitch、快捷键和状态文本互相遮挡的问题。
- 修复紧凑修改器把 `AppWindow.Resize` 的物理像素误当作 XAML 有效像素，导致 150% / 200% 缩放下窗口实际宽度过小的问题。
- 修复语言切换提示显示 `!Settings_RestartInfo...!` 原始占位符的问题。

### Changed
- 修改器分类改为顶部横向分类栏，功能列表使用完整内容宽度。
- 语言切换改为运行时立即更新 MRT Core、Culture、ResourceLoader 与可见界面，无需重启应用。
- 主窗口和紧凑修改器的静态界面文字均支持运行时重新本地化。
- 继续保留 Steam / DX12 / Gathering Storm 1023995 精确 SHA、13 AoB 与指针链 fail-closed 写入保护。

## [0.3.1] - 2026-08-18

### Added
- 将已实机验证的 Steam / DX12 / Gathering Storm 1.0.12.68 (1023995) 精确 Trainer Profile 与稳定性调度器接入新版 UI。
- 恢复全部 22 项修改器、全局快捷键、金钱 / 影响力自定义输入，以及置顶紧凑修改器窗口的真实操作能力。
- 响应式页面重排：最大化窗口充分利用可用宽度，中窄窗口自动重排首页、诊断和设置内容。

### Changed
- 主壳层改用与 UrbanPlanToolbox 一致的原生 WinUI 3 `TitleBar`、`NavigationView`、Mica 和系统 ThemeResource，替换固定自绘导航与硬编码浅色表面。
- 首页、预设与诊断、设置与关于移除页面级固定 `MaxWidth`，卡片随窗口宽度伸展。
- 紧凑修改器改用原生 WinUI 材质，并与主窗口共享 Trainer 状态和操作。
- 保持 GameCore SHA + 13 AoB 精确校验和 fail-closed 写入保护；不匹配构建不会执行内存写入。

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