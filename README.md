# CivVIToolkit

A modern WinUI 3 toolkit for **Sid Meier's Civilization VI** on Windows.

The first milestone establishes a reliable single-player trainer core while keeping the architecture ready for save management, map/game information, mod management and quick launch features.

## v0.1.0 foundation

- Automatic **Steam / Epic Games** installation discovery.
- Automatic **DX11 / DX12** detection from the running Civilization VI executable.
- Runtime process monitoring with PID, executable path and file-version reporting.
- Windows process-memory read/write layer.
- AoB pattern parser/scanner with wildcard support.
- Data-driven framework for the classic **22 trainer features** and their shortcuts.
- Trainer feature availability states; unsupported or unverified builds stay disabled instead of using guessed addresses.
- WinUI 3 shell with navigation reserved for Trainer, Save Manager, Maps & Game Info, Mod Manager and Quick Launch.
- CI and contract tests.

## Detection behavior

CivVIToolkit does not require the user to choose a storefront or renderer manually.

- Steam: reads Steam registry roots, library metadata and app manifest `289070`.
- Epic Games: reads Epic Games Launcher manifests from ProgramData.
- DX11: detected from `CivilizationVI.exe`.
- DX12: detected from `CivilizationVI_DX12.exe`.

If the game is already running, executable-path inference provides a fallback when launcher metadata is unavailable.

## Trainer status

The 22-feature catalog and low-level memory/AoB infrastructure are implemented in v0.1.0. Individual cheats intentionally remain **Signature pending** until their AoB signatures and patch semantics are verified against real Steam/Epic DX11/DX12 game builds. No fixed-address guesses are enabled.

See [`docs/TRAINER-SIGNATURES.md`](docs/TRAINER-SIGNATURES.md) for the compatibility strategy.

## Planned toolkit modules

- Save Manager — backups, tags, metadata and restore workflows.
- Maps & Game Info — map/ruleset/session information.
- Mod Manager — discovery, validation, dependencies and enable/disable workflows.
- Quick Launch — launch the detected Steam/Epic build with a selected renderer.

Architecture details: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Development

Requirements:

- Windows 10/11 x64
- .NET 10 SDK
- Visual Studio with WinUI / Windows App SDK tooling

Build:

```powershell
dotnet restore CivVIToolkit.sln -p:Platform=x64
dotnet build CivVIToolkit.sln -c Release -p:Platform=x64
```

## Scope

The trainer module is intended for local single-player use. Multiplayer cheating and anti-cheat bypass features are out of scope.

## License

MIT
