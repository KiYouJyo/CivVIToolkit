简体中文 | [日本語](README.ja.md) | [English](README.en.md)

# CivVIToolkit

面向 **Sid Meier's Civilization VI** 的现代 Windows 工具箱。项目以 WinUI 3 为界面基础，首阶段聚焦本地单机修改器，同时为存档管理、地图/游戏信息、Mod 管理和快捷启动预留独立模块。

[![GitHub Release](https://img.shields.io/github/v/release/KiYouJyo/CivVIToolkit?display_name=tag&sort=semver&color=2F81F7&label=Release)](https://github.com/KiYouJyo/CivVIToolkit/releases) [![CI](https://github.com/KiYouJyo/CivVIToolkit/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/CivVIToolkit/actions/workflows/ci.yml) [![Closed PRs](https://img.shields.io/github/issues-pr-closed/KiYouJyo/CivVIToolkit?color=8250DF&label=Closed%20PRs)](https://github.com/KiYouJyo/CivVIToolkit/pulls?q=is%3Apr+is%3Aclosed) [![Last Commit](https://img.shields.io/github/last-commit/KiYouJyo/CivVIToolkit?color=57606A&label=Last%20Commit)](https://github.com/KiYouJyo/CivVIToolkit/commits/main/)

[![Windows](https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows&logoColor=white)](https://github.com/KiYouJyo/CivVIToolkit) [![Architecture](https://img.shields.io/badge/Architecture-x64-005A9E)](#系统要求) [![Languages](https://img.shields.io/badge/Languages-%E4%B8%AD%E6%96%87%20%7C%20%E6%97%A5%E6%9C%AC%E8%AA%9E%20%7C%20English-6F42C1)](#语言) [![Local First](https://img.shields.io/badge/Local-Single--Player-2EA043)](#使用范围) [![MIT License](https://img.shields.io/badge/License-MIT-D4A72C)](LICENSE)

## 当前能力

- 自动识别 **Steam / Epic Games** 安装来源，不要求手动选择平台。
- 自动识别正在运行的 **DX11 / DX12** 版本。
- 监测 Civilization VI 进程，读取 PID、可执行文件路径和版本信息。
- 提供进程内存读写、AoB 特征码解析与扫描基础。
- 已建立经典 **22 项单机修改功能**的数据驱动框架与快捷键契约。
- 已针对 Steam / DX12 / Gathering Storm `1.0.12.68 (1023995)` 建立首个精确 GameCore Profile。
- v0.1.3 开始开放首个真实写入功能：`PageUp` / 应用内按钮 **增加金钱 +10,000**。
- 每次写入前都会重新验证 GameCore SHA-256、13 个 AoB anchor、本地玩家与 Treasury 路径；其余 21 项仍保持 `Signature pending`。
- 提供只读运行时诊断，便于建立 Steam / Epic、DX11 / DX12 的版本化 Signature Profile。
- 已预留 Save Manager、Maps & Game Info、Mod Manager、Quick Launch 与 Settings 模块。

## 自动识别逻辑

- Steam：读取 Steam 注册表根目录、Library metadata 与 App ID `289070` 的 manifest。
- Epic Games：读取 Epic Games Launcher 的本地 manifests。
- DX11：识别 `CivilizationVI.exe`。
- DX12：识别 `CivilizationVI_DX12.exe`。

游戏已经运行但启动器元数据不可用时，程序会使用实际进程路径作为回退判断依据。

## Trainer 状态

当前低层内存访问、AoB 扫描、快捷键与 22 项功能目录已经建立。已验证 Build 上的单项功能会逐步开放；任何未完成 Profile、AoB、对象链或写入语义验证的功能仍保持不可用，不会使用猜测地址。

兼容策略见 [Trainer Signature 文档](docs/TRAINER-SIGNATURES.md)，运行时 Build Fingerprint 见 [诊断文档](docs/RUNTIME-DIAGNOSTICS.md)。

## Signed Acceptance 验收包

仓库提供自动签名的 x64 Preview 验收工作流。仓库内同源 PR 会自动执行，维护者也可以在 Actions 中手动运行 **Signed MSIX Acceptance**。工作流会完成 Restore、测试、Release 构建、MSIX 签名、签名验证、SHA-256 清单生成和 Artifact 上传。

验收包用于真实 Civilization VI 环境测试，不等同于正式 Release。详细流程见 [发布指南](docs/RELEASE.md)。

## 规划模块

- **Save Manager**：存档发现、备份、标签、元数据与恢复。
- **Maps & Game Info**：地图、规则集和当前游戏信息。
- **Mod Manager**：Mod 发现、验证、依赖关系与启停管理。
- **Quick Launch**：按检测到的平台与渲染器快速启动游戏。

路线图见 [docs/ROADMAP.md](docs/ROADMAP.md)，架构见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

## 隐私与本地设计

CivVIToolkit 不要求账户，也不包含遥测。安装发现、游戏进程识别、特征码扫描与当前诊断功能均在本机完成。诊断 JSON 只复制到本机剪贴板，不自动上传。公开提交诊断信息前请删除本机路径等不希望公开的内容。

详见 [PRIVACY.md](PRIVACY.md)。

## 系统要求

- Windows 10 17763 或更高版本 / Windows 11
- x64
- 开发环境：.NET 10 SDK 与 WinUI / Windows App SDK 工具链

## 语言

仓库与应用本地化基础支持简体中文、日本語和 English。三套资源必须保持相同键集；新增用户可见文案时需要同步维护三种语言。

## 开发与构建

```powershell
dotnet restore CivVIToolkit.sln -p:Platform=x64
dotnet test tests/CivVIToolkit.Tests/CivVIToolkit.Tests.csproj -c Release -p:Platform=x64 --no-restore
dotnet build CivVIToolkit.sln -c Release -p:Platform=x64 --no-restore
```

贡献规范见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 文档

- [路线图与版本规划](docs/ROADMAP.md)
- [架构](docs/ARCHITECTURE.md)
- [Trainer 特征码策略](docs/TRAINER-SIGNATURES.md)
- [运行时诊断](docs/RUNTIME-DIAGNOSTICS.md)
- [本地化](docs/LOCALIZATION.md)
- [发布流程](docs/RELEASE.md)

## 使用范围

Trainer 功能仅面向 **Civilization VI 本地单机模式**。本项目不面向多人对战作弊，也不会为绕过联机反作弊机制提供支持。

## License

MIT
