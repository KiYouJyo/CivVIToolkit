# 隐私政策

最后更新：2026-08-17

CivVIToolkit 是面向 Civilization VI 的本地优先 Windows 工具箱。当前版本不要求账户、不包含广告 SDK、不包含遥测，也不会自动上传游戏数据或用户文件。

## 当前会读取什么

为了自动识别游戏与运行状态，应用可能在本机读取：

- Steam 安装目录、Library metadata 与 Civilization VI app manifest；
- Epic Games Launcher 的本地安装 manifest；
- 正在运行的 Civilization VI 进程、PID、主模块路径与文件版本；
- 在用户使用 Trainer/Signature 验证相关能力时读取目标游戏进程中必要的内存区域。

这些读取用于本地功能，不会自动发送到项目维护者或第三方服务器。

## Runtime Diagnostics

用户主动点击 **Copy diagnostics** 时，应用会生成只读诊断信息，包括 Store、DX 后端、PID、可执行文件路径、文件版本、SHA-256、主模块基址和映像大小等，并复制到本机剪贴板。

应用不会自动上传该 JSON。用户自行粘贴到 Issue、聊天或其他服务时，数据将受目标服务的隐私规则约束。诊断中包含本机文件路径，公开分享前可以先进行脱敏。

## Trainer 与进程内存

Trainer 的目的仅是本地单机使用。内存访问限定于被识别的 Civilization VI 游戏进程与已经实现的功能需求。未知游戏 Build 默认保持不可用，不以大范围无关内存收集替代 Signature 验证。

## 存档、Mod 与未来模块

Save Manager、Mod Manager 等模块仍在规划或开发阶段。未来若增加存档索引、备份、云端能力或网络请求，必须在功能进入正式版本前同步更新本政策并明确数据边界。

## 网络访问

当前应用核心检测和诊断流程不依赖网络。GitHub Actions 的构建、依赖恢复和签名时间戳发生在项目 CI 环境中，不属于已安装应用上传用户数据。

## 本地设置

语言等应用设置保存在当前 Windows 用户的本地应用数据目录中，不设计跨设备同步。

## 联系与反馈

如对隐私边界有疑问，请通过仓库 Issue 提出，并避免在公开 Issue 中粘贴不必要的私人信息。
