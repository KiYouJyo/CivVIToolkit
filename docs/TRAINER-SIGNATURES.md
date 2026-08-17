# Trainer signature strategy

CivVIToolkit does not ship trainer writes from guessed offsets. Every write-capable path is tied to an exact Civilization VI build fingerprint and fails closed when the GameCore fingerprint, required runtime signatures, pointer chain, or patch preconditions do not match.

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

## First verified profile: Steam / DX12 / build 1023995

The first real-machine validation supplied the exact Gathering Storm GameCore module for Civilization VI `1.0.12.68 (1023995)`.

- profile: `steam-dx12-gs-1.0.12.68-1023995`
- executable SHA-256: `c2c3d40b86260a541d8a4d38cb70d50d3406ae1de374afc382d1e42bc1342f1e`
- GameCore SHA-256: `324c51e9ea3531758842e16c69e6cddbefbb226c5b675c3d6e60111646c2e98c`
- GameCore image size: `0xC60000`

Static and live validation established 13 runtime anchors spanning the game context, local-player path, real `Player::Manager`, live Treasury / Religion / Influence bridges, and the Gold/Faith routines.

### Live Player vs Player Cache

The first probe iteration followed the `Player::Cache::Instance` layout (`[player+0xB0] + component offset`) and therefore did not represent the live `Player::Instance` returned by `Player::Manager`.

The second live run exposed that distinction directly: the live player's `+0xB0` cache-component pointer is null. Inspection of the exact GameCore shows that the game's own bridge layer has two branches:

- live `Player::Instance`: direct component pointers
  - Units: `[player+0x6C8]`
  - Cities: `[player+0x6D0]`
  - Culture: `[player+0x6F0]`
  - Resources: `[player+0x700]`
  - Techs: `[player+0x718]`
  - Religion: `[player+0x720]`
  - Influence: `[player+0x748]`
  - Treasury: `[player+0x780]`
- `Player::Cache::Instance`: `[cache+0xB0] + component offset`

The corrected probe follows the live branch and validates the bridge routines themselves by AoB. The subsequent real-machine run matched the in-game Gold and Faith values, completing the read-chain acceptance gate.

## First verified write: Add Gold +10,000

v0.1.3 introduced `player.add-gold` for the exact profile above. The real-machine acceptance test confirmed that `PageUp` changes the live Gold balance by exactly +10,000.

Before the write, CivVIToolkit re-runs the complete read-only probe so the GameCore fingerprint, all 13 AoB anchors, the local player, and the live Treasury pointer must still validate. Gold is stored using Civilization VI's 1/256 fixed-point representation and is read back immediately after the write.

## v0.2.0 complete 22-feature acceptance profile

v0.2.0 promotes the remaining 21 catalog entries from `SignaturePending` to executable acceptance implementations for this exact build. This means the implementation exists and CI/MSIX packaging can be tested; it does **not** mean all 21 have already completed live gameplay acceptance.

The profile uses three mechanisms:

1. **Verified component state** — Gold, Faith, Influence, movement, damage, builder charges, population and resource stockpiles are read/written through the exact live-player object graph.
2. **Verified native GameCore routines** — research, civic and production paths use exact RVAs from the supplied GameCore build through a temporary Windows x64 call bridge. The bridge allocates a short-lived call stub, invokes the function, reads the result and immediately frees the allocation; no persistent injected DLL is installed.
3. **Reversible exact-byte patches** — the upgrade toggle uses narrow instruction patches only after the expected original bytes are checked. Original bytes are retained and restored on disable, detach and normal application shutdown.

### Implemented acceptance behaviors

- `Num 1` Unlimited Gold: periodically maintains the local Treasury balance.
- `Num 2` Unlimited Faith: periodically maintains the local Religion/Faith balance.
- `Num 3` One-Turn Research: applies high progress to the currently researched technology through the GameCore research-progress routine.
- `Num 4` One-Turn Civic: applies high progress to the current civic through the GameCore culture-progress routine.
- `Num 5` Add Influence: adds the configured fixed-point Influence amount.
- `Num 6` Unlimited Movement: periodically replenishes local-unit movement.
- `Num 7` Unlimited Health: periodically clears local-unit damage.
- `Num 8` One-Turn Construction / Recruitment: resolves each local city's live BuildQueue and invokes the exact finish-progress routine for active production.
- `Num 9` All Luxury / Strategic Resources: maintains the local live resource stockpile entries.
- `Num 0` Units Can Always Upgrade: maintains Gold/resources and applies the two exact-build upgrade eligibility patches for economic/location checks while preserving target/technology validation.
- `Num .` Maximum City Population: periodically maintains local city population at the acceptance ceiling.
- `PageUp` Add Gold: verified +10,000 value action.
- `PageDown` Unlimited Builder Charges: periodically replenishes local builder charges.
- `Alt+Num 1` / `Alt+Num 2` / `Alt+Num 5`: periodically zero non-local Gold, Faith and Influence.
- `Alt+Num 3` / `Alt+Num 4`: reset current non-local research/civic progress.
- `Alt+Num 6`: keeps non-local unit movement at zero.
- `Alt+Num 7` One-Hit Kill: acceptance implementation keeps non-local units at 99 damage so the next valid damage event is lethal.
- `Alt+Num 8`: resets the active production item's progress for non-local cities.
- `Alt+Num 9`: clears non-local resource stockpiles.

`unit.always-upgrade`, `combat.one-hit-kill`, and `ai.block-production` are deliberately surfaced as **experimental acceptance** in v0.2.0 because their high-level gameplay semantics have more edge cases than the direct resource paths and require focused live validation before being described as fully verified.

## Patch and call lifecycle

1. Attach only to a detected Civilization VI process.
2. Require the expected Gathering Storm GameCore to be loaded.
3. Resolve the exact profile by store/renderer/build fingerprint.
4. Validate the profile's runtime AoB anchors before enabling trainer rows.
5. Revalidate the exact profile before explicit actions and toggle changes.
6. For direct state writes, use the verified live object graph and fixed-point/storage layout.
7. For native calls, use only exact-build RVAs and a short-lived call bridge.
8. For code patches, require exact original bytes, preserve them, and restore them on disable/detach/shutdown.
9. If a fingerprint, pattern, pointer, original byte, or read/write verification is absent or mismatched, fail closed rather than guessing.

## Acceptance order

For live validation, test a disposable single-player save in four groups rather than enabling everything simultaneously:

1. resources and balances: Num 1, Num 2, Num 5, Num 9, PageUp;
2. progress: Num 3, Num 4, Num 8;
3. local units/cities: Num 6, Num 7, Num 0, Num ., PageDown;
4. AI/combat: Alt+Num 1 through Alt+Num 9.

The uploaded proprietary GameCore DLL is never committed to this repository or included in build artifacts. Only fingerprints, independently derived layout metadata, narrow verification signatures and exact-build implementation metadata are stored.
