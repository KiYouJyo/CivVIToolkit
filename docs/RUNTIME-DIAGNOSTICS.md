# Runtime Diagnostics

CivVIToolkit can copy a JSON runtime diagnostic snapshot for exact-build troubleshooting.

Typical fields include:

- process ID, store and graphics backend
- executable path/version/SHA-256
- module base and image size
- Gathering Storm GameCore path/SHA-256/base/size
- exact trainer profile ID
- verified AoB RVA map
- local player ID and selected live component addresses
- read-only Gold/Faith/Influence probe values when available
- an error message when the trainer probe cannot be resolved

Starting with v0.2.2, the trainer snapshot `ResourcePath` also records whether local-player resolution used the preferred GameContext route or the verified single-player Player::Manager slot-0 fallback.

The diagnostics action does not intentionally include save contents, credentials, authentication tokens or arbitrary memory dumps. The JSON is copied to the local clipboard and is not automatically uploaded.
