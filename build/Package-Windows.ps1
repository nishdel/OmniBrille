[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier = 'win-x64',
    [string] $Version,
    [string] $NumericVersion,
    [string] $InnoCompiler,
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

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid semantic package version '$Version'."
}
if ($NumericVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Invalid numeric file version '$NumericVersion'."
}

$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\$RuntimeIdentifier"
$packageDirectory = Join-Path $repositoryRoot 'artifacts\packages'
New-Item -ItemType Directory -Path $publishDirectory,$packageDirectory -Force | Out-Null

if (-not $SkipRestore) {
    & dotnet restore (Join-Path $repositoryRoot 'src\OmniBrille.Desktop\OmniBrille.Desktop.csproj') --runtime $RuntimeIdentifier
    if ($LASTEXITCODE -ne 0) { throw 'runtime-specific dotnet restore failed.' }
}

$publishArguments = @(
    'publish',
    (Join-Path $repositoryRoot 'src\OmniBrille.Desktop\OmniBrille.Desktop.csproj'),
    '--configuration', $Configuration,
    '--runtime', $RuntimeIdentifier,
    '--self-contained', 'true',
    '--output', $publishDirectory,
    '--no-restore',
    '-p:PublishSingleFile=false',
    '-p:PublishTrimmed=false',
    "-p:Version=$Version",
    "-p:FileVersion=$NumericVersion",
    "-p:AssemblyVersion=$NumericVersion"
)
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$application = Join-Path $publishDirectory 'OmniBrille.exe'
if (-not (Test-Path -LiteralPath $application)) {
    throw "Published application was not found at '$application'."
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
& $InnoCompiler "/DAppVersion=$Version" "/DNumericVersion=$NumericVersion" "/DSourceDir=$publishDirectory" "/DOutputDir=$packageDirectory" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$package = Join-Path $packageDirectory "OmniBrille-$Version-win-x64-setup.exe"
if (-not (Test-Path -LiteralPath $package)) {
    throw "Expected installer was not produced at '$package'."
}

$publishedBytes = (Get-ChildItem -LiteralPath $publishDirectory -Recurse -File | Measure-Object -Property Length -Sum).Sum
$packageBytes = (Get-Item -LiteralPath $package).Length
[pscustomobject]@{
    Package = $package
    Version = $Version
    Runtime = $RuntimeIdentifier
    Deployment = 'Self-contained, non-trimmed, multi-file'
    PublishedBytes = $publishedBytes
    PackageBytes = $packageBytes
    Signed = $false
}
