# Roadmap

## Current milestone — Trainer exact-build acceptance

- Steam / DX12 / Gathering Storm `1.0.12.68 (1023995)` exact profile
- 22 classic single-player trainer entries
- live grouped acceptance and runtime hardening
- v0.2.2: resilient local-player resolution when GameContext root is transiently null

## Next trainer milestones

- finish live verification for all 22 entries
- classify each feature as verified / experimental / unsupported for the exact profile
- add additional Civilization VI builds only after independent fingerprint/signature validation
- keep unknown builds fail-closed

## Toolkit expansion

- Save Manager
- Maps & Game Info
- Mod Manager
- Quick Launch
- Settings and diagnostics improvements

These modules remain separated from the trainer memory layer so the application can grow without coupling unrelated functionality to GameCore editing.
