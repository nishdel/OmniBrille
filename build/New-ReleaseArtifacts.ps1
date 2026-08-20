[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackagePath,

    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $NumericVersion,

    [Parameter(Mandatory)]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory)]
    [long] $PublishedBytes,

    [string] $CommitSha,
    [switch] $Signed
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gitSafeDirectory = $repositoryRoot.Replace('\', '/')
$resolvedPackage = [System.IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $resolvedPackage -PathType Leaf)) {
    throw "Installer was not found at '$resolvedPackage'."
}

if ([string]::IsNullOrWhiteSpace($CommitSha)) {
    $CommitSha = (& git -c "safe.directory=$gitSafeDirectory" -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the release commit SHA.' }
}
if ($CommitSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Invalid commit SHA '$CommitSha'."
}

$package = Get-Item -LiteralPath $resolvedPackage
$hash = (Get-FileHash -LiteralPath $resolvedPackage -Algorithm SHA256).Hash.ToUpperInvariant()
$signature = Get-AuthenticodeSignature -FilePath $resolvedPackage
if ($Signed -and $signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw "The release was marked signed, but installer signature status is '$($signature.Status)'."
}

$baseName = [System.IO.Path]::GetFileNameWithoutExtension($package.Name)
$outputDirectory = $package.DirectoryName
$checksumPath = Join-Path $outputDirectory "$($package.Name).sha256"
$manifestPath = Join-Path $outputDirectory "$baseName-manifest.json"
$dependencyPath = Join-Path $outputDirectory "$baseName-dependencies.json"
$releaseNotesPath = Join-Path $outputDirectory "$baseName-release-notes.md"
$skiaNoticeRelativePath = 'THIRD-PARTY-LICENSES\SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt'
$skiaNoticePath = Join-Path $repositoryRoot $skiaNoticeRelativePath
if (-not (Test-Path -LiteralPath $skiaNoticePath -PathType Leaf)) {
    throw "Reviewed SkiaSharp distribution notice was not found at '$skiaNoticePath'."
}
$skiaNoticeHash = (Get-FileHash -LiteralPath $skiaNoticePath -Algorithm SHA256).Hash.ToUpperInvariant()
$nativePackageRelativePath = 'packages\OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng.3.119.4.2.nupkg'
$nativePackagePath = Join-Path $repositoryRoot $nativePackageRelativePath
$publishedNativePath = Join-Path $repositoryRoot "artifacts\publish\$RuntimeIdentifier\libSkiaSharp.dll"
$expectedNativeHash = 'A5A4C1EECE528A5BED7C98889435BD8214BBA610F963FE80E35256A91508B5DD'
foreach ($requiredNativePath in @($nativePackagePath, $publishedNativePath)) {
    if (-not (Test-Path -LiteralPath $requiredNativePath -PathType Leaf)) {
        throw "Reviewed DNG-free SkiaSharp artifact was not found at '$requiredNativePath'."
    }
}
$nativePackageHash = (Get-FileHash -LiteralPath $nativePackagePath -Algorithm SHA256).Hash.ToUpperInvariant()
$publishedNativeHash = (Get-FileHash -LiteralPath $publishedNativePath -Algorithm SHA256).Hash.ToUpperInvariant()
$publishedNativeSignature = Get-AuthenticodeSignature -LiteralPath $publishedNativePath
if ($publishedNativeHash -ne $expectedNativeHash -or
    $publishedNativeSignature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
    throw "Published DNG-free SkiaSharp native asset is not the reviewed unsigned binary: '$publishedNativeHash' / '$($publishedNativeSignature.Status)'."
}

$workflowRunId = if ($env:GITHUB_RUN_ID -match '^\d+$') { $env:GITHUB_RUN_ID } else { $null }
$workflowRepository = if ($env:GITHUB_REPOSITORY -match '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    $env:GITHUB_REPOSITORY
} else {
    $null
}
$workflow = if ($null -ne $workflowRunId -and $null -ne $workflowRepository) {
    [ordered]@{
        runId = $workflowRunId
        runUrl = "https://github.com/$workflowRepository/actions/runs/$workflowRunId"
    }
} else {
    $null
}

Set-Content -LiteralPath $checksumPath -Encoding Ascii -Value "$hash *$($package.Name)"

$manifest = [ordered]@{
    schemaVersion = 4
    product = 'OmniBrille'
    version = $Version
    fileVersion = $NumericVersion
    projectLicenseExpression = 'MIT'
    commitSha = $CommitSha.ToLowerInvariant()
    sourceUrl = "https://github.com/nishdel/OmniBrille/tree/$($CommitSha.ToLowerInvariant())"
    buildTimestampUtc = [DateTimeOffset]::UtcNow.ToString('O')
    runtimeIdentifier = $RuntimeIdentifier
    deployment = 'self-contained; non-trimmed; multi-file'
    explorerProtocol = [ordered]@{
        major = 1
        minor = 0
    }
    installer = [ordered]@{
        fileName = $package.Name
        bytes = $package.Length
        sha256 = $hash
        signed = [bool] $Signed
        signatureStatus = [string] $signature.Status
    }
    distributionNotices = [ordered]@{
        indexPath = 'THIRD-PARTY-NOTICES.txt'
        directory = 'THIRD-PARTY-LICENSES'
        skiaSharpNotice = [ordered]@{
            path = $skiaNoticeRelativePath.Replace('\', '/')
            sha256 = $skiaNoticeHash
        }
    }
    nativeComponents = [ordered]@{
        skiaSharp = [ordered]@{
            managedVersion = '3.119.4'
            nativePackageId = 'OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng'
            nativePackageVersion = '3.119.4.2'
            nativePackagePath = $nativePackageRelativePath.Replace('\', '/')
            nativePackageSha256 = $nativePackageHash
            nativeDllSha256 = $publishedNativeHash
            authenticodeStatus = [string] $publishedNativeSignature.Status
            upstreamCommit = 'f568ac94dd768ef9a2f593537cfde2dd0d348ef5'
            dngSdkIncluded = $false
            provenance = 'docs/native-skia.md'
        }
    }
    publishedRuntimeBytes = $PublishedBytes
    workflow = $workflow
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$desktopProject = Join-Path $repositoryRoot 'src\OmniBrille.Desktop\OmniBrille.Desktop.csproj'
$dependencyJson = & dotnet list $desktopProject package --include-transitive --format json
if ($LASTEXITCODE -ne 0) { throw 'Could not generate the runtime dependency manifest.' }
$dependencyReport = ($dependencyJson -join [Environment]::NewLine) | ConvertFrom-Json
$projects = foreach ($project in $dependencyReport.projects) {
    [ordered]@{
        name = [System.IO.Path]::GetFileNameWithoutExtension([string] $project.path)
        frameworks = @(
            foreach ($framework in $project.frameworks) {
                [ordered]@{
                    framework = [string] $framework.framework
                    topLevelPackages = @(
                        foreach ($item in $framework.topLevelPackages) {
                            [ordered]@{
                                id = [string] $item.id
                                requestedVersion = [string] $item.requestedVersion
                                resolvedVersion = [string] $item.resolvedVersion
                            }
                        }
                    )
                    transitivePackages = @(
                        foreach ($item in $framework.transitivePackages) {
                            [ordered]@{
                                id = [string] $item.id
                                resolvedVersion = [string] $item.resolvedVersion
                            }
                        }
                    )
                }
            }
        )
    }
}
$dependencies = [ordered]@{
    schemaVersion = 1
    product = 'OmniBrille'
    version = $Version
    projectLicenseExpression = 'MIT'
    scope = 'desktop project resolved dependency graph; may include runtime-identifier alternatives not present in the win-x64 publish output'
    format = 'OmniBrille dependency graph; not an exact packaged-file inventory, SPDX document, or CycloneDX SBOM'
    assetOverrides = @(
        [ordered]@{
            packageId = 'SkiaSharp.NativeAssets.Win32'
            version = '3.119.4'
            excludedAssets = 'all'
            contributesPackagedFiles = $false
            replacementPackageId = 'OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng'
            replacementPackageVersion = '3.119.4.2'
            reason = 'Suppress the transitive official DNG-bearing native runtime; the manifest binds the sole shipped win-x64 replacement DLL.'
        }
    )
    projects = @($projects)
}
$dependencies | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $dependencyPath -Encoding UTF8

$releaseTemplatePath = Join-Path $repositoryRoot 'docs\release-notes.md'
if (-not (Test-Path -LiteralPath $releaseTemplatePath -PathType Leaf)) {
    throw "Installer-facing release notes were not found at '$releaseTemplatePath'."
}
$signingDescription = if ($Signed) {
    'Authenticode signed; verify the publisher and Valid signature before installation.'
} else {
    'Unsigned release; Windows may show Unknown Publisher or SmartScreen reputation warnings.'
}
$workflowDescription = if ($null -ne $workflow) {
    "GitHub Actions run: $($workflow.runUrl)"
} else {
    'Build provenance: local package build; release-gate status is not recorded'
}
$releaseBody = Get-Content -Raw -LiteralPath $releaseTemplatePath
$releaseNotes = @"
# OmniBrille $Version

Installer: $($package.Name)
Commit: $($CommitSha.ToLowerInvariant())
SHA-256: $hash
Signing: $signingDescription
$workflowDescription

$releaseBody
"@
Set-Content -LiteralPath $releaseNotesPath -Encoding UTF8 -Value $releaseNotes

[pscustomobject]@{
    Checksum = $checksumPath
    Manifest = $manifestPath
    DependencyManifest = $dependencyPath
    ReleaseNotes = $releaseNotesPath
    Sha256 = $hash
    SignatureStatus = [string] $signature.Status
}
