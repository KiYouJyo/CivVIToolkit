# 发布与 Signed Acceptance

CivVIToolkit 将“开发验收包”和“正式 Release”分开管理。

## Signed Acceptance

`.github/workflows/signed-acceptance.yml` 用于真实机器验收。它会：

1. Restore .NET 依赖；
2. 校验三语资源键集；
3. 运行测试；
4. 执行 Release x64 构建；
5. 以 self-contained 方式发布 WinUI 3 x64 应用；
6. 从 GitHub Actions Secrets 导入代码签名证书；
7. 对 CivVIToolkit 自有 EXE/DLL 进行 SHA-256 Authenticode 签名并验证 signer；
8. 导出公开 `.cer`；
9. 生成可直接解压运行的 Preview ZIP、证书安装辅助脚本和 SHA-256 清单；
10. 上传 GitHub Actions Artifact。

### 触发方式

- 同一仓库内、目标为 `main` 的 Pull Request 自动执行；
- 维护者可以通过 `workflow_dispatch` 手动运行。

外部 Fork PR 不接收仓库 Secrets，因此 Signed Acceptance 会跳过；普通 CI 仍可运行。

### Actions Secrets

工作流使用与 UrbanPlanToolbox 相同的 Secret 命名：

- `GH_RELEASE_CERTIFICATE_BASE64`
- `GH_RELEASE_CERTIFICATE_PASSWORD`

PFX 不得提交到 Git。工作流会在 Runner 临时目录中解码、导入、使用并删除证书文件。

### 验收包

Artifact 名称包含应用版本和 `signed-acceptance`。Preview ZIP 内包含 self-contained x64 应用、公开证书、当前用户证书安装脚本以及三语验收说明。

验收包用于：

- Steam / Epic 自动检测确认；
- DX11 / DX12 自动检测确认；
- Runtime Diagnostics 采样；
- 后续 Trainer Signature 的真实 Build 验证。

它不是正式 Release，也不应被 README 描述为稳定版本。

## 正式 Release

正式 Release 至少应满足：

- `main` CI 全绿；
- 对目标真实 Civilization VI Build 完成必要验收；
- CHANGELOG 与版本元数据一致；
- 三语资源一致性检查通过；
- 发布产物签名验证通过；
- 隐私、第三方声明和支持文档与功能一致。

正式 GitHub Release 自动化将在版本进入可公开分发阶段后单独建立，避免把早期验收工作流和稳定发布链混在一起。
