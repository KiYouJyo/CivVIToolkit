# Privacy

CivVIToolkit is designed as a local desktop utility.

## Local data

Application settings and toolkit data are stored locally on the user's device unless a future feature explicitly documents an external destination chosen by the user.

## Runtime diagnostics

The optional “Copy diagnostics” action may include game process metadata such as:

- Civilization VI executable path and version
- Steam / Epic and DX11 / DX12 detection result
- executable and GameCore SHA-256 fingerprints
- loaded module base/size metadata
- build-specific trainer probe values and verified RVA names

It does not intentionally collect Steam/Epic credentials, authentication tokens, save-file contents, or arbitrary process-memory dumps.

Diagnostics are copied to the local clipboard for troubleshooting. CivVIToolkit does not automatically upload them.

## Network

The core application is intended to function locally. Any future networked module will be documented separately before release.
