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
    $matches = & git -C $repositoryRoot grep -n -I -E $stalePattern -- .
    $exitCode = $LASTEXITCODE
    if ($exitCode -notin 0,1) { throw 'Stale-brand audit could not be completed.' }
    if (@($matches).Count -gt 0) {
        throw "Stale product branding was found:`n$($matches -join [Environment]::NewLine)"
    }
}

function Assert-TrackedArtifactHygiene {
    $tracked = @(& git -C $repositoryRoot ls-files)
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

function Assert-VersionConsistency {
    [xml] $propertiesDocument = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props')
    $properties = $propertiesDocument.Project.PropertyGroup | Select-Object -First 1
    $semanticVersion = "$($properties.VersionPrefix)-$($properties.VersionSuffix)"
    if ($semanticVersion -notmatch '^\d+\.\d+\.\d+-(preview|beta|rc)\.\d+$') {
        throw "Version '$semanticVersion' is not a supported pre-release version."
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

    $dependencyManifest = Get-Content -Raw -LiteralPath $PackageResult.DependencyManifest | ConvertFrom-Json
    if ($dependencyManifest.product -ne 'OmniBrille' -or $dependencyManifest.version -ne $Version) {
        throw 'Runtime dependency manifest identity/version is inconsistent.'
    }

    if (-not (Test-Path -LiteralPath $PackageResult.PreviewNotes -PathType Leaf)) {
        throw 'Tester-facing private-preview notes were not generated.'
    }
    $previewNotes = Get-Content -Raw -LiteralPath $PackageResult.PreviewNotes
    foreach ($requiredValue in @($Version, [string] $manifest.commitSha, [string] $manifest.installer.fileName, $expectedHash)) {
        if ($previewNotes.IndexOf($requiredValue, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Tester-facing private-preview notes do not contain '$requiredValue'."
        }
    }
    if (-not $manifest.installer.signed -and
        $previewNotes.IndexOf('Unsigned private preview', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw 'Unsigned tester notes do not disclose the signing state.'
    }
}

Push-Location $repositoryRoot
try {
    if (-not $AllowDirty) {
        $status = @(& git status --porcelain --untracked-files=all)
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

    Invoke-Checked git @('diff', '--check')
    Assert-TrackedArtifactHygiene

    $packagePath = $null
    $manifestPath = $null
    $checksumPath = $null
    $dependencyManifestPath = $null
    $previewNotesPath = $null
    $signed = $false
    if ($null -ne $packageResult) {
        $packagePath = $packageResult.Package
        $manifestPath = $packageResult.Manifest
        $checksumPath = $packageResult.Checksum
        $dependencyManifestPath = $packageResult.DependencyManifest
        $previewNotesPath = $packageResult.PreviewNotes
        $signed = [bool] $packageResult.Signed
    }
    [pscustomobject]@{
        Ready = $true
        Version = $version
        Commit = (& git rev-parse HEAD).Trim()
        Package = $packagePath
        Manifest = $manifestPath
        Checksum = $checksumPath
        DependencyManifest = $dependencyManifestPath
        PreviewNotes = $previewNotesPath
        Signed = $signed
    }
}
finally {
    Pop-Location
}
