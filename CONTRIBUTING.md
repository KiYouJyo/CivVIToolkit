# Contributing

CivVIToolkit welcomes focused contributions to the modular Windows toolkit.

## Development baseline

- .NET 10
- WinUI 3 / Windows App SDK
- x64
- keep zh-CN / ja-JP / en-US resources consistent
- run the repository CI before merge

## Trainer changes

Trainer code is build-specific. Do not add guessed fixed addresses or enable writes on an unknown GameCore build. A trainer change should include:

1. an exact executable/GameCore fingerprint,
2. runtime AoB/RVA validation where applicable,
3. fail-closed behavior for unsupported builds,
4. live single-player acceptance evidence before marking a feature verified.

Runtime pointers are volatile. Prefer re-resolving the relevant live object chain rather than caching an address indefinitely.

## Scope

The project is for single-player local tooling. Multiplayer cheating and anti-cheat bypasses are not accepted.
