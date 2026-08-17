# CivVIToolkit architecture

CivVIToolkit starts as a Civilization VI single-player trainer, but the repository is intentionally shaped as a toolkit rather than a one-off memory editor.

## Project boundaries

- `CivVIToolkit.Core` contains store-independent models, module contracts, trainer definitions and shortcut contracts. It must not depend on WinUI or Win32.
- `CivVIToolkit.Platform.Windows` owns Windows-specific discovery, process inspection, process memory access and AoB scanning.
- `CivVIToolkit.App` is the WinUI 3 shell. Navigation exposes the long-term module boundaries from day one.
- `CivVIToolkit.Tests` protects catalog, shortcut and memory-pattern contracts.

## Detection pipeline

1. Steam discovery reads Valve registry roots, then `steamapps/libraryfolders.vdf`, then Civilization VI app manifest `289070`.
2. Epic discovery reads Epic Games Launcher `.item` manifests under ProgramData.
3. Both discovery paths resolve real Civilization VI executables below the install root instead of assuming a single hard-coded folder.
4. Runtime process monitoring watches both `CivilizationVI.exe` and `CivilizationVI_DX12.exe`.
5. Store is taken from a matched installation when possible and falls back to executable-path inference when the game is already running.
6. Renderer is inferred from the actual running executable, so the user does not need a DX11/DX12 setting.

## Module roadmap

### Trainer
Versioned signature profiles, hotkeys, patch lifecycle, safe detach and per-build compatibility diagnostics.

### Save Manager
Local save discovery, backups, metadata, tags, restore points and future cloud-oriented workflows without coupling save operations to trainer memory code.

### Maps & Game Info
Read-only game/session metadata, map and ruleset inspection, and later save-derived map information.

### Mod Manager
Discover official/user mod folders, validate manifests, show dependencies and conflicts, and manage enable/disable state.

### Quick Launch
Use the detected Steam/Epic installation plus the user's renderer preference to launch Civilization VI without maintaining a second manual game-path setting.

## Design rule

Store detection, renderer detection and install-path discovery are shared platform services. New modules consume those services instead of implementing their own Steam/Epic probing.
