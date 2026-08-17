[简体中文](README.md) | 日本語 | [English](README.en.md)

# CivVIToolkit

**Sid Meier's Civilization VI** 向けのモダンな Windows ツールキットです。WinUI 3 を基盤とし、最初の段階ではローカルのシングルプレイヤー用 Trainer を中心に実装しつつ、セーブ管理、マップ／ゲーム情報、Mod 管理、クイック起動を独立モジュールとして拡張できる構成にしています。

[![GitHub Release](https://img.shields.io/github/v/release/KiYouJyo/CivVIToolkit?display_name=tag&sort=semver&color=2F81F7&label=Release)](https://github.com/KiYouJyo/CivVIToolkit/releases) [![CI](https://github.com/KiYouJyo/CivVIToolkit/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/CivVIToolkit/actions/workflows/ci.yml) [![Closed PRs](https://img.shields.io/github/issues-pr-closed/KiYouJyo/CivVIToolkit?color=8250DF&label=Closed%20PRs)](https://github.com/KiYouJyo/CivVIToolkit/pulls?q=is%3Apr+is%3Aclosed) [![Last Commit](https://img.shields.io/github/last-commit/KiYouJyo/CivVIToolkit?color=57606A&label=Last%20Commit)](https://github.com/KiYouJyo/CivVIToolkit/commits/main/)

[![Windows](https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows&logoColor=white)](https://github.com/KiYouJyo/CivVIToolkit) [![Architecture](https://img.shields.io/badge/Architecture-x64-005A9E)](#動作環境) [![Languages](https://img.shields.io/badge/Languages-%E4%B8%AD%E6%96%87%20%7C%20%E6%97%A5%E6%9C%AC%E8%AA%9E%20%7C%20English-6F42C1)](#言語) [![Local First](https://img.shields.io/badge/Local-Single--Player-2EA043)](#利用範囲) [![MIT License](https://img.shields.io/badge/License-MIT-D4A72C)](LICENSE)

## 現在の機能

- **Steam / Epic Games** のインストール元を自動検出。
- 実行中のゲームから **DX11 / DX12** を自動判別。
- Civilization VI の PID、実行ファイルパス、ファイルバージョンを監視。
- プロセスメモリアクセスと AoB シグネチャスキャンの基盤。
- クラシックな **22 項目 Trainer** のデータ駆動カタログとショートカット契約。
- 未検証 Build は `Signature pending` のままにし、推測した固定アドレスを使用しません。
- Steam/Epic・DX11/DX12 ごとのバージョン化 Signature Profile 作成用の読み取り専用診断。
- Save Manager、Maps & Game Info、Mod Manager、Quick Launch、Settings の拡張枠。

## 自動検出

- Steam：レジストリ、ライブラリ情報、App ID `289070` の manifest。
- Epic Games：Epic Games Launcher のローカル manifest。
- DX11：`CivilizationVI.exe`。
- DX12：`CivilizationVI_DX12.exe`。

ランチャー情報が取得できない場合は、実際に動作している実行ファイルのパスをフォールバックとして利用します。

## Trainer の状態

メモリアクセス、AoB スキャナ、ショートカット契約、22 項目のカタログは実装済みです。ゲーム状態を変更する機能は、検出した Build に対してシグネチャと Patch の意味が検証された場合のみ有効化します。

詳細は [Trainer Signatures](docs/TRAINER-SIGNATURES.md) と [Runtime Diagnostics](docs/RUNTIME-DIAGNOSTICS.md) を参照してください。

## Signed Acceptance ビルド

リポジトリには自動署名 x64 Preview 検収ワークフローがあります。同一リポジトリ内の PR では **Signed Acceptance** が自動実行され、メンテナは Actions から手動実行することもできます。Restore、テスト、Release ビルド、自己完結型 x64 publish、署名と検証、SHA-256 作成、Artifact アップロードまで自動化します。

検収ビルドは実ゲーム環境での確認用で、正式 Release ではありません。詳細は [docs/RELEASE.md](docs/RELEASE.md) を参照してください。

## 予定モジュール

- **Save Manager**：セーブの検出、バックアップ、タグ、メタデータ、復元。
- **Maps & Game Info**：マップ、ルールセット、現在のセッション情報。
- **Mod Manager**：Mod の検出、検証、依存関係、オン／オフ管理。
- **Quick Launch**：検出したストア版とレンダラーでゲームを起動。

[Roadmap](docs/ROADMAP.md) と [Architecture](docs/ARCHITECTURE.md) を参照してください。

## プライバシーとローカル設計

CivVIToolkit はアカウントを必要とせず、テレメトリも含みません。インストール検出、プロセス検出、シグネチャスキャン、現在の診断はローカルで完結します。診断 JSON はローカルのクリップボードにのみコピーされ、自動アップロードされません。公開 Issue に貼る場合は必要に応じてローカルパスを削除してください。

詳細は [PRIVACY.md](PRIVACY.md) を参照してください。

## 動作環境

- Windows 10 17763 以降 / Windows 11
- x64
- 開発環境：.NET 10 SDK、WinUI / Windows App SDK ツール

## 言語

リポジトリとアプリのローカライズ基盤は简体中文、日本語、English に対応します。3 言語のリソースキーは常に同一セットを維持します。

## 開発

```powershell
dotnet restore CivVIToolkit.sln -p:Platform=x64
dotnet test tests/CivVIToolkit.Tests/CivVIToolkit.Tests.csproj -c Release -p:Platform=x64 --no-restore
dotnet build CivVIToolkit.sln -c Release -p:Platform=x64 --no-restore
```

[CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

## ドキュメント

- [Roadmap](docs/ROADMAP.md)
- [Release & Signed Acceptance](docs/RELEASE.md)
- [Documentation governance](docs/DOCUMENTATION.md)
- [Localization](docs/LOCALIZATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Trainer Signatures](docs/TRAINER-SIGNATURES.md)
- [Runtime Diagnostics](docs/RUNTIME-DIAGNOSTICS.md)
- [Changelog](CHANGELOG.md)
- [Support](SUPPORT.md) · [Privacy](PRIVACY.md) · [Third-party notices](THIRD-PARTY-NOTICES.md)

## 利用範囲

Trainer はローカルのシングルプレイヤー専用です。マルチプレイでの不正行為、アンチチート回避、オンラインの公平性を損なう機能は対象外です。

## License と商標

コードは [MIT License](LICENSE) で公開しています。Sid Meier's Civilization、Civilization VI および関連商標は各権利者に帰属します。CivVIToolkit は独立したコミュニティプロジェクトであり、Firaxis Games、2K、Take-Two Interactive とは提携・承認関係にありません。
