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
[CmdletBinding()]
param([switch]$ImportCertificateOnly)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$msix = Get-ChildItem -LiteralPath $root -Filter '*.msix' -File | Select-Object -First 1
$cer = Get-ChildItem -LiteralPath $root -Filter '*.cer' -File | Select-Object -First 1
if (-not $msix) { throw 'CivVIToolkit MSIX was not found.' }
if (-not $cer) { throw 'CivVIToolkit public certificate was not found.' }

function Test-IsAdministrator {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$expectedThumbprint = '__THUMBPRINT__'
$expectedPackageVersion = '__PACKAGE_VERSION__'
$certificate = Get-PfxCertificate -FilePath $cer.FullName
if (-not $certificate -or $certificate.HasPrivateKey -or $certificate.Thumbprint -cne $expectedThumbprint) {
    throw "Certificate validation failed. Expected public certificate $expectedThumbprint."
}

$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
    Where-Object Thumbprint -eq $expectedThumbprint |
    Select-Object -First 1

if (-not $trusted -and $ImportCertificateOnly) {
    if (-not (Test-IsAdministrator)) { throw 'Certificate trust requires elevation.' }
    Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    Write-Host 'Trusted the CivVIToolkit public release certificate in LocalMachine TrustedPeople.'
    exit 0
}

if (-not $trusted -and -not (Test-IsAdministrator)) {
    Write-Host 'Windows will ask for elevation once to trust the public signing certificate.'
    $arguments = @(
        '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', "`"$PSCommandPath`"", '-ImportCertificateOnly'
    ) -join ' '
    $elevated = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -Wait -PassThru
    if ($elevated.ExitCode -ne 0) { throw 'Certificate trust setup was cancelled or failed.' }
    $trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
        Where-Object Thumbprint -eq $expectedThumbprint |
        Select-Object -First 1
    if (-not $trusted) { throw 'The public signing certificate is still not trusted after elevation.' }
}
elseif (-not $trusted) {
    Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    Write-Host 'Trusted the CivVIToolkit public release certificate in LocalMachine TrustedPeople.'
}

$signature = Get-AuthenticodeSignature -FilePath $msix.FullName
if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -cne $expectedThumbprint) {
    throw 'MSIX signer does not match the bundled public certificate.'
}

Write-Host "Installing $($msix.Name)..."
Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown -ErrorAction Stop
$packages = @(Get-AppxPackage -Name '7F943FBD-0D20-4D24-B20E-F59ADF91A916' -ErrorAction SilentlyContinue |
    Where-Object Publisher -eq 'CN=AppPublisher')
if ($packages.Count -ne 1) { throw "Package verification failed: expected one CivVIToolkit package, found $($packages.Count)." }
$package = $packages[0]
if ([string]$package.Version -ne $expectedPackageVersion -or [string]$package.Architecture -ne 'X64' -or [string]$package.Status -ne 'Ok') {
    throw "Package verification failed: Version=$($package.Version); Architecture=$($package.Architecture); Status=$($package.Status)"
}

Write-Host "Installed CivVIToolkit $($package.Version)."
Write-Host 'You can now launch Civ VI Toolkit from the Start menu.'
'@
$installer = $installer.Replace('__THUMBPRINT__', $certificate.Thumbprint).Replace('__PACKAGE_VERSION__', $PackageVersion)
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
- 推荐双击 Install-CivVIToolkit.cmd。
- 首次安装时，Windows 会弹出一次 UAC，只用于把公开签名证书加入 LocalMachine\TrustedPeople；PFX/私钥不在安装包中。
- 随后脚本会校验 MSIX 签名并通过 Add-AppxPackage 安装，最后检查版本、架构与包状态。
- 也可以手动把 .cer 安装到“本地计算机 → 受信任的人”，再双击 .msix。
- 若同一版本已经安装且需要重新验收，请先在 Windows 设置中卸载旧副本。
- Trainer 仅面向 Civilization VI 本地单机模式。

日本語
- Install-CivVIToolkit.cmd の実行を推奨します。
- 初回のみ UAC が表示され、公開署名証明書を LocalMachine\TrustedPeople に登録します。PFX / 秘密鍵は配布物に含まれません。
- その後、MSIX の署名を確認して Add-AppxPackage でインストールし、バージョン・アーキテクチャ・パッケージ状態を検証します。
- .cer を「ローカル コンピューター → 信頼されたユーザー」に手動登録してから .msix を開くこともできます。
- 同じバージョンを再検収する場合は、Windows の設定から既存のコピーをアンインストールしてください。
- Trainer は Civilization VI のローカル・シングルプレイ専用です。

English
- Recommended: run Install-CivVIToolkit.cmd.
- On first install, Windows requests elevation once to trust the public signing certificate in LocalMachine\TrustedPeople. The package never contains a PFX or private key.
- The script then validates the MSIX signer, installs with Add-AppxPackage, and verifies version, architecture, and package status.
- You may also manually trust the .cer under Local Computer → Trusted People, then double-click the .msix.
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
