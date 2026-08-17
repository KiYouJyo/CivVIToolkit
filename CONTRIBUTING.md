# 贡献指南

欢迎提交可复现的问题、文档改进和经过充分说明的代码变更。较大的功能提案建议先通过 Issue 说明目标、边界和验证方式，再提交 PR。

## 支持范围

优先处理可复现缺陷、游戏版本兼容性、安装/检测问题、隐私与本地数据边界、现有功能文档修正，以及与当前路线图一致的改进。Trainer 相关变更必须说明适用的 Store、DX 后端、游戏 Build 与 Signature 验证依据。

多人作弊、反作弊绕过、隐藏恶意行为或破坏在线公平性的功能不接受贡献。

## Issue 与 PR

Bug Issue 建议包含：

- CivVIToolkit 版本或 Commit；
- Windows 版本；
- Civilization VI 来源（Steam / Epic Games）；
- DX11 / DX12；
- 游戏文件版本或经过脱敏的 Runtime Diagnostics；
- 复现步骤、预期结果和实际结果。

PR 应说明范围、测试方式、是否修改三语文案、是否影响签名配置/本地数据，以及是否需要真实游戏验收。

## 分支与提交

从 `main` 创建主题分支，使用简洁的 Conventional Commits 风格，例如 `feat: ...`、`fix: ...`、`docs: ...`、`test: ...`。不要直接把开发提交推到 `main`。

## 构建和测试

```powershell
dotnet restore CivVIToolkit.sln -p:Platform=x64
dotnet test tests/CivVIToolkit.Tests/CivVIToolkit.Tests.csproj -c Release -p:Platform=x64 --no-restore
dotnet build CivVIToolkit.sln -c Release -p:Platform=x64 --no-restore
pwsh ./scripts/Test-LocalizationConsistency.ps1
```

提交前还应执行 `git diff --check`。

## 本地化

应用资源位于：

- `src/CivVIToolkit.App/Strings/zh-CN`
- `src/CivVIToolkit.App/Strings/ja-JP`
- `src/CivVIToolkit.App/Strings/en-US`

三套 `Resources.resw` 必须保持完全一致的键集。新增用户可见文案时不得只修改一种语言。

## Signature 与内存修改

- 不提交未经真实游戏 Build 验证的固定地址。
- 优先使用可版本化、可审计的 AoB Signature Profile。
- 默认对未知 Build fail closed，保持功能不可用。
- Patch 必须能在禁用、进程退出或 Detach 时恢复原始状态。
- 诊断收集不得扩大为无关的任意内存转储。

## 证书、数据与机密

不要提交 PFX、私钥、Token、Actions Secret、用户存档、本机路径、内存转储、MSIX/ZIP 验收产物或其他构建输出。签名证书只通过 GitHub Actions Secrets 注入。
