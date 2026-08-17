# Runtime diagnostics

The first verified trainer signatures must be derived from real Civilization VI builds. CivVIToolkit therefore includes a deliberately read-only runtime diagnostics snapshot before any signature-dependent feature is enabled.

When Civilization VI is running, **Copy diagnostics** collects:

- detected storefront (`Steam` / `EpicGames`)
- renderer (`DirectX11` / `DirectX12`)
- process ID
- executable path
- the raw Civilization VI file-version string plus its normalized version when available
- SHA-256 of the running executable
- main-module base address
- main-module image size
- matched installation root and whether launcher metadata matched it
- when a Gathering Storm match has loaded `GameCore_XP2_FinalRelease.dll`: its path, SHA-256, module base address and image size
- the adjacent `GameCore_XP2_FinalRelease.map` path when the shipped symbol map is present

The JSON snapshot is copied to the clipboard. It contains no save-game contents, account credentials, launcher tokens or arbitrary memory dump data.

## Fingerprint versus signature

A diagnostics JSON is a **build fingerprint**, not an AoB signature. It identifies the exact executable and, once the match core is loaded, the exact Gathering Storm GameCore module that a verified trainer profile must target. Users do not need to copy diagnostics during normal use; it exists for development and troubleshooting.

Civilization VI loads much of Gathering Storm's game logic through `GameCore_XP2_FinalRelease.dll` after entering a match. CivVIToolkit therefore tracks that module independently from `CivilizationVI.exe` / `CivilizationVI_DX12.exe`. A future signature profile may use the executable fingerprint, the GameCore fingerprint and verified AoB patterns together rather than assuming a fixed absolute address.

Actual AoB signatures and patch semantics remain separate from diagnostics and must still be verified before a trainer switch becomes available.
