# Changelog

All notable changes to CivVIToolkit will be documented in this file.

The format is based on Keep a Changelog and the project follows Semantic Versioning while releases remain in Preview.

## [0.2.2] - 2026-08-18

### Fixed

- Prevent the exact Steam/DX12 1023995 trainer profile from failing all features when Civilization VI temporarily clears the GameContext root pointer during a live single-player match.
- Keep the verified GameContext local-player route as the preferred path, but fall back to the already live-verified Player::Manager slot 0 only after checking that the slot is active and its Treasury, Religion and Influence components are present.
- Preserve the GameCore SHA-256 lock and all 13 runtime AoB validations before any trainer write is allowed.

## [0.2.1] - 2026-08-17

### Fixed

- Isolate continuous trainer enforcement per feature so one runtime exception no longer marks every enabled feature as failed.
- Correct the exact-build unit collection enumeration to the `collection + 0x98 / +0xA0` chunk pointer range used by the GameCore shared unit/city lookup.
- Validate a toggle once before keeping it enabled and allow failed features to be retried.
- Avoid creating the remote GameCore call bridge for direct-memory-only feature paths.

## [0.2.0] - 2026-08-17

### Added

- Expose the complete 22-feature Steam / DX12 / Gathering Storm `1.0.12.68 (1023995)` trainer profile for live acceptance.
- Add continuous enforcement for toggle-style trainer features.
- Add exact-profile GameCore call and patch infrastructure for research, civics, production, resources, upgrades, unit, city, combat and AI acceptance paths.
- Register all classic trainer hotkeys and allow clicking trainer rows to trigger the same actions.

## [0.1.3] - 2026-08-17

### Added

- Add the first live-verified trainer write path: PageUp adds 10,000 Gold on the exact Steam DX12 1023995 GameCore profile.
- Gate all writes behind the verified GameCore SHA-256 and runtime AoB signature set.

## [0.1.2] - 2026-08-17

### Fixed

- Correct the live Player::Instance component path for Treasury, Religion and Influence.
- Add runtime bridge signatures for direct live-player component access.

## [0.1.1] - 2026-08-17

### Added

- Add exact Steam DX12 Gathering Storm GameCore runtime diagnostics and read-only trainer probe support.
- Capture the loaded `GameCore_XP2_FinalRelease.dll` fingerprint and verified signature RVAs.

## [0.1.0] - 2026-08-17

### Added

- Initial WinUI 3 application shell and modular toolkit navigation.
- Steam / Epic Games installation discovery and DX11 / DX12 process detection.
- Core memory access, AoB scanning and hotkey contracts.
- 22-entry classic trainer catalog with safe signature-pending behavior.
- Runtime diagnostics, three-language repository/app foundation, MSIX acceptance packaging and release workflows.
