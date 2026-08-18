# Architecture

CivVIToolkit is split into a small domain core, Windows platform services and a WinUI 3 application shell.

## Projects

- `CivVIToolkit.Core` — game/session models, toolkit module contracts, trainer catalog and interfaces.
- `CivVIToolkit.Platform.Windows` — Steam/Epic discovery, process monitoring, memory access, AoB scanning, diagnostics, hotkeys and exact-build trainer implementations.
- `CivVIToolkit.App` — WinUI 3 navigation, localization, settings and user interaction.
- `CivVIToolkit.Tests` — architecture and contract tests.

## Trainer boundary

Trainer implementations are build-specific and live under the Windows platform layer. The UI does not own memory addresses or signatures.

An exact trainer profile must:

1. fingerprint the target GameCore;
2. verify required AoB/RVA anchors;
3. resolve live runtime objects;
4. refuse writes when validation fails;
5. keep reversible patches restorable on disable/detach.

Runtime object pointers are treated as volatile. A UI transition may invalidate or temporarily clear one discovery route while other game systems remain live. v0.2.2 therefore allows the exact SHA-locked Steam/DX12 profile to fall back from GameContext to a separately validated Player::Manager local-player route instead of treating one transient null root as proof that the whole match is unavailable.

## Future modules

Save management, maps/game information, Mod management and quick launch remain independent from the trainer layer. They should not depend on GameCore memory editing unless a narrowly defined feature explicitly requires it.
