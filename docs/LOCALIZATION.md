# Localization

CivVIToolkit maintains three application languages:

- `zh-CN` — Simplified Chinese
- `ja-JP` — Japanese
- `en-US` — English

The three RESW sets must contain matching keys. CI runs `scripts/Test-LocalizationConsistency.ps1` and fails when a language has missing, extra, duplicate or empty resources.

Repository release notes should also be provided in the three languages for each published/acceptance version where release metadata references them.
