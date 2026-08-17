# Runtime diagnostics

The first verified trainer signatures must be derived from real Civilization VI builds. CivVIToolkit therefore includes a deliberately read-only runtime diagnostics snapshot before any signature-dependent feature is enabled.

When Civilization VI is running, **Copy diagnostics** collects:

- detected storefront (`Steam` / `EpicGames`)
- renderer (`DirectX11` / `DirectX12`)
- process ID
- executable path
- executable file version
- SHA-256 of the running executable
- main-module base address
- main-module image size
- matched installation root and whether launcher metadata matched it

The JSON snapshot is copied to the clipboard. It contains no save-game contents, account credentials, launcher tokens or arbitrary memory dump data.

This information is enough to identify a build reproducibly and decide which versioned signature profile should be loaded. Actual AoB signatures and patch semantics remain separate from diagnostics and must still be verified before a trainer switch becomes available.
