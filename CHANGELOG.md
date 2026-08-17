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

## [0.2.0] - 2026-08-17

### Added
- 针对已验证的 Steam / DX12 / Gathering Storm `1.0.12.68 (1023995)` 精确 Profile，将经典 22 项单机修改器全部接入可操作状态。
- 22 个全局快捷键与可点击修改器功能行；Toggle 功能显示实时启用状态，ValueAction 直接执行。
- 临时 x64 GameCore 调用桥：按 Windows x64 ABI 在目标进程内调用已验证函数，调用后立即释放，不注入常驻 DLL。
- 可逆代码 Patch 基础：写入前验证原字节，修改页保护并刷新指令缓存，关闭功能 / Detach / 退出时恢复原字节。
- 本地玩家资源、研究、市政、单位、城市、生产、资源库存等完整运行时路径。
- AI 玩家枚举以及 AI 金钱 / 信仰 / 影响力 / 研究 / 市政 / 移动力 / 生产 / 资源控制。

### Trainer
- Num 1：无限金钱。
- Num 2：无限信仰。
- Num 3：一回合完成研究。
- Num 4：一回合完成市政。
- Num 5：增加影响力 +1000。
- Num 6：单位无限移动力。
- Num 7：单位无限生命。
- Num 8：一回合建造 / 招募。
- Num 9：全部奢侈 / 战略资源。
- Num 0：单位随时可升级。
- Num .：城市最大人口。
- PageUp：增加金钱 +10000（v0.1.3 已实机验证）。
- PageDown：建造者无限使用次数。
- Alt+Num 1–9：AI 对应限制与一击必杀功能。

### Acceptance
- v0.2.0 为“全 22 项实机验收构建”，不是宣称 22 项全部已经实机验证完成。
- `PageUp +10000 Gold` 已完成真实游戏写入验收；Gold / Faith / Influence 读取链与 13 个运行时 AoB anchor 已完成实机验证。
- `unit.always-upgrade`、`combat.one-hit-kill`、`ai.block-production` 当前在 UI 中标记为“实验性验收”，需要重点验证其游戏语义与边界条件。
- 所有功能仍严格锁定精确 GameCore SHA-256；任何 Build / signature / 原字节不匹配均 fail closed。

### Safety
- 仅用于 Civilization VI 本地单机模式。
- 不实现多人联机作弊、反作弊规避或持久注入。
- 代码 Patch 在正常关闭、Detach 时尝试恢复原始字节。

## [0.1.3] - 2026-08-17

### Added
- 首个真实写入功能：`PageUp` / 应用内按钮“增加金钱 +10,000”。
- 每次写入前重新执行完整 Steam / DX12 / Gathering Storm build 1023995 Profile 验证。
- Gold 写入采用游戏原生 1/256 定点格式，并在写入后立即回读确认。

### Safety
- 只有 `player.add-gold` 在精确 GameCore SHA-256 与 13 个 AoB anchor 全部验证通过时可用。
- 其余 21 项 Trainer 功能继续保持 Signature Pending。
- 本阶段仍限定 Civilization VI 本地单机模式。

## [0.1.2] - 2026-08-17

### Fixed
- 修正第二轮真实对局验收发现的 live Player 资源对象链错误。
- 明确区分 `Player::Instance` 与 `Player::Cache::Instance`：`Player::Manager` 返回 live Player，不能套用 Cache 的 `+0xB0` component block。
- 根据 GameCore 自身 bridge 代码，live Player 改为直接读取 Religion `+0x720`、Influence `+0x748`、Treasury `+0x780` 指针。

### Added
- live Faith / Gold / Influence bridge 的运行时 AoB 验证。
- Probe 输出 `ResourcePath` 与实际 Treasury / Religion / Influence 组件地址。

### Safety
- v0.1.2 仍只执行读取与验证；所有 Trainer 写入继续关闭，直到真实 UI 的 Gold / Faith 与 Probe 结果一致。

## [0.1.1] - 2026-08-17

### Added
- Steam / DX12 / Gathering Storm `1.0.12.68 (1023995)` 的首个精确 GameCore Profile。
- GameCore SHA-256 gating 与运行时 AoB 验证。
- 本地玩家 Gold / Faith / Influence 只读 Probe。
- Game Context current-game、Player Manager、Treasury、Religion、Influence、Gold/Faith setter 等 10 个运行时签名锚点。
- Probe 原始定点数值和 Manager / Player / Component 地址诊断。

### Fixed
- 修正首轮真实对局验收发现的 Player Manager singleton 错误：由错误的 `GameCore+0xB8BEE0` 改为实际 `GameCore+0xB8E140`。
- 修正 active-player slot table 偏移：由错误的 `+0x1460` 改为实际 `+0x2B48`。
- 显式验证 Game Context 的 current-game getter 等价于读取 `[context+0x08]`，避免把未经验证的结构假设继续传递到写入阶段。

### Safety
- v0.1.1 仍只执行读取与验证；所有 Trainer 写入继续保持关闭，直到真实对局资源数值与 UI 完全吻合。

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
