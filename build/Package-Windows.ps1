[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier = 'win-x64',
    [string] $Version,
    [string] $NumericVersion,
    [string] $AssemblyVersion,
    [string] $InnoCompiler,
    [string] $SigningCertificateThumbprint = $env:OMNIBRILLE_SIGNING_CERTIFICATE_THUMBPRINT,
    [string] $TimestampServer = 'http://timestamp.digicert.com',
    [switch] $RequireSigning,
    [switch] $BootstrapInnoSetup,
    [switch] $SkipRestore
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml] $buildProperties = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props')
$properties = $buildProperties.Project.PropertyGroup | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = "$($properties.VersionPrefix)-$($properties.VersionSuffix)"
}
if ([string]::IsNullOrWhiteSpace($NumericVersion)) {
    $NumericVersion = [string] $properties.FileVersion
}
if ([string]::IsNullOrWhiteSpace($AssemblyVersion)) {
    $AssemblyVersion = [string] $properties.AssemblyVersion
}

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid semantic package version '$Version'."
}
if ($NumericVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Invalid numeric file version '$NumericVersion'."
}
if ($AssemblyVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Invalid assembly version '$AssemblyVersion'."
}
if ($RequireSigning -and [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
    throw 'Signing was required, but no certificate thumbprint was supplied.'
}

$signingCertificate = $null
if (-not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
    $normalizedThumbprint = $SigningCertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    $certificatePaths = @(
        "Cert:\CurrentUser\My\$normalizedThumbprint",
        "Cert:\LocalMachine\My\$normalizedThumbprint"
    )
    $signingCertificate = $certificatePaths |
        Where-Object { Test-Path -LiteralPath $_ } |
        ForEach-Object { Get-Item -LiteralPath $_ } |
        Where-Object { $_.HasPrivateKey -and $_.NotAfter -gt [DateTime]::UtcNow } |
        Select-Object -First 1
    if ($null -eq $signingCertificate) {
        throw 'The requested signing certificate was not found, lacks a private key, or is expired.'
    }
}

function Set-VerifiedAuthenticodeSignature {
    param([Parameter(Mandatory)][string] $FilePath)

    $arguments = @{
        FilePath = $FilePath
        Certificate = $signingCertificate
        HashAlgorithm = 'SHA256'
    }
    if (-not [string]::IsNullOrWhiteSpace($TimestampServer)) {
        $arguments.TimestampServer = $TimestampServer
    }
    $signature = Set-AuthenticodeSignature @arguments
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode signing failed for '$([System.IO.Path]::GetFileName($FilePath))': $($signature.StatusMessage)"
    }
}

$artifactsDirectory = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactsDirectory "publish\$RuntimeIdentifier"
$packageDirectory = Join-Path $artifactsDirectory 'packages'
$resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsDirectory) + [System.IO.Path]::DirectorySeparatorChar
$resolvedPublish = [System.IO.Path]::GetFullPath($publishDirectory)
if (-not $resolvedPublish.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean unexpected publish directory '$resolvedPublish'."
}
if (Test-Path -LiteralPath $resolvedPublish) {
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedPublish,$packageDirectory -Force | Out-Null

$desktopProject = Join-Path $repositoryRoot 'src\OmniBrille.Desktop\OmniBrille.Desktop.csproj'
if (-not $SkipRestore) {
    & dotnet restore $desktopProject --runtime $RuntimeIdentifier
    if ($LASTEXITCODE -ne 0) { throw 'Runtime-specific dotnet restore failed.' }
}

$publishArguments = @(
    'publish',
    $desktopProject,
    '--configuration', $Configuration,
    '--runtime', $RuntimeIdentifier,
    '--self-contained', 'true',
    '--output', $resolvedPublish,
    '--no-restore',
    '-p:PublishSingleFile=false',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    "-p:Version=$Version",
    "-p:FileVersion=$NumericVersion",
    "-p:AssemblyVersion=$AssemblyVersion",
    "-p:InformationalVersion=$Version",
    '-p:IncludeSourceRevisionInInformationalVersion=false'
)
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# Runtime packs may carry native-library symbols even when project debug symbols are disabled.
# They are useful to developers but must not enter preview publish/install artifacts.
Get-ChildItem -LiteralPath $resolvedPublish -Recurse -File -Filter '*.pdb' |
    Remove-Item -Force

$application = Join-Path $resolvedPublish 'OmniBrille.exe'
if (-not (Test-Path -LiteralPath $application)) {
    throw "Published application was not found at '$application'."
}
if ($null -ne $signingCertificate) {
    Set-VerifiedAuthenticodeSignature -FilePath $application
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $candidates = @(
        (Join-Path $repositoryRoot 'artifacts\tools\inno-setup-6.7.3\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($InnoCompiler) -and $BootstrapInnoSetup) {
    $InnoCompiler = & (Join-Path $PSScriptRoot 'Get-InnoSetup.ps1')
}
if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw 'Inno Setup 6.7.3 was not found. Pass -InnoCompiler or use -BootstrapInnoSetup.'
}

$installerScript = Join-Path $repositoryRoot 'installer\OmniBrille.iss'
& $InnoCompiler "/DAppVersion=$Version" "/DNumericVersion=$NumericVersion" "/DSourceDir=$resolvedPublish" "/DOutputDir=$packageDirectory" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$package = Join-Path $packageDirectory "OmniBrille-$Version-win-x64-setup.exe"
if (-not (Test-Path -LiteralPath $package)) {
    throw "Expected installer was not produced at '$package'."
}
if ($null -ne $signingCertificate) {
    Set-VerifiedAuthenticodeSignature -FilePath $package
}

$publishedBytes = (Get-ChildItem -LiteralPath $resolvedPublish -Recurse -File | Measure-Object -Property Length -Sum).Sum
$releaseArtifacts = & (Join-Path $PSScriptRoot 'New-ReleaseArtifacts.ps1') `
    -PackagePath $package `
    -Version $Version `
    -NumericVersion $NumericVersion `
    -RuntimeIdentifier $RuntimeIdentifier `
    -PublishedBytes $publishedBytes `
    -Signed:($null -ne $signingCertificate)

[pscustomobject]@{
    Package = $package
    Version = $Version
    Runtime = $RuntimeIdentifier
    Deployment = 'Self-contained, non-trimmed, multi-file'
    PublishedBytes = $publishedBytes
    PackageBytes = (Get-Item -LiteralPath $package).Length
    Signed = $null -ne $signingCertificate
    SignatureStatus = $releaseArtifacts.SignatureStatus
    Sha256 = $releaseArtifacts.Sha256
    Checksum = $releaseArtifacts.Checksum
    Manifest = $releaseArtifacts.Manifest
    DependencyManifest = $releaseArtifacts.DependencyManifest
    PreviewNotes = $releaseArtifacts.PreviewNotes
}
