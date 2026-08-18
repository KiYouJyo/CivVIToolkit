# CivVIToolkit

[简体中文](README.md) · [日本語](README.ja.md)

CivVIToolkit is a modular Windows companion for Civilization VI. The current focus is a single-player trainer, with separate foundations for save management, maps/game information, Mod management and quick launch workflows.

## Current status

- WinUI 3 / .NET 10 / x64 / MSIX
- Steam / Epic Games installation detection
- DX11 / DX12 runtime detection
- Simplified Chinese / Japanese / English foundation
- Exact Steam + DX12 + Gathering Storm `1.0.12.68 (1023995)` GameCore profile
- 22 single-player trainer features in live acceptance
- v0.2.2 fixes whole-page trainer failure when the live game temporarily clears the GameContext root pointer
- Signed MSIX Acceptance and GitHub Release workflows

## Live-verified foundation

- `GameCore_XP2_FinalRelease.dll` SHA-256 and 13 runtime AoB anchors
- Local Player / Treasury / Religion / Influence path
- Gold / Faith readback verified against the real game UI
- PageUp: Add Gold +10,000 verified in a live single-player match

Writes are enabled only for the exact verified build. Unknown GameCore fingerprints or runtime anchor mismatches fail closed.

## Modules

- Overview / game detection
- Single-player Trainer
- Save Manager (planned)
- Maps & Game Info (planned)
- Mod Manager (planned)
- Quick Launch (planned)
- Settings

## Development and release

The repository includes Windows CI, localization consistency checks, signed MSIX acceptance artifacts, one-click installation packaging, centralized `release/release.json` metadata and release orchestration workflows.

The project remains Preview while live trainer acceptance is in progress.

## Scope

CivVIToolkit is intended for single-player local tooling. It does not target multiplayer cheating or anti-cheat bypasses.

## License

MIT
