# Figma UI integration — v0.3.0

Design source: Figma file `Y4is6b86iVXHXS0P3Cx1hi`, node `11:2`.

## Shell

The v0.3.0 shell replaces the stock `NavigationView` layout with a custom WinUI 3 two-column shell that follows the current Figma direction:

- compact dark sidebar with a hamburger collapse control;
- primary navigation at the top and Diagnostics / Settings pinned to the bottom;
- dark graphite surfaces, low-saturation cyan accent, restrained borders and 12 px cards;
- custom in-content page headers and status cards rather than dense form-like panels.

## Functional wiring

| UI page | v0.3.0 behavior |
| --- | --- |
| Home | Steam / Epic discovery, DX11 / DX12 recognition, process ID, game version, install paths, refresh, diagnostics copy |
| Trainer | Shows all 22 catalog features and their shortcuts; intentionally disabled while signatures are unverified |
| Quick Launch | Starts the discovered DX11 or DX12 executable; disabled while a Civ VI process is already running |
| Diagnostics | Shows current runtime identity and copies the existing read-only diagnostics JSON |
| Settings | Preserves the existing local language preference selector |
| Save Manager | Empty entrance reserved for a later milestone |
| Mod Manager | Empty entrance reserved for a later milestone |
| Encyclopedia | Empty entrance reserved for a later milestone |

## Trainer integrity

This UI milestone does **not** add guessed memory addresses. `PendingSignatureTrainerEngine` remains the trainer backend, so the 22 controls remain unavailable until signatures are verified against concrete Civilization VI builds.

The UI can therefore be tested independently from signature reverse-engineering without creating a false impression that unsupported memory patches are ready.
