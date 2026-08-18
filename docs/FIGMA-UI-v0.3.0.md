# Figma UI integration — v0.3.0

Design source: Figma file `Y4is6b86iVXHXS0P3Cx1hi`, node `11:2`.

## Shell

The v0.3.0 shell follows the current Figma v2 boards instead of the earlier dark draft:

- 48 px WinUI-style custom title bar with app identity and connection badge;
- 220 px light navigation pane with a hamburger collapse control below the title bar;
- Home, Trainer, Save Manager, Mod Manager, and Encyclopedia at the top;
- Presets & Diagnostics and Settings & About pinned to the bottom;
- no extra Game/System navigation group headings;
- `#F8F8F8` content canvas, near-white cards, subtle `#D6D6DB` strokes, 8 px radii, and `#005EB8` primary accent;
- layout spacing and card proportions derived from the Figma Home (`13:122`) and Trainer (`13:216`) content hosts.

## Functional wiring

| UI page | v0.3.0 behavior |
| --- | --- |
| Home | Steam / Epic discovery, DX11 / DX12 recognition, process ID, game version, install paths, rescan, and launch buttons |
| Trainer | Organizes all 22 catalog features into four Figma categories, adds search, and keeps toggles disabled while signatures are unverified |
| Compact Trainer | Always-on-top compact window containing the same 22 feature entries; write controls stay gated |
| Presets & Diagnostics | Shows current runtime identity and copies the existing user-triggered read-only diagnostics JSON |
| Settings & About | Preserves the existing local language preference selector and version information |
| Save Manager | Empty entrance reserved for a later milestone |
| Mod Manager | Empty entrance reserved for a later milestone |
| Encyclopedia | Empty entrance reserved for a later milestone |

Quick launch is intentionally integrated into Home rather than occupying a separate navigation item, matching the current Figma navigation contract.

## Trainer category mapping

The first 22 entries are grouped as follows so the counts match the Figma design:

- Resources & Economy: 9;
- Science & Culture: 4;
- Units & Combat: 5;
- Cities & Production: 4 (builder charges are grouped here with production/build actions).

## Trainer integrity

This UI milestone does **not** add guessed memory addresses. `PendingSignatureTrainerEngine` remains the trainer backend, so write controls stay unavailable until signatures are verified against concrete Civilization VI builds.

The UI can therefore be tested independently from signature reverse-engineering without creating a false impression that unsupported memory patches are ready.
