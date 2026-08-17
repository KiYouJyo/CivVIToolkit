# CivVIToolkit Roadmap

本路线图描述模块边界和推荐推进顺序，不承诺固定发布日期。版本号可根据实际工程量调整。

## v0.1.x — Foundation / Trainer Core

目标：把“可识别、可验证、可安全失败”做扎实。

- Steam / Epic Games 自动识别。
- DX11 / DX12 自动识别。
- 运行进程监测与 Build Fingerprint。
- 内存访问、AoB Scanner、Hotkey、Patch 生命周期。
- 22 项 Trainer 的版本化 Signature Profile。
- Signed Acceptance 自动验收包。
- 三语基础、仓库治理与文档契约。

完成标准：未知 Build 不误写内存；已支持 Build 能明确显示兼容状态；签名验收包可以在真实机器上复现。

## v0.2.x — Save Manager

- 自动定位常见 Civilization VI 存档目录。
- 存档列表、时间、规则集/玩家等可安全解析的元数据。
- 本地备份、恢复、标签和备注。
- 明确的备份格式与数据迁移策略。

## v0.3.x — Maps & Game Info

- 当前安装的规则集、DLC 与可公开解析的游戏信息。
- 地图/局信息查看。
- 与存档和启动模块共享统一 GameInstallation / GameSession 模型。

## v0.4.x — Mod Manager

- 本地 Mod 发现与清单。
- 启用/停用、基础依赖检查与冲突提示。
- 不替代 Steam Workshop / Epic 平台本身的许可和分发规则。

## v0.5.x — Quick Launch

- 按 Steam / Epic 自动选择启动方式。
- DX11 / DX12 快捷启动。
- 可保存本地启动偏好，不保存平台账号凭据。

## 长期原则

- WinUI 3 / x64。
- Local-first；新增联网行为必须显式说明。
- Trainer 仅限本地单机。
- 核心模块之间通过稳定模型和服务边界协作，避免把所有功能耦合到内存修改层。
- 简体中文、日本語、English 三语键集保持一致。
