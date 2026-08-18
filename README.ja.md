# CivVIToolkit

[简体中文](README.md) · [English](README.en.md)

CivVIToolkit は Civilization VI 向けのモジュール型 Windows ツールボックスです。現在はシングルプレイヤー Trainer を中心に開発し、セーブ管理、マップ／ゲーム情報、Mod 管理、クイック起動を独立モジュールとして拡張できる構成にしています。

## 現在の状態

- WinUI 3 / .NET 10 / x64 / MSIX
- Steam / Epic Games のインストール検出
- DX11 / DX12 の実行時検出
- 简体中文 / 日本語 / English の三言語基盤
- Steam + DX12 + Gathering Storm `1.0.12.68 (1023995)` の精確 GameCore Profile
- 22 項目のシングルプレイヤー Trainer を実機受け入れ確認中
- v0.2.2 では、実行中に GameContext ルートポインターが一時的に null になった際に Trainer 全体が失敗する問題を修正
- Signed MSIX Acceptance と GitHub Release ワークフロー

## 実機確認済みの基盤

- `GameCore_XP2_FinalRelease.dll` の SHA-256 と 13 個の AoB アンカー
- Local Player / Treasury / Religion / Influence の読み取り経路
- Gold / Faith の値がゲーム UI と一致することを確認
- PageUp：Gold +10,000 をシングルプレイヤーで実機確認済み

書き込みは精確に検証済みの Build のみで有効です。未知の GameCore fingerprint やランタイムアンカー不一致では fail-closed になります。

## モジュール

- 概要 / ゲーム検出
- シングルプレイヤー Trainer
- セーブ管理（予定）
- マップ／ゲーム情報（予定）
- Mod 管理（予定）
- クイック起動（予定）
- 設定

## 開発とリリース

Windows CI、三言語リソース整合性チェック、署名済み MSIX 受け入れパッケージ、ワンクリックインストール、`release/release.json` による中央リリースメタデータ、Release Orchestrator を備えています。

Trainer の実機検証中のため、現在は Preview です。

## 対象範囲

CivVIToolkit はシングルプレイヤー向けのローカルツールです。マルチプレイヤーでのチートやアンチチート回避は対象にしません。

## License

MIT
