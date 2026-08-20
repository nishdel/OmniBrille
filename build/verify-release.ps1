[CmdletBinding()]
param(
    [switch] $AllowDirty,
    [switch] $SkipPackage,
    [switch] $RequireSigning,
    [string] $SigningCertificateThumbprint = $env:OMNIBRILLE_SIGNING_CERTIFICATE_THUMBPRINT,
    [string] $InnoCompiler
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gitSafeDirectory = $repositoryRoot.Replace('\', '/')
$solution = Join-Path $repositoryRoot 'OmniBrille.sln'

function Invoke-Checked {
    param(
        [Parameter(Mandatory)][string] $Command,
        [Parameter(Mandatory)][string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command $($Arguments -join ' ')"
    }
}

function Assert-NoStaleBranding {
    $stalePattern = ("Omni" + "Explorer") + '|' + ("Omni" + "Nav")
    $matches = & git -c "safe.directory=$gitSafeDirectory" -C $repositoryRoot grep -n -I -E $stalePattern -- .
    $exitCode = $LASTEXITCODE
    if ($exitCode -notin 0,1) { throw 'Stale-brand audit could not be completed.' }
    if (@($matches).Count -gt 0) {
        throw "Stale product branding was found:`n$($matches -join [Environment]::NewLine)"
    }
}

function Assert-TrackedArtifactHygiene {
    $tracked = @(& git -c "safe.directory=$gitSafeDirectory" -C $repositoryRoot ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'Tracked-file audit could not be completed.' }
    $forbidden = @(
        $tracked | Where-Object {
            $_ -match '(^|/)(bin|obj|artifacts|TestResults|\.vs)/' -or
            $_ -match '\.(pdb|pfx|snk|cer|key|log|db|sqlite|wav|mp3|flac|m4a|ogg|wma)$' -or
            $_ -match '(?i)(^|/)ggml-[^/]+\.bin$'
        }
    )
    if ($forbidden.Count -gt 0) {
        throw "Forbidden release/development artifacts are tracked:`n$($forbidden -join [Environment]::NewLine)"
    }
}

function Assert-DistributionLicense {
    $licensePath = Join-Path $repositoryRoot 'LICENSE'
    if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf) -or
        (Get-Item -LiteralPath $licensePath).Length -eq 0) {
        throw 'Public release verification requires a maintainer-approved, non-empty LICENSE file.'
    }
    [xml] $propertiesDocument = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props')
    $licenseExpression = [string] ($propertiesDocument.Project.PropertyGroup | Select-Object -First 1).PackageLicenseExpression
    if ($licenseExpression -ne 'MIT') {
        throw "Public release metadata must identify the owner-selected MIT license; found '$licenseExpression'."
    }
    $licenseText = Get-Content -Raw -LiteralPath $licensePath
    foreach ($requiredText in @(
        'MIT License',
        'Copyright (c) 2026 OmniBrille Contributors',
        'Permission is hereby granted, free of charge',
        'The above copyright notice and this permission notice shall be included',
        'THE SOFTWARE IS PROVIDED "AS IS"'
    )) {
        if ($licenseText.IndexOf($requiredText, [StringComparison]::Ordinal) -lt 0) {
            throw "Root LICENSE does not contain the expected MIT text: '$requiredText'."
        }
    }

    $skiaNoticesPath = Join-Path $repositoryRoot 'THIRD-PARTY-LICENSES\SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt'
    $skiaNotices = Get-Content -Raw -LiteralPath $skiaNoticesPath
    $expectedSkiaNoticeHash = 'D865C31394CD46C76DDBA4405E96650D3EFA6066C553BD9BCF60D48B4DD6880B'
    if ((Get-FileHash -LiteralPath $skiaNoticesPath -Algorithm SHA256).Hash -ne $expectedSkiaNoticeHash -or
        $skiaNotices.IndexOf('OMNIBRILLE DNG-FREE SKIASHARP 3.119.4 NOTICE', [StringComparison]::Ordinal) -lt 0) {
        throw 'The derived DNG-free SkiaSharp notice does not match the reviewed bytes and provenance header.'
    }
    foreach ($forbiddenDngNotice in @('DNG SDK License Agreement', '# DNG SDK', '# piex', 'external/dng_sdk', 'external/piex')) {
        if ($skiaNotices.IndexOf($forbiddenDngNotice, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "The DNG-free SkiaSharp notice still claims excluded component '$forbiddenDngNotice'."
        }
    }

    $nativePackagePath = Join-Path $repositoryRoot 'packages\OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng.3.119.4.2.nupkg'
    $expectedNativePackageHash = '880578F572F541A58C47418AABF881D5F0665A4B1A48AD26753549D01EDDC10C'
    if (-not (Test-Path -LiteralPath $nativePackagePath -PathType Leaf) -or
        (Get-FileHash -LiteralPath $nativePackagePath -Algorithm SHA256).Hash -ne $expectedNativePackageHash) {
        throw 'The tracked DNG-free SkiaSharp package is missing or differs from the reviewed package bytes.'
    }
}

function Assert-VersionConsistency {
    [xml] $propertiesDocument = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props')
    $properties = $propertiesDocument.Project.PropertyGroup | Select-Object -First 1
    $semanticVersion = [string] $properties.VersionPrefix
    if (-not [string]::IsNullOrWhiteSpace([string] $properties.VersionSuffix)) {
        $semanticVersion += "-$($properties.VersionSuffix)"
    }
    if ($semanticVersion -notmatch '^\d+\.\d+\.\d+(?:-(preview|beta|rc)\.\d+)?$') {
        throw "Version '$semanticVersion' is not a supported stable or pre-release version."
    }
    if ([string] $properties.FileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "FileVersion '$($properties.FileVersion)' is invalid."
    }
    $installer = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'installer\OmniBrille.iss')
    if ($installer -notmatch [regex]::Escape("#define AppVersion `"$semanticVersion`"")) {
        throw 'Installer semantic version fallback does not match Directory.Build.props.'
    }
    if ($installer -notmatch [regex]::Escape("#define NumericVersion `"$($properties.FileVersion)`"")) {
        throw 'Installer numeric version fallback does not match Directory.Build.props.'
    }
    return $semanticVersion
}

function Assert-PackagedContents {
    param(
        [Parameter(Mandatory)][string] $Version,
        [Parameter(Mandatory)][psobject] $PackageResult
    )

    $publishDirectory = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
    foreach ($requiredPath in @(
        'LICENSE',
        'THIRD-PARTY-NOTICES.txt',
        'THIRD-PARTY-LICENSES\Avalonia-LICENSE.txt',
        'THIRD-PARTY-LICENSES\ANGLE-LICENSE.txt',
        'THIRD-PARTY-LICENSES\DotNet-LICENSE.txt',
        'THIRD-PARTY-LICENSES\MicroCom-LICENSE.txt',
        'THIRD-PARTY-LICENSES\Inter-OFL-1.1.txt',
        'THIRD-PARTY-LICENSES\Tmds.DBus-LICENSE.txt',
        'THIRD-PARTY-LICENSES\SkiaSharp-HarfBuzz-LICENSE.txt',
        'THIRD-PARTY-LICENSES\SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt',
        'THIRD-PARTY-LICENSES\DotNet-Runtime-THIRD-PARTY-NOTICES.txt',
        'THIRD-PARTY-LICENSES\System.IO.Pipelines-THIRD-PARTY-NOTICES.txt'
    )) {
        $sourcePath = Join-Path $repositoryRoot $requiredPath
        $packagedPath = Join-Path $publishDirectory $requiredPath
        if (-not (Test-Path -LiteralPath $packagedPath -PathType Leaf)) {
            throw "Required distribution notice '$requiredPath' was not packaged."
        }
        if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $packagedPath -Algorithm SHA256).Hash) {
            throw "Packaged distribution notice '$requiredPath' does not match its reviewed repository source."
        }
    }
    $forbiddenFiles = @(
        Get-ChildItem -LiteralPath $publishDirectory -Recurse -File | Where-Object {
            $_.Extension -in '.pdb','.cs','.csproj','.sln','.user','.log','.db','.sqlite','.pfx','.snk','.key' -or
            $_.Extension -in '.wav','.mp3','.flac','.m4a','.ogg','.wma' -or
            $_.Name -match '(?i)(testhost|OmniBrille\.(Tests|HeadlessTests)|fixture|screenshot|whisper-cli|ggml-.*\.bin)'
        }
    )
    if ($forbiddenFiles.Count -gt 0) {
        throw "Forbidden files were found in the published runtime: $($forbiddenFiles.Name -join ', ')"
    }

    $unexpectedCompanionBinaries = @(
        Get-ChildItem -LiteralPath $publishDirectory -Filter 'OmniSorSe*.dll' -File |
            Where-Object { $_.Name -ne 'OmniSorSe.ExplorerProtocol.dll' }
    )
    if ($unexpectedCompanionBinaries.Count -gt 0) {
        throw "Unexpected OmniSorSe binaries were packaged: $($unexpectedCompanionBinaries.Name -join ', ')"
    }

    $publishedSkiaBinaries = @(Get-ChildItem -LiteralPath $publishDirectory -Recurse -Filter 'libSkiaSharp.dll' -File)
    if ($publishedSkiaBinaries.Count -ne 1) {
        throw "Expected exactly one published libSkiaSharp.dll; found $($publishedSkiaBinaries.Count)."
    }
    $expectedNativeHash = 'A5A4C1EECE528A5BED7C98889435BD8214BBA610F963FE80E35256A91508B5DD'
    $publishedSkia = $publishedSkiaBinaries[0]
    if ((Get-FileHash -LiteralPath $publishedSkia.FullName -Algorithm SHA256).Hash -ne $expectedNativeHash -or
        (Get-AuthenticodeSignature -LiteralPath $publishedSkia.FullName).Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
        throw 'Published libSkiaSharp.dll is not the reviewed project-built DNG-free unsigned binary.'
    }
    $publishedSkiaText = [System.Text.Encoding]::Latin1.GetString([System.IO.File]::ReadAllBytes($publishedSkia.FullName))
    foreach ($forbiddenNativeMarker in @('dng_pixel_buffer', 'dng_negative', 'dng_priority_manager', 'dng_sdk', 'DNG SDK', 'SkRawCodec', 'SkDngHost', 'SkDngImage', '.?AVdng_')) {
        if ($publishedSkiaText.IndexOf($forbiddenNativeMarker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Published libSkiaSharp.dll contains excluded DNG/raw marker '$forbiddenNativeMarker'."
        }
    }

    $profilePath = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    $textFiles = Get-ChildItem -LiteralPath $publishDirectory -Recurse -File |
        Where-Object { $_.Extension -in '.json','.config','.xml','.txt' }
    foreach ($file in $textFiles) {
        $content = Get-Content -Raw -LiteralPath $file.FullName
        if ($content.IndexOf($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            (-not [string]::IsNullOrWhiteSpace($profilePath) -and
             $content.IndexOf($profilePath, [StringComparison]::OrdinalIgnoreCase) -ge 0)) {
            throw "Developer-machine path leaked into '$($file.Name)'."
        }
    }

    $manifest = Get-Content -Raw -LiteralPath $PackageResult.Manifest | ConvertFrom-Json
    if ($manifest.product -ne 'OmniBrille' -or $manifest.version -ne $Version) {
        throw 'Release manifest identity/version is inconsistent.'
    }
    if ($manifest.schemaVersion -ne 4 -or
        $manifest.projectLicenseExpression -ne 'MIT' -or
        $manifest.sourceUrl -ne "https://github.com/nishdel/OmniBrille/tree/$($manifest.commitSha)") {
        throw 'Release manifest schema, MIT project license, or source metadata is invalid.'
    }
    $expectedSkiaNoticePath = 'THIRD-PARTY-LICENSES/SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt'
    $expectedSkiaNoticeHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot ($expectedSkiaNoticePath.Replace('/', '\'))) -Algorithm SHA256).Hash
    if ($manifest.distributionNotices.skiaSharpNotice.path -ne $expectedSkiaNoticePath -or
        $manifest.distributionNotices.skiaSharpNotice.sha256 -ne $expectedSkiaNoticeHash) {
        throw 'Release manifest does not bind the reviewed DNG-free SkiaSharp notice path and bytes.'
    }
    $expectedNativePackageHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'packages\OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng.3.119.4.2.nupkg') -Algorithm SHA256).Hash
    if ($manifest.nativeComponents.skiaSharp.nativePackageId -ne 'OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng' -or
        $manifest.nativeComponents.skiaSharp.nativePackageVersion -ne '3.119.4.2' -or
        $manifest.nativeComponents.skiaSharp.nativePackageSha256 -ne $expectedNativePackageHash -or
        $manifest.nativeComponents.skiaSharp.nativeDllSha256 -ne $expectedNativeHash -or
        $manifest.nativeComponents.skiaSharp.authenticodeStatus -ne 'NotSigned' -or
        $manifest.nativeComponents.skiaSharp.dngSdkIncluded) {
        throw 'Release manifest does not bind the reviewed DNG-free SkiaSharp package, DLL, signature state, and exclusion result.'
    }
    if ($manifest.explorerProtocol.major -ne 1 -or $manifest.explorerProtocol.minor -ne 0) {
        throw 'Release manifest protocol compatibility is inconsistent.'
    }
    $expectedHash = (Get-FileHash -LiteralPath $PackageResult.Package -Algorithm SHA256).Hash
    if ($manifest.installer.sha256 -ne $expectedHash) {
        throw 'Release manifest checksum does not match the installer.'
    }
    $checksum = (Get-Content -Raw -LiteralPath $PackageResult.Checksum).Trim()
    if (-not $checksum.StartsWith($expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'SHA-256 sidecar does not match the installer.'
    }
    if ($RequireSigning -and -not $manifest.installer.signed) {
        throw 'Release signing was required, but the manifest describes an unsigned installer.'
    }
    if (-not $manifest.installer.signed -and
        ($manifest.installer.signatureStatus -ne 'NotSigned' -or
         (Get-AuthenticodeSignature -LiteralPath $PackageResult.Package).Status -ne [System.Management.Automation.SignatureStatus]::NotSigned)) {
        throw 'Unsigned release metadata and installer must both report NotSigned.'
    }

    $dependencyManifest = Get-Content -Raw -LiteralPath $PackageResult.DependencyManifest | ConvertFrom-Json
    if ($dependencyManifest.product -ne 'OmniBrille' -or
        $dependencyManifest.version -ne $Version -or
        $dependencyManifest.projectLicenseExpression -ne 'MIT') {
        throw 'Runtime dependency manifest identity/version is inconsistent.'
    }
    $assetOverrides = @($dependencyManifest.assetOverrides)
    if ($assetOverrides.Count -ne 1 -or
        $assetOverrides[0].packageId -ne 'SkiaSharp.NativeAssets.Win32' -or
        $assetOverrides[0].version -ne '3.119.4' -or
        $assetOverrides[0].excludedAssets -ne 'all' -or
        $assetOverrides[0].contributesPackagedFiles -ne $false -or
        $assetOverrides[0].replacementPackageId -ne 'OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng' -or
        $assetOverrides[0].replacementPackageVersion -ne '3.119.4.2') {
        throw 'Runtime dependency manifest does not describe the fail-closed official-native exclusion and DNG-free replacement.'
    }

    if (-not (Test-Path -LiteralPath $PackageResult.ReleaseNotes -PathType Leaf)) {
        throw 'Installer-facing release notes were not generated.'
    }
    $releaseNotes = Get-Content -Raw -LiteralPath $PackageResult.ReleaseNotes
    foreach ($requiredValue in @($Version, [string] $manifest.commitSha, [string] $manifest.installer.fileName, $expectedHash)) {
        if ($releaseNotes.IndexOf($requiredValue, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Installer-facing release notes do not contain '$requiredValue'."
        }
    }
    if (-not $manifest.installer.signed -and
        $releaseNotes.IndexOf('Unsigned release', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw 'Unsigned release notes do not disclose the signing state.'
    }
    if ($releaseNotes.IndexOf('MIT License', [StringComparison]::Ordinal) -lt 0 -or
        $releaseNotes.IndexOf('DNG/RAW codec excluded', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw 'Release notes do not describe the MIT project license and project-built DNG-free SkiaSharp native asset.'
    }
}

Push-Location $repositoryRoot
try {
    if (-not $AllowDirty) {
        $status = @(& git -c "safe.directory=$gitSafeDirectory" -C $repositoryRoot status --porcelain --untracked-files=all)
        if ($LASTEXITCODE -ne 0) { throw 'Git status could not be inspected.' }
        if ($status.Count -gt 0) {
            throw "Release verification requires a clean checkout:`n$($status -join [Environment]::NewLine)"
        }
    }

    if ($RequireSigning -and [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
        throw 'Signing was required, but OMNIBRILLE_SIGNING_CERTIFICATE_THUMBPRINT was not supplied.'
    }

    $version = Assert-VersionConsistency
    Assert-NoStaleBranding
    Assert-TrackedArtifactHygiene
    Assert-DistributionLicense
    & (Join-Path $PSScriptRoot 'Test-EngineeringDocs.ps1') -RepositoryRoot $repositoryRoot

    Invoke-Checked dotnet @('restore', $solution)
    Invoke-Checked dotnet @('format', $solution, '--verify-no-changes', '--no-restore')
    Invoke-Checked dotnet @('build', $solution, '--configuration', 'Release', '--no-restore')
    Invoke-Checked dotnet @('test', $solution, '--configuration', 'Release', '--no-build', '--no-restore', '--logger', 'console;verbosity=minimal')
    & (Join-Path $PSScriptRoot 'Test-NuGetVulnerabilities.ps1') -Solution $solution

    Write-Host 'Direct dependency update review (informational only):'
    Invoke-Checked dotnet @('list', $solution, 'package', '--outdated')

    $packageResult = $null
    if (-not $SkipPackage) {
        if ($env:OS -ne 'Windows_NT') {
            throw 'Windows packaging verification must run on Windows, or use -SkipPackage.'
        }
        $packageArguments = @{
            BootstrapInnoSetup = $true
            RequireSigning = $RequireSigning
            SigningCertificateThumbprint = $SigningCertificateThumbprint
        }
        if (-not [string]::IsNullOrWhiteSpace($InnoCompiler)) {
            $packageArguments.InnoCompiler = $InnoCompiler
            $packageArguments.BootstrapInnoSetup = $false
        }
        $packageOutput = & (Join-Path $PSScriptRoot 'Package-Windows.ps1') @packageArguments
        $packageResult = $packageOutput |
            Where-Object { $_ -is [psobject] -and $null -ne $_.PSObject.Properties['Package'] } |
            Select-Object -Last 1
        if ($null -eq $packageResult) { throw 'Packaging did not return release artifact metadata.' }
        Assert-PackagedContents -Version $version -PackageResult $packageResult
    }

    Invoke-Checked git @('-c', "safe.directory=$gitSafeDirectory", '-C', $repositoryRoot, 'diff', '--check')
    Assert-TrackedArtifactHygiene

    $packagePath = $null
    $manifestPath = $null
    $checksumPath = $null
    $dependencyManifestPath = $null
    $releaseNotesPath = $null
    $signed = $false
    if ($null -ne $packageResult) {
        $packagePath = $packageResult.Package
        $manifestPath = $packageResult.Manifest
        $checksumPath = $packageResult.Checksum
        $dependencyManifestPath = $packageResult.DependencyManifest
        $releaseNotesPath = $packageResult.ReleaseNotes
        $signed = [bool] $packageResult.Signed
    }
    [pscustomobject]@{
        Ready = $true
        Version = $version
        Commit = (& git -c "safe.directory=$gitSafeDirectory" -C $repositoryRoot rev-parse HEAD).Trim()
        Package = $packagePath
        Manifest = $manifestPath
        Checksum = $checksumPath
        DependencyManifest = $dependencyManifestPath
        ReleaseNotes = $releaseNotesPath
        Signed = $signed
    }
}
finally {
    Pop-Location
}
