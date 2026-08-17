param(
    [Parameter(Mandatory = $true)][string]$MsixPath,
    [Parameter(Mandatory = $true)][string]$CertificatePath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$DisplayVersion,
    [Parameter(Mandatory = $true)][string]$PackageVersion
)

$ErrorActionPreference = 'Stop'

$msix = (Resolve-Path -LiteralPath $MsixPath).Path
$certificateFile = (Resolve-Path -LiteralPath $CertificatePath).Path
$certificate = Get-PfxCertificate -FilePath $certificateFile
if (-not $certificate) { throw 'Unable to read the public signing certificate.' }
if ($certificate.HasPrivateKey) { throw 'The one-click package must never contain a private key.' }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$root = Join-Path $OutputDirectory "CivVIToolkit-v$DisplayVersion-x64-one-click"
if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
New-Item -ItemType Directory -Force -Path $root | Out-Null

$msixName = "CivVIToolkit_${PackageVersion}_x64.msix"
$cerName = "CivVIToolkit-$DisplayVersion.cer"
Copy-Item -LiteralPath $msix -Destination (Join-Path $root $msixName) -Force
Copy-Item -LiteralPath $certificateFile -Destination (Join-Path $root $cerName) -Force

$installer = @'
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$msix = Get-ChildItem -LiteralPath $root -Filter '*.msix' -File | Select-Object -First 1
$cer = Get-ChildItem -LiteralPath $root -Filter '*.cer' -File | Select-Object -First 1
if (-not $msix) { throw 'CivVIToolkit MSIX was not found.' }
if (-not $cer) { throw 'CivVIToolkit public certificate was not found.' }

$expectedThumbprint = '__THUMBPRINT__'
$certificate = Get-PfxCertificate -FilePath $cer.FullName
if (-not $certificate -or $certificate.Thumbprint -cne $expectedThumbprint) {
    throw "Certificate thumbprint mismatch. Expected $expectedThumbprint."
}

$trusted = Get-ChildItem Cert:\CurrentUser\TrustedPeople |
    Where-Object Thumbprint -eq $expectedThumbprint |
    Select-Object -First 1
if (-not $trusted) {
    Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null
    Write-Host 'Trusted the CivVIToolkit release certificate for the current user.'
}

$signature = Get-AuthenticodeSignature -FilePath $msix.FullName
if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -cne $expectedThumbprint) {
    throw 'MSIX signer does not match the bundled public certificate.'
}

Write-Host "Installing $($msix.Name)..."
Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown
$package = Get-AppxPackage -Name '7F943FBD-0D20-4D24-B20E-F59ADF91A916' |
    Sort-Object Version -Descending |
    Select-Object -First 1
if (-not $package) { throw 'CivVIToolkit package was not found after installation.' }

Write-Host "Installed CivVIToolkit $($package.Version)."
Write-Host 'You can now launch Civ VI Toolkit from the Start menu.'
'@
$installer = $installer.Replace('__THUMBPRINT__', $certificate.Thumbprint)
Set-Content -LiteralPath (Join-Path $root 'Install-CivVIToolkit.ps1') -Value $installer -Encoding utf8

$cmd = @'
@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-CivVIToolkit.ps1"
if errorlevel 1 (
  echo.
  echo CivVIToolkit installation failed.
) else (
  echo.
  echo CivVIToolkit installation completed.
)
pause
'@
Set-Content -LiteralPath (Join-Path $root 'Install-CivVIToolkit.cmd') -Value $cmd -Encoding ascii

$readme = @"
CivVIToolkit v$DisplayVersion · x64 MSIX

简体中文
- 推荐双击 Install-CivVIToolkit.cmd。它只会为当前用户信任仓库发布证书，然后安装已签名的 MSIX。
- 也可以先安装 .cer 到“受信任的人”，再双击 .msix。
- 若同一版本已经安装且需要重新验收，请先在 Windows 设置中卸载旧副本。
- Trainer 仅面向 Civilization VI 本地单机模式。

日本語
- Install-CivVIToolkit.cmd の実行を推奨します。現在のユーザーに公開証明書を信頼させた後、署名済み MSIX をインストールします。
- .cer を「信頼されたユーザー」に登録してから .msix を直接開くこともできます。
- 同じバージョンを再検収する場合は、Windows の設定から既存のコピーをアンインストールしてください。
- Trainer は Civilization VI のローカル・シングルプレイ専用です。

English
- Recommended: run Install-CivVIToolkit.cmd. It trusts the repository release certificate for the current user, then installs the signed MSIX.
- You may also trust the .cer in Trusted People first and double-click the .msix.
- If the same version is already installed and must be tested again, uninstall the old copy in Windows Settings first.
- The Trainer is for local Civilization VI single-player use only.

Package version: $PackageVersion
Signer: $($certificate.Subject)
Thumbprint: $($certificate.Thumbprint)
"@
Set-Content -LiteralPath (Join-Path $root 'README-INSTALL.txt') -Value $readme -Encoding utf8

$zip = Join-Path $OutputDirectory "CivVIToolkit-v$DisplayVersion-x64-one-click.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $root '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Output $zip
