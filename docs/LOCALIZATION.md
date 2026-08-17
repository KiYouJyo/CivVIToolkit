# 本地化规范

CivVIToolkit 的基础语言为简体中文，并同时维护日本語和 English。

## 资源目录

应用 MRT Core 资源位于：

- `src/CivVIToolkit.App/Strings/zh-CN/Resources.resw`
- `src/CivVIToolkit.App/Strings/ja-JP/Resources.resw`
- `src/CivVIToolkit.App/Strings/en-US/Resources.resw`

## 语言策略

支持的 BCP-47 标签：

- `zh-CN`
- `ja-JP`
- `en-US`

设置值 `system` 表示跟随 Windows 用户语言；不受支持的系统语言当前回退到 `zh-CN`。

## 文案规则

- 品牌名 `CivVIToolkit` / `Civ VI Toolkit` 不强制翻译。
- Steam、Epic Games、DX11、DX12、AoB、Signature 等技术名词保持一致。
- 用户可见的状态、按钮、导航、说明和 Trainer 功能名必须使用资源键。
- 三套 `Resources.resw` 必须保持完全一致的键集。
- 格式化字符串的占位符数量和顺序必须一致。

## 验证

```powershell
pwsh ./scripts/Test-LocalizationConsistency.ps1
```

普通 CI 和 Signed Acceptance 都应执行该检查。
