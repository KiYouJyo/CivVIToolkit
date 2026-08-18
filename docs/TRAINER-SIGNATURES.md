# Trainer Signatures

CivVIToolkit treats memory editing as build-specific functionality. A trainer feature is enabled only after the target Civilization VI GameCore build has been fingerprinted and its required runtime anchors have been verified.

## Verified Steam / DX12 Gathering Storm build

- Civilization VI version: `1.0.12.68 (1023995)`
- Store / renderer: Steam / DX12
- EXE SHA-256: `c2c3d40b86260a541d8a4d38cb70d50d3406ae1de374afc382d1e42bc1342f1e`
- GameCore: `GameCore_XP2_FinalRelease.dll`
- GameCore SHA-256: `324c51e9ea3531758842e16c69e6cddbefbb226c5b675c3d6e60111646c2e98c`

## Runtime anchor policy

The exact-build profile verifies 13 AoB anchors before enabling writes. The anchors cover GameContext, Player::Manager, live player component bridges, and verified Gold/Faith routines. A matching SHA alone is not sufficient; expected RVAs must also match the loaded module.

## Local-player resolution

The preferred route is:

`GameCore -> GameContext root -> current game -> LocalPlayerId -> Player::Manager -> live Player::Instance`

Real-game acceptance showed that Civilization VI can temporarily clear the GameContext root pointer while the match itself and Player::Manager remain live. Starting with v0.2.2, the exact SHA-locked single-player profile therefore has a narrow fallback:

`Player::Manager -> verified single-player slot 0 -> live Treasury / Religion / Influence components`

The fallback is accepted only when slot 0 is active, a live Player pointer exists, and the already verified Treasury, Religion, and Influence component pointers are all non-null. If any check fails, the trainer refuses to write.

This fallback is intentionally scoped to the exact Steam/DX12 `1023995` profile and does not generalize to unknown builds.

## Safety rules

- Never persist an absolute module base address; ASLR changes it between launches.
- Never enable a write path solely from a guessed fixed address.
- Fingerprint the GameCore module and verify AoB/RVA anchors before writes.
- Restore reversible code patches when the feature is disabled or the trainer detaches.
- Treat runtime object pointers as volatile and re-resolve them as needed.
- Single-player use only; the project does not target multiplayer cheating or anti-cheat bypasses.
