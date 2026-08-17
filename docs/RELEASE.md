# 发布、MSIX 与一键 Release

CivVIToolkit 将开发验收和正式 GitHub Release 分开，但两条链路共用同一套 **x64 MSIX 构建、证书签名、签名身份验证和 one-click 安装包**逻辑。

## 为什么改用 MSIX

早期 Signed Acceptance 采用 self-contained、解压后直接运行的 WinUI 3 目录。该形式仍可能受到 Windows App SDK bootstrap、运行时注册和文件部署状态影响。自 v0.1.0 Preview 起，真实机器验收统一改用 MSIX：

- Windows 负责注册应用身份和 WinUI 3 包环境；
- Manifest 明确声明 `runFullTrust`，保留本地单机 Trainer 所需的桌面进程访问能力；
- 包内固定声明 `zh-CN` / `ja-JP` / `en-US`；
- MSIX 与仓库发布证书的 Publisher 必须一致；
- Actions 会在上传前验证签名者 Subject / Thumbprint 和包内 Manifest。

当前只发布 Windows x64。未来增加 ARM64 时，可把直接 `.msix` 提升为 `.msixbundle`，而不需要改变 release 元数据契约。

## Signed MSIX Acceptance

`.github/workflows/signed-acceptance.yml` 用于 PR 和手工真实机器验收。它会：

1. 读取并校验 `release/release.json`；
2. 校验三语资源一致性；
3. Restore 和运行测试；
4. 生成 self-contained Windows App SDK x64 MSIX；
5. 从 GitHub Actions Secrets 临时导入发布证书；
6. 验证 Publisher、证书 Subject 和 Thumbprint；
7. 使用 SHA-256 + RFC 3161 timestamp 对 MSIX 签名；
8. 读取 MSIX 的 Authenticode 签名并核对嵌入的签名者 Subject / Thumbprint；由于仓库证书是自签名证书，干净 Runner 上允许预期的 `UnknownError / untrusted root`，不会为了 CI 修改系统根证书库；
9. 导出公开 `.cer`；
10. 生成 one-click 安装 ZIP；
11. 解包 MSIX 并验证版本、Identity、三语 Resources、`runFullTrust` 和安装脚本契约；
12. 生成 `SHA256SUMS.txt` 并上传 14 天 Artifact。

### Actions Secrets

与 UrbanPlanToolbox 保持相同命名：

- `GH_RELEASE_CERTIFICATE_BASE64`
- `GH_RELEASE_CERTIFICATE_PASSWORD`

PFX、私钥和密码都不得进入 Git 或 Release。公开 `.cer` 可以随安装包分发。

### 验收产物

Artifact 包含：

- `CivVIToolkit_X.Y.Z.0_x64.msix`
- `CivVIToolkit-vX.Y.Z-x64-one-click.zip`
- `CivVIToolkit-X.Y.Z.cer`
- `SHA256SUMS.txt`

one-click ZIP 内的 `Install-CivVIToolkit.cmd` 会调用 PowerShell。首次安装若发布证书尚未受信任，会通过 UAC 提升一个只负责证书导入的辅助步骤，把**公开证书**加入 `LocalMachine\TrustedPeople`；随后回到普通安装流程，验证 MSIX 签名、执行 `Add-AppxPackage`，并核对已安装包的版本、x64 架构和 `Ok` 状态。安装包从不包含 PFX 或私钥。

这与 UrbanPlanToolbox 当前的侧载证书边界保持一致：自签名 MSIX 的信任建立在本机 `TrustedPeople`，而不是依赖 `CurrentUser\TrustedPeople`。

## release/release.json

正式发布状态集中在 `release/release.json`。核心字段：

- `product.version`：三段应用版本，如 `0.1.0`；
- `product.packageVersion`：MSIX 四段版本，必须为 `0.1.0.0`；
- `product.status`：例如 `preview`；
- `channels.github.publish`：是否允许由 metadata push 自动进入发布编排；
- `notes`：中 / 日 / 英三语 Release Notes 文件。

正式发布前，`release.json`、`.csproj` 和 `Package.appxmanifest` 三处版本必须完全一致。

## 一键 GitHub Release

`.github/workflows/release-orchestrator.yml` 是维护者入口。

手动运行时：

1. 输入版本号，例如 `0.1.0`；
2. 输入精确确认文本 `PUBLISH 0.1.0`；
3. Orchestrator 验证 release metadata 与源代码版本；
4. 在当前 `main` HEAD 创建不可变 annotated tag `v0.1.0`；
5. 如果同名 tag 已存在，只允许它指向同一个 commit；
6. 调度 `.github/workflows/publish-github-release.yml`。

`publish-github-release.yml` 会重新执行测试和 MSIX 构建，而不是复用未经验证的本地文件，然后：

- 生成并签名 x64 MSIX；
- 生成 one-click ZIP 和公开证书；
- 生成三语 GitHub Release 正文；
- 生成 SHA-256；
- 创建或 reconcile GitHub Release；
- 已存在的同名 Asset 必须 SHA-256 完全一致，否则发布失败，不会静默覆盖不同二进制。

也可以直接手动运行 `Publish GitHub Release` 并选择 `dry_run=true`，只验证正式发布链并上传 7 天 Artifact，不创建 Release。

## Preview 原则

当前 v0.1.0 仍是 Preview。MSIX / Release 链可先稳定下来，Trainer 的真实功能仍必须等待对应 Civilization VI Build 的 Signature Profile 验证。未经验证的内存特征码不会因为正式打包而自动启用。

Trainer 仅面向 Civilization VI 本地单机模式，不实现多人作弊或反作弊绕过。
