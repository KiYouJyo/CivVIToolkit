# 文档治理与当前状态

CivVIToolkit 的仓库文本遵循“README 用于入口、docs 用于契约、CHANGELOG 用于历史”的分工。

## 主入口

- `README.md`：简体中文主页。
- `README.ja.md`：日本語主页。
- `README.en.md`：English 主页。

三个 README 描述相同的产品状态，不允许其中一种语言提前宣称未实现功能。

## 稳定契约文档

- `docs/ARCHITECTURE.md`：模块边界与依赖方向。
- `docs/TRAINER-SIGNATURES.md`：Trainer Signature 与兼容策略。
- `docs/RUNTIME-DIAGNOSTICS.md`：只读 Build Fingerprint。
- `docs/LOCALIZATION.md`：三语资源与新增文案规则。
- `docs/RELEASE.md`：验收和发布流程。
- `docs/ROADMAP.md`：阶段规划。

## 仓库治理文本

- `CONTRIBUTING.md`：贡献约束。
- `SUPPORT.md`：问题反馈需要的信息与隐私提醒。
- `PRIVACY.md`：应用数据边界。
- `THIRD-PARTY-NOTICES.md`：第三方依赖和商标声明。
- `CHANGELOG.md`：用户可见变化。

## 更新原则

功能代码合入时，如果改变用户可见行为、数据边界、支持平台、Trainer 兼容状态、网络行为或本地化文案，应在同一个 PR 中更新对应文档。

未完成的功能应使用“planned / planned module / 计划”等明确表达，不在 README 中描述为已可用。
