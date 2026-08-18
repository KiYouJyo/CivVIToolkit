# Documentation

CivVIToolkit keeps architecture, release, localization and trainer research notes under `docs/`.

## Key documents

- `ARCHITECTURE.md` — module and project boundaries
- `LOCALIZATION.md` — zh-CN / ja-JP / en-US resource contract
- `RELEASE.md` — MSIX acceptance and GitHub Release flow
- `ROADMAP.md` — planned toolkit modules
- `RUNTIME-DIAGNOSTICS.md` — runtime fingerprint collection
- `TRAINER-SIGNATURES.md` — exact-build trainer signatures and safety gates
- `releases/` — multilingual release notes

## Current trainer acceptance

The exact Steam / DX12 / Gathering Storm `1.0.12.68 (1023995)` profile is under grouped live acceptance. v0.2.2 specifically hardens local-player resolution when the game temporarily clears the GameContext root pointer while Player::Manager remains live.
