param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedDisplayVersion,
    [Parameter(Mandatory = $true)][string]$ExpectedPackageVersion
)

$ErrorActionPreference = 'Stop'

$root = Join-Path $PackageDirectory "CivVIToolkit-v$ExpectedDisplayVersion-x64-one-click"
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "One-click package root was not found: $root"
}

$msix = Join-Path $root "CivVIToolkit_${ExpectedPackageVersion}_x64.msix"
$cer = Join-Path $root "CivVIToolkit-$ExpectedDisplayVersion.cer"
$installer = Join-Path $root 'Install-CivVIToolkit.ps1'
foreach ($required in @($msix, $cer, $installer, (Join-Path $root 'Install-CivVIToolkit.cmd'), (Join-Path $root 'README-INSTALL.txt'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required package file is missing: $required" }
}

$certificate = Get-PfxCertificate -FilePath $cer
if (-not $certificate) { throw 'Public certificate is unreadable.' }
if ($certificate.HasPrivateKey) { throw 'Private key material leaked into the one-click package.' }
if ($certificate.Subject -cne 'CN=AppPublisher') { throw "Unexpected public certificate subject: $($certificate.Subject)" }

$signature = Get-AuthenticodeSignature -FilePath $msix
if (-not $signature.SignerCertificate) { throw 'The MSIX is not Authenticode signed.' }
if ($signature.SignerCertificate.Thumbprint -cne $certificate.Thumbprint) {
    throw 'MSIX signer and bundled public certificate do not match.'
}

$installerText = Get-Content -Raw -LiteralPath $installer
foreach ($requiredContract in @('Cert:\LocalMachine\TrustedPeople', 'Start-Process', '-Verb RunAs', 'Add-AppxPackage', $ExpectedPackageVersion, $certificate.Thumbprint)) {
    if ($installerText -notmatch [regex]::Escape($requiredContract)) {
        throw "Installer contract is missing: $requiredContract"
    }
}
if ($installerText.Contains('Cert:\CurrentUser\TrustedPeople')) {
    throw 'Installer must not rely on CurrentUser TrustedPeople for MSIX sideload trust.'
}

$inspection = Join-Path $env:RUNNER_TEMP "CivVIToolkit-msix-inspection-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $inspection | Out-Null
$zipCopy = Join-Path $inspection 'package.zip'
Copy-Item -LiteralPath $msix -Destination $zipCopy
Expand-Archive -LiteralPath $zipCopy -DestinationPath (Join-Path $inspection 'expanded') -Force
$manifestPath = Join-Path $inspection 'expanded\AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'AppxManifest.xml was not found inside the MSIX.' }

[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$identity = $manifest.SelectSingleNode("//*[local-name()='Identity']")
if (-not $identity) { throw 'MSIX Identity is missing.' }
if ($identity.Version -ne $ExpectedPackageVersion) {
    throw "MSIX version mismatch. Expected $ExpectedPackageVersion, found $($identity.Version)."
}
if ($identity.Publisher -ne 'CN=AppPublisher') { throw "Unexpected MSIX publisher: $($identity.Publisher)" }
if ($identity.Name -ne '7F943FBD-0D20-4D24-B20E-F59ADF91A916') { throw "Unexpected MSIX identity name: $($identity.Name)" }

$languages = @($manifest.SelectNodes("//*[local-name()='Resources']/*[local-name()='Resource']") | ForEach-Object Language)
foreach ($language in @('zh-CN', 'ja-JP', 'en-US')) {
    if ($language -notin $languages) { throw "MSIX resource language is missing: $language" }
}
$fullTrust = $manifest.SelectSingleNode("//*[local-name()='Capability' and @Name='runFullTrust']")
if (-not $fullTrust) { throw 'runFullTrust capability is missing from the MSIX manifest.' }

$privateArtifacts = Get-ChildItem -LiteralPath $root -Recurse -File |
    Where-Object Extension -in @('.pfx', '.p12', '.key')
if ($privateArtifacts) { throw 'Private signing material is present in the distribution package.' }

Remove-Item -LiteralPath $inspection -Recurse -Force
Write-Host "One-click package contract OK: CivVIToolkit $ExpectedDisplayVersion / $ExpectedPackageVersion."
