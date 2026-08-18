# Release

CivVIToolkit uses signed x64 MSIX packages for acceptance and GitHub releases.

## Acceptance

The `Signed MSIX Acceptance` workflow validates release metadata, localization, tests, certificate configuration, builds/signs the x64 MSIX, creates a one-click installer package and uploads a SHA-256 manifest with the artifact.

Acceptance packages are Preview builds intended for real-machine validation before merge/release.

## Version contract

The following values must agree:

- `release/release.json` product version/packageVersion
- `CivVIToolkit.App.csproj` version fields
- `Package.appxmanifest` package identity version

## Current acceptance

v0.2.2 focuses on resilient exact-build local-player resolution after live testing showed that Civilization VI may temporarily clear the GameContext root while the single-player match remains active.

## GitHub release

Formal publication is driven through Release Orchestrator / Publish GitHub Release and remains disabled in `release/release.json` until the requested acceptance conditions are complete.
