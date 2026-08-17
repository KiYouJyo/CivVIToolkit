$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$resourceRoot = Join-Path $root 'src/CivVIToolkit.App/Strings'
$languages = @('zh-CN', 'ja-JP', 'en-US')

function Get-ResourceMap([string]$language) {
    $path = Join-Path $resourceRoot "$language/Resources.resw"
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing localization resource: $path"
    }

    [xml]$xml = Get-Content -LiteralPath $path -Raw
    $map = @{}
    foreach ($node in @($xml.root.data)) {
        if ([string]::IsNullOrWhiteSpace($node.name)) { continue }
        if ($map.ContainsKey($node.name)) { throw "Duplicate resource key '$($node.name)' in $language" }
        $map[$node.name] = [string]$node.value
    }
    return $map
}

$maps = @{}
foreach ($language in $languages) {
    $maps[$language] = Get-ResourceMap $language
}

$baseline = @($maps['zh-CN'].Keys | Sort-Object)
foreach ($language in $languages) {
    $keys = @($maps[$language].Keys | Sort-Object)
    $missing = @($baseline | Where-Object { $_ -notin $keys })
    $extra = @($keys | Where-Object { $_ -notin $baseline })
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        throw "$language resource keys differ. Missing=[$($missing -join ', ')]; Extra=[$($extra -join ', ')]"
    }

    foreach ($key in $baseline) {
        if ([string]::IsNullOrWhiteSpace($maps[$language][$key])) {
            throw "$language has an empty value for '$key'."
        }
    }
}

Write-Host "Localization contract OK: $($baseline.Count) keys across $($languages -join ', ')."
