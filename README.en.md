[简体中文](README.md) | [日本語](README.ja.md) | English

# CivVIToolkit

A modern Windows toolkit for **Sid Meier's Civilization VI**. The WinUI 3 application starts with a local single-player trainer core while keeping save management, map/game information, mod management and quick launch as independent future modules.

[![GitHub Release](https://img.shields.io/github/v/release/KiYouJyo/CivVIToolkit?display_name=tag&sort=semver&color=2F81F7&label=Release)](https://github.com/KiYouJyo/CivVIToolkit/releases) [![CI](https://github.com/KiYouJyo/CivVIToolkit/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/KiYouJyo/CivVIToolkit/actions/workflows/ci.yml) [![Closed PRs](https://img.shields.io/github/issues-pr-closed/KiYouJyo/CivVIToolkit?color=8250DF&label=Closed%20PRs)](https://github.com/KiYouJyo/CivVIToolkit/pulls?q=is%3Apr+is%3Aclosed) [![Last Commit](https://img.shields.io/github/last-commit/KiYouJyo/CivVIToolkit?color=57606A&label=Last%20Commit)](https://github.com/KiYouJyo/CivVIToolkit/commits/main/)

[![Windows](https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows&logoColor=white)](https://github.com/KiYouJyo/CivVIToolkit) [![Architecture](https://img.shields.io/badge/Architecture-x64-005A9E)](#requirements) [![Languages](https://img.shields.io/badge/Languages-%E4%B8%AD%E6%96%87%20%7C%20%E6%97%A5%E6%9C%AC%E8%AA%9E%20%7C%20English-6F42C1)](#languages) [![Local First](https://img.shields.io/badge/Local-Single--Player-2EA043)](#scope) [![MIT License](https://img.shields.io/badge/License-MIT-D4A72C)](LICENSE)

## Current capabilities

- Automatic **Steam / Epic Games** installation discovery.
- Automatic **DX11 / DX12** detection from the running game executable.
- Civilization VI process monitoring with PID, executable path and file version.
- Process-memory access and AoB signature scanning foundations.
- A data-driven catalog and shortcut contract for the classic **22 trainer features**.
- Unverified builds remain `Signature pending`; guessed fixed addresses are not enabled.
- Read-only runtime diagnostics for building versioned Steam/Epic and DX11/DX12 signature profiles.
- Reserved modules for Save Manager, Maps & Game Info, Mod Manager, Quick Launch and Settings.

## Detection

- Steam: Steam registry roots, library metadata and app manifest `289070`.
- Epic Games: Epic Games Launcher local manifests.
- DX11: `CivilizationVI.exe`.
- DX12: `CivilizationVI_DX12.exe`.

The actual running executable path is used as a fallback when launcher metadata is unavailable.

## Trainer status

The memory layer, AoB scanner, hotkey contracts and 22-feature catalog are already in place. A feature that changes game state is enabled only after its signatures and patch semantics are verified against the detected game build.

See [Trainer Signatures](docs/TRAINER-SIGNATURES.md) and [Runtime Diagnostics](docs/RUNTIME-DIAGNOSTICS.md).

## Signed Acceptance builds

The repository includes an automatically signed x64 Preview acceptance workflow. Same-repository pull requests run **Signed Acceptance** automatically, and maintainers can also start it manually from Actions. It restores, tests, builds, publishes a self-contained x64 app, signs and verifies the binaries, generates SHA-256 checksums and uploads an artifact.

Acceptance builds are for real-game validation and are not formal releases. See [docs/RELEASE.md](docs/RELEASE.md).

## Planned modules

- **Save Manager** — discovery, backups, tags, metadata and restore workflows.
- **Maps & Game Info** — map, ruleset and current-session information.
- **Mod Manager** — discovery, validation, dependencies and enable/disable workflows.
- **Quick Launch** — launch the detected storefront build with a selected renderer.

See [Roadmap](docs/ROADMAP.md) and [Architecture](docs/ARCHITECTURE.md).

## Privacy and local design

CivVIToolkit requires no account and contains no telemetry. Installation discovery, process detection, signature scanning and current diagnostics are local. Diagnostic JSON is copied only to the local clipboard and is never uploaded automatically. Redact local paths before posting diagnostics publicly if desired.

See [PRIVACY.md](PRIVACY.md).

## Requirements

- Windows 10 17763 or later / Windows 11
- x64
- Development: .NET 10 SDK and WinUI / Windows App SDK tooling

## Languages

The repository and application localization foundation support Simplified Chinese, Japanese and English. All three resource sets must keep identical keys.

## Development

```powershell
dotnet restore CivVIToolkit.sln -p:Platform=x64
dotnet test tests/CivVIToolkit.Tests/CivVIToolkit.Tests.csproj -c Release -p:Platform=x64 --no-restore
dotnet build CivVIToolkit.sln -c Release -p:Platform=x64 --no-restore
```

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation

- [Roadmap](docs/ROADMAP.md)
- [Release & Signed Acceptance](docs/RELEASE.md)
- [Documentation governance](docs/DOCUMENTATION.md)
- [Localization](docs/LOCALIZATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Trainer Signatures](docs/TRAINER-SIGNATURES.md)
- [Runtime Diagnostics](docs/RUNTIME-DIAGNOSTICS.md)
- [Changelog](CHANGELOG.md)
- [Support](SUPPORT.md) · [Privacy](PRIVACY.md) · [Third-party notices](THIRD-PARTY-NOTICES.md)

## Scope

The trainer is for local single-player use only. Multiplayer cheating, anti-cheat bypasses and features that undermine online fairness are out of scope.

## License and trademarks

Code is licensed under the [MIT License](LICENSE). Sid Meier's Civilization, Civilization VI and related marks belong to their respective owners. CivVIToolkit is an independent community project and is not affiliated with or endorsed by Firaxis Games, 2K or Take-Two Interactive.
