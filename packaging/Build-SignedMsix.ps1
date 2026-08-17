param(
    [Parameter(Mandatory = $true)][string]$DisplayVersion,
    [Parameter(Mandatory = $true)][string]$PackageVersion,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$CertificateBase64,
    [Parameter(Mandatory = $true)][string]$CertificatePassword,
    [string]$ExpectedSubject = 'CN=AppPublisher',
    [string]$ExpectedThumbprint = 'BD85AD77A651C86CA01A480C8E9BC64952993F98'
)

$ErrorActionPreference = 'Stop'
$projectPath = 'src/CivVIToolkit.App/CivVIToolkit.App.csproj'
$manifestPath = 'src/CivVIToolkit.App/Package.appxmanifest'

if (-not (Test-Path -LiteralPath $projectPath)) { throw "Project not found: $projectPath" }
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Package manifest not found: $manifestPath" }

[xml]$project = Get-Content -Raw -LiteralPath $projectPath
$projectVersion = @($project.Project.PropertyGroup | ForEach-Object Version | Where-Object { $_ })[0]
if ($projectVersion -ne $DisplayVersion) {
    throw "Project version mismatch. Expected $DisplayVersion, found $projectVersion."
}

[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$identity = $manifest.SelectSingleNode("//*[local-name()='Identity']")
if (-not $identity) { throw 'Package Identity is missing.' }
if ($identity.Version -ne $PackageVersion) {
    throw "Package manifest version mismatch. Expected $PackageVersion, found $($identity.Version)."
}
if ($identity.Publisher -ne $ExpectedSubject) {
    throw "Package manifest Publisher mismatch. Expected $ExpectedSubject, found $($identity.Publisher)."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$buildOutput = Join-Path $env:RUNNER_TEMP "CivVIToolkit-msix-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $buildOutput | Out-Null

Write-Host "Building unsigned self-contained x64 MSIX for CivVIToolkit $DisplayVersion..."
dotnet build $projectPath -c Release -p:Platform=x64 -r win-x64 --no-restore `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxBundle=Never `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxPackageDir="$buildOutput\" `
    -p:SelfContained=true `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=false
if ($LASTEXITCODE -ne 0) { throw "MSIX build failed with exit code $LASTEXITCODE." }

$candidates = Get-ChildItem -LiteralPath $buildOutput -Recurse -Filter '*.msix' -File |
    Where-Object FullName -notmatch '[\\/]Dependencies[\\/]' |
    Sort-Object Length -Descending
if ($candidates.Count -eq 0) { throw "No MSIX was produced under $buildOutput." }
$builtMsix = $candidates[0]

$msixPath = Join-Path $OutputDirectory "CivVIToolkit_${PackageVersion}_x64.msix"
Copy-Item -LiteralPath $builtMsix.FullName -Destination $msixPath -Force

if ([string]::IsNullOrWhiteSpace($CertificateBase64) -or [string]::IsNullOrWhiteSpace($CertificatePassword)) {
    throw 'Release signing certificate secrets are missing.'
}

$signingDirectory = Join-Path $env:RUNNER_TEMP "CivVIToolkit-signing-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $signingDirectory | Out-Null
$pfxPath = Join-Path $signingDirectory 'release-signing.pfx'
[IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($CertificateBase64))
$securePassword = ConvertTo-SecureString $CertificatePassword -AsPlainText -Force
$certificate = Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $securePassword
if (-not $certificate -or -not $certificate.HasPrivateKey) {
    throw 'Signing certificate with private key could not be imported.'
}
if ($certificate.Subject -cne $ExpectedSubject) {
    throw "Signing certificate subject mismatch. Expected $ExpectedSubject, found $($certificate.Subject)."
}
if ($certificate.Thumbprint -cne $ExpectedThumbprint) {
    throw "Signing certificate thumbprint mismatch. Expected $ExpectedThumbprint, found $($certificate.Thumbprint)."
}

$kitsRoot = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
$signtool = Get-ChildItem (Join-Path $kitsRoot 'Windows Kits\10\bin') -Recurse -Filter signtool.exe |
    Where-Object FullName -match '\\x64\\signtool.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $signtool) { throw 'x64 signtool.exe was not found.' }

& $signtool.FullName sign /fd SHA256 /sha1 $certificate.Thumbprint /tr http://timestamp.digicert.com /td SHA256 $msixPath
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed with exit code $LASTEXITCODE." }

$cerPath = Join-Path $OutputDirectory "CivVIToolkit-$DisplayVersion.cer"
Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null

# The repository certificate is self-signed. For CI verification only, trust its public
# certificate as a root in the ephemeral CurrentUser store, run a full WinVerifyTrust
# verification, then remove that temporary trust anchor before the job exits. The
# distributed one-click installer still uses TrustedPeople and never receives a private key.
$rootStorePath = "Cert:\CurrentUser\Root\$($certificate.Thumbprint)"
try {
    Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    & $signtool.FullName verify /pa /v $msixPath
    if ($LASTEXITCODE -ne 0) { throw "signtool verify failed with exit code $LASTEXITCODE." }

    $signature = Get-AuthenticodeSignature -FilePath $msixPath
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -cne $ExpectedThumbprint) {
        throw 'Signed MSIX does not carry the expected repository signer.'
    }
    if ($signature.Status -ne 'Valid') {
        throw "Authenticode verification did not resolve to Valid: $($signature.Status) / $($signature.StatusMessage)"
    }
}
finally {
    if (Test-Path -LiteralPath $rootStorePath) { Remove-Item -LiteralPath $rootStorePath -Force }
}

Remove-Item -LiteralPath $pfxPath -Force
$privateStorePath = "Cert:\CurrentUser\My\$($certificate.Thumbprint)"
if (Test-Path -LiteralPath $privateStorePath) { Remove-Item -LiteralPath $privateStorePath -Force }

Write-Host "Signed MSIX ready: $msixPath"
Write-Host "Public certificate: $cerPath"
