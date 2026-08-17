# Trainer signature strategy

CivVIToolkit does not ship trainer writes from guessed offsets. The memory layer and 22-feature catalog are present, while each feature remains unavailable until its target is verified against a real game build.

## Why signatures instead of fixed addresses

Civilization VI updates, store builds and DX11/DX12 executables can move code and data. Absolute runtime addresses are therefore intentionally not part of the feature contract. A verified profile identifies the exact executable/GameCore build, resolves code by AoB signature, validates expected RVAs or surrounding bytes, and only then permits build-specific state access.

Gathering Storm loads much of the relevant gameplay logic from `GameCore_XP2_FinalRelease.dll` after a match is entered, so the GameCore fingerprint is a first-class part of the profile identity.

## Profile identity

A profile is keyed by:

- store: `steam` or `epic`
- renderer: `dx11` or `dx12`
- Civilization VI executable file version/build
- executable SHA-256 when available
- loaded GameCore module name and SHA-256

## First research profile: Steam / DX12 / build 1023995

The first real-machine validation supplied the exact Gathering Storm GameCore module for Civilization VI `1.0.12.68 (1023995)`.

- profile: `steam-dx12-gs-1.0.12.68-1023995`
- executable SHA-256: `c2c3d40b86260a541d8a4d38cb70d50d3406ae1de374afc382d1e42bc1342f1e`
- GameCore SHA-256: `324c51e9ea3531758842e16c69e6cddbefbb226c5b675c3d6e60111646c2e98c`
- GameCore image size: `0xC60000`

Static analysis of that exact module established unique anchors for the game root/local-player path, Player Manager, Treasury, Religion/Faith and Influence paths, plus the Gold/Faith balance routines. The development probe verifies those anchors against the *loaded* module before reading any game state.

### Live Player vs Player Cache

The first probe iteration followed the `Player::Cache::Instance` layout (`[player+0xB0] + component offset`) and therefore did not represent the live `Player::Instance` returned by `Player::Manager`.

The second live run exposed that distinction directly: the live player's `+0xB0` cache-component pointer is null. Inspection of the exact GameCore shows that the game's own bridge layer has two branches:

- live `Player::Instance`: direct component pointers
  - Religion: `[player+0x720]`
  - Influence: `[player+0x748]`
  - Treasury: `[player+0x780]`
- `Player::Cache::Instance`: `[cache+0xB0] + component offset`

The v0.1.2 probe follows the live branch and adds AoB anchors for the bridge routines themselves, so the direct component offsets are verified against code from the exact supported GameCore build rather than stored as unverified structure guesses.

For this profile the probe remains deliberately read-only. It resolves the local human player, reads fixed-point Gold, Faith and Influence values, and compares them with the in-game UI before any write-capable trainer feature is enabled.

The uploaded proprietary game DLL is never committed to this repository or included in build artifacts. Only hashes, independently derived layout metadata and short verification signatures are stored.

## Patch lifecycle

1. Attach only to a detected Civilization VI process.
2. Require the expected GameCore module to be loaded for Gathering Storm-specific features.
3. Resolve the matching profile by store/renderer/build fingerprint.
4. Scan and validate every requested feature before exposing its switch.
5. Preserve original bytes/state before writing.
6. Restore patches on disable, detach and normal application shutdown.
7. If a fingerprint/signature is absent or mismatched, show an unsupported/pending state rather than guessing.

The current trainer engine deliberately keeps the 22 feature rows in `SignaturePending` until the live read/write path for each feature has been validated. The build probe is a development bridge: passing it proves the first profile's runtime pointer chain and signatures, but does not itself enable a cheat switch.
