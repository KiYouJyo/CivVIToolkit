# Trainer signature strategy

CivVIToolkit does not ship v0.1.0 with guessed offsets. The memory layer and 22-feature catalog are present, while each feature remains unavailable until a signature is verified against a real game build.

## Why signatures instead of fixed addresses

Civilization VI updates, store builds and DX11/DX12 executables can move code and data. Fixed addresses are therefore intentionally not part of the feature contract. A verified profile should identify code/data by AoB signature and validate surrounding bytes before a patch is activated.

## Planned profile identity

A profile should be keyed by:

- store: `steam` or `epic`
- renderer: `dx11` or `dx12`
- executable file version
- optional executable hash for strict verification

## Patch lifecycle

1. Attach only to a detected Civilization VI process.
2. Resolve the matching signature profile.
3. Scan and validate every requested feature before exposing its switch.
4. Preserve original bytes/state before writing.
5. Restore patches on disable, detach and normal application shutdown.
6. If a signature is absent or ambiguous, show `UnsupportedGameVersion` rather than guessing.

The initial `PendingSignatureTrainerEngine` deliberately enforces this rule: the UI can attach to the process, but the 22 feature rows stay in `SignaturePending` state until verified signatures are added.
