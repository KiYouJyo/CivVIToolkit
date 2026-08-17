param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ChineseNotes,
    [Parameter(Mandatory = $true)][string]$JapaneseNotes,
    [Parameter(Mandatory = $true)][string]$EnglishNotes,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
foreach ($path in @($ChineseNotes, $JapaneseNotes, $EnglishNotes)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release notes file not found: $path" }
}

$zh = Get-Content -Raw -LiteralPath $ChineseNotes
$ja = Get-Content -Raw -LiteralPath $JapaneseNotes
$en = Get-Content -Raw -LiteralPath $EnglishNotes

$body = @"
# CivVIToolkit v$Version

> Windows x64 · Signed MSIX · Preview

## 简体中文

$zh

## 日本語

$ja

## English

$en

---

### Downloads

- `CivVIToolkit_$Version.0_x64.msix` — signed x64 MSIX package.
- `CivVIToolkit-v$Version-x64-one-click.zip` — MSIX + public certificate + current-user installer helper.
- `SHA256SUMS.txt` — SHA-256 checksums for release assets.

CivVIToolkit is an independent community project and is not affiliated with Firaxis Games, 2K, Steam, or Epic Games. Trainer capabilities are intended for local single-player use only.
"@
Set-Content -LiteralPath $OutputPath -Value $body -Encoding utf8
