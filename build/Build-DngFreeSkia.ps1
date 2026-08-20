[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OfficialReferenceDll,

    [string] $OutputDirectory,
    [string] $WorkDirectory,
    [string] $VisualStudioInstall,
    [string] $LlvmHome = 'C:\Program Files\LLVM',
    [string] $Python = 'python'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$skiaSharpRepository = 'https://github.com/mono/SkiaSharp.git'
$skiaSharpVersion = '3.119.4'
$skiaSharpCommit = 'f568ac94dd768ef9a2f593537cfde2dd0d348ef5'
$skiaCommit = '7dbfc07dd33181f84e0958afb7ee805c6c769f0b'
$depotToolsCommit = '8fecc592a290769242d5098666cee8d29b7f0523'
$dngRevision = 'c8d0c9b1d16bfda56f15165d39e0ffa360a11123'
$piexRevision = 'bb217acdca1cc0c16b704669dd6f91a1b509c406'
$expectedDotnetSdk = '10.0.105'
$expectedLlvmVersion = '19.1.1'
$expectedCakeVersion = '4.0.0'
$expectedOfficialSha256 = '7DEC3BA900AB353491E6446F0083739924C6F8DD668832E2F09D38EBFFDBBE1C'
$variant = 'omnibrille-no-dng'
$architecture = 'x64'
$configuration = 'Release'

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw 'The pinned SkiaSharp Windows native build must run on Windows.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\native-skia\$skiaSharpVersion-$timestamp"
}
if ([string]::IsNullOrWhiteSpace($WorkDirectory)) {
    $WorkDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "omnibrille-skia-$skiaSharpVersion-$timestamp"
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$workRoot = [System.IO.Path]::GetFullPath($WorkDirectory)
$checkoutRoot = Join-Path $workRoot 'SkiaSharp'
$buildLog = Join-Path $outputRoot 'build.log'

foreach ($newDirectory in @($outputRoot, $workRoot)) {
    if (Test-Path -LiteralPath $newDirectory) {
        throw "Refusing to reuse existing directory '$newDirectory'. Supply a new empty path."
    }
    New-Item -ItemType Directory -Path $newDirectory | Out-Null
}

function Invoke-Logged {
    param(
        [Parameter(Mandatory)]
        [string] $FilePath,

        [Parameter(Mandatory)]
        [string[]] $ArgumentList,

        [string] $WorkingDirectory = $checkoutRoot
    )

    Push-Location $WorkingDirectory
    try {
        "`n> $FilePath $($ArgumentList -join ' ')" | Tee-Object -FilePath $buildLog -Append
        & $FilePath @ArgumentList 2>&1 | Tee-Object -FilePath $buildLog -Append
        if ($LASTEXITCODE -ne 0) {
            throw "Command '$FilePath' failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-Captured {
    param(
        [Parameter(Mandatory)]
        [string] $FilePath,

        [Parameter(Mandatory)]
        [string[]] $ArgumentList,

        [string] $WorkingDirectory = $checkoutRoot
    )

    Push-Location $WorkingDirectory
    try {
        $commandOutput = @(& $FilePath @ArgumentList 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "Command '$FilePath' failed with exit code $LASTEXITCODE.`n$($commandOutput -join [Environment]::NewLine)"
        }
        return @($commandOutput | ForEach-Object { [string] $_ })
    }
    finally {
        Pop-Location
    }
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-NormalizedExports {
    param(
        [Parameter(Mandatory)][string] $Dumpbin,
        [Parameter(Mandatory)][string] $Dll
    )

    $dump = Invoke-Captured -FilePath $Dumpbin -ArgumentList @('/nologo', '/exports', $Dll)
    $names = foreach ($line in $dump) {
        if ($line -match '^\s+\d+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+(\S+)') {
            $Matches[1]
        }
    }
    $normalized = @($names | Sort-Object -Unique)
    if ($normalized.Count -eq 0) {
        throw "No exports could be parsed from '$Dll'."
    }
    return $normalized
}

function Assert-NoBinaryMarkers {
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string[]] $Markers
    )

    $binaryText = [System.Text.Encoding]::Latin1.GetString([System.IO.File]::ReadAllBytes($Path))
    $found = @($Markers | Where-Object { $binaryText.IndexOf($_, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($found.Count -gt 0) {
        throw "DNG-free binary verification failed. '$Path' contains: $($found -join ', ')."
    }
}

'' | Set-Content -LiteralPath $buildLog -Encoding utf8

$officialReference = [System.IO.Path]::GetFullPath($OfficialReferenceDll)
if (-not (Test-Path -LiteralPath $officialReference -PathType Leaf)) {
    throw "Official SkiaSharp reference DLL was not found at '$officialReference'."
}
$officialReferenceHash = Get-Sha256 $officialReference
if ($officialReferenceHash -ne $expectedOfficialSha256) {
    throw "Official reference hash was '$officialReferenceHash'; expected '$expectedOfficialSha256' for SkiaSharp.NativeAssets.Win32 3.119.4 win-x64."
}

$gitCommand = (Get-Command git -ErrorAction Stop).Source
$dotnetCommand = (Get-Command dotnet -ErrorAction Stop).Source
$pythonCommand = (Get-Command $Python -ErrorAction Stop).Source

if ([string]::IsNullOrWhiteSpace($VisualStudioInstall)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        throw 'Visual Studio was not supplied and vswhere.exe was not found.'
    }
    $VisualStudioInstall = (& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($VisualStudioInstall)) {
        throw 'Could not resolve a Visual Studio C++ installation.'
    }
}
$VisualStudioInstall = [System.IO.Path]::GetFullPath($VisualStudioInstall)
if (-not (Test-Path -LiteralPath $VisualStudioInstall -PathType Container)) {
    throw "Visual Studio installation was not found at '$VisualStudioInstall'."
}

$dumpbinCandidates = @(Get-ChildItem -LiteralPath (Join-Path $VisualStudioInstall 'VC\Tools\MSVC') -Recurse -Filter dumpbin.exe -File |
        Where-Object { $_.FullName -match '\\bin\\Hostx64\\x64\\dumpbin\.exe$' } |
        Sort-Object FullName -Descending)
if ($dumpbinCandidates.Count -eq 0) {
    throw 'dumpbin.exe for Hostx64/x64 was not found in the selected Visual Studio installation.'
}
$dumpbinCommand = $dumpbinCandidates[0].FullName

$spectreDirectories = @(Get-ChildItem -Path (Join-Path $VisualStudioInstall 'VC\Tools\MSVC\*\lib\spectre\x64') -Directory -ErrorAction SilentlyContinue |
        Sort-Object FullName)
if ($spectreDirectories.Count -eq 0) {
    throw 'SkiaSharp v3.119.4 requires an x64 MSVC Spectre library, but none was found.'
}

$clangCommand = Join-Path ([System.IO.Path]::GetFullPath($LlvmHome)) 'bin\clang.exe'
if (-not (Test-Path -LiteralPath $clangCommand -PathType Leaf)) {
    throw "LLVM clang was not found at '$clangCommand'. Use the upstream v3.119.4 scripts/install-llvm.ps1 provisioner."
}
$clangVersion = @(& $clangCommand --version 2>&1 | ForEach-Object { [string] $_ })
if ($LASTEXITCODE -ne 0 -or ($clangVersion -join "`n") -notmatch "clang version $([regex]::Escape($expectedLlvmVersion))(?:\s|$)") {
    throw "Expected LLVM $expectedLlvmVersion. Actual output:`n$($clangVersion -join [Environment]::NewLine)"
}

Invoke-Logged -FilePath $gitCommand -ArgumentList @('init', $checkoutRoot) -WorkingDirectory $workRoot
Invoke-Logged -FilePath $gitCommand -ArgumentList @('-C', $checkoutRoot, 'config', 'core.longpaths', 'true') -WorkingDirectory $workRoot
Invoke-Logged -FilePath $gitCommand -ArgumentList @('-C', $checkoutRoot, 'remote', 'add', 'origin', $skiaSharpRepository) -WorkingDirectory $workRoot
Invoke-Logged -FilePath $gitCommand -ArgumentList @('-C', $checkoutRoot, 'fetch', '--depth', '1', 'origin', $skiaSharpCommit) -WorkingDirectory $workRoot
Invoke-Logged -FilePath $gitCommand -ArgumentList @('-C', $checkoutRoot, 'checkout', '--detach', $skiaSharpCommit) -WorkingDirectory $workRoot
Invoke-Logged -FilePath $gitCommand -ArgumentList @('-C', $checkoutRoot, 'submodule', 'sync', '--recursive') -WorkingDirectory $workRoot
Invoke-Logged -FilePath $gitCommand -ArgumentList @(
    '-C', $checkoutRoot, 'submodule', 'update', '--init', 'externals/skia', 'externals/depot_tools'
) -WorkingDirectory $workRoot

$actualSkiaSharpCommit = (Invoke-Captured $gitCommand @('rev-parse', 'HEAD'))[0].Trim()
$skiaRoot = Join-Path $checkoutRoot 'externals\skia'
$depotToolsRoot = Join-Path $checkoutRoot 'externals\depot_tools'
$actualSkiaCommit = (Invoke-Captured $gitCommand @('-C', $skiaRoot, 'rev-parse', 'HEAD'))[0].Trim()
$actualDepotToolsCommit = (Invoke-Captured $gitCommand @('-C', $depotToolsRoot, 'rev-parse', 'HEAD'))[0].Trim()
if ($actualSkiaSharpCommit -ne $skiaSharpCommit -or
    $actualSkiaCommit -ne $skiaCommit -or
    $actualDepotToolsCommit -ne $depotToolsCommit) {
    throw "Pinned source mismatch. SkiaSharp='$actualSkiaSharpCommit'; Skia='$actualSkiaCommit'; depot_tools='$actualDepotToolsCommit'."
}
$initialStatus = @(Invoke-Captured $gitCommand @('status', '--short'))
if ($initialStatus.Count -ne 0) {
    throw "Fresh SkiaSharp checkout was unexpectedly dirty:`n$($initialStatus -join [Environment]::NewLine)"
}

$dotnetVersion = (Invoke-Captured $dotnetCommand @('--version'))[0].Trim()
if ($dotnetVersion -ne $expectedDotnetSdk) {
    throw "SkiaSharp native build requires the pinned .NET SDK '$expectedDotnetSdk'; found '$dotnetVersion'."
}
$pythonVersion = @(Invoke-Captured $pythonCommand @('--version'))
$dotnetInfo = @(Invoke-Captured $dotnetCommand @('--info'))
$dotnetInfo | Set-Content -LiteralPath (Join-Path $outputRoot 'dotnet-info.txt') -Encoding utf8
$clangVersion | Set-Content -LiteralPath (Join-Path $outputRoot 'clang-version.txt') -Encoding utf8

$toolManifestPath = Join-Path $checkoutRoot '.config\dotnet-tools.json'
$toolManifest = Get-Content -Raw -LiteralPath $toolManifestPath | ConvertFrom-Json
$cakeVersion = [string] $toolManifest.tools.'cake.tool'.version
if ($cakeVersion -ne $expectedCakeVersion) {
    throw "Pinned Cake version changed from '$expectedCakeVersion' to '$cakeVersion'."
}

$depsPath = Join-Path $skiaRoot 'DEPS'
$depsText = [System.IO.File]::ReadAllText($depsPath)
$originalDepsHash = Get-Sha256 $depsPath
$escapedDngUrl = [regex]::Escape("https://android.googlesource.com/platform/external/dng_sdk.git@$dngRevision")
$dngEntryPattern = "(?m)^[\t ]*`"third_party/externals/dng_sdk`"[\t ]*:[\t ]*`"$escapedDngUrl`",[\t ]*\r?\n"
$dngEntries = [regex]::Matches($depsText, $dngEntryPattern)
if ($dngEntries.Count -ne 1) {
    throw "Expected exactly one pinned Adobe DNG DEPS entry; found $($dngEntries.Count). Upstream source must be reviewed before rebuilding."
}
$patchedDeps = [regex]::Replace($depsText, $dngEntryPattern, '', 1)
$escapedPiexUrl = [regex]::Escape("https://android.googlesource.com/platform/external/piex.git@$piexRevision")
$piexEntryPattern = "(?m)^[\t ]*`"third_party/externals/piex`"[\t ]*:[\t ]*`"$escapedPiexUrl`",[\t ]*\r?\n"
$piexEntries = [regex]::Matches($patchedDeps, $piexEntryPattern)
if ($piexEntries.Count -ne 1) {
    throw "Expected exactly one pinned PIEX DEPS entry; found $($piexEntries.Count). Upstream source must be reviewed before rebuilding."
}
$patchedDeps = [regex]::Replace($patchedDeps, $piexEntryPattern, '', 1)
if ($patchedDeps -match 'third_party/externals/(?:dng_sdk|piex)|external/(?:dng_sdk|piex)') {
    throw 'DNG or its RAW-only PIEX dependency still appears in the patched DEPS file.'
}
[System.IO.File]::WriteAllText($depsPath, $patchedDeps, [System.Text.UTF8Encoding]::new($false))
$patchedDepsHash = Get-Sha256 $depsPath

$depsPatch = @(Invoke-Captured $gitCommand @('-C', $skiaRoot, 'diff', '--', 'DEPS'))
$depsPatch | Set-Content -LiteralPath (Join-Path $outputRoot 'skia-deps-dng-removal.patch') -Encoding utf8
$changedSkiaFiles = @(Invoke-Captured $gitCommand @('-C', $skiaRoot, 'diff', '--name-only'))
if ($changedSkiaFiles.Count -ne 1 -or $changedSkiaFiles[0].Trim() -ne 'DEPS') {
    throw "The source preparation changed files other than the pinned DEPS entry: $($changedSkiaFiles -join ', ')."
}
Invoke-Logged -FilePath $gitCommand -ArgumentList @('-C', $skiaRoot, 'diff', '--check')

Invoke-Logged -FilePath $dotnetCommand -ArgumentList @('tool', 'restore')
Invoke-Logged -FilePath $dotnetCommand -ArgumentList @(
    'cake',
    '--target=externals-windows',
    "--configuration=$configuration",
    "--arch=$architecture",
    "--variant=$variant",
    '--gnArgs=skia_use_dng_sdk=false',
    "--vsinstall=$VisualStudioInstall",
    "--llvm=$LlvmHome",
    "--python=$pythonCommand"
)

$externalDngSource = Join-Path $skiaRoot 'third_party\externals\dng_sdk'
$externalPiexSource = Join-Path $skiaRoot 'third_party\externals\piex'
if ((Test-Path -LiteralPath $externalDngSource) -or (Test-Path -LiteralPath $externalPiexSource)) {
    throw 'DNG or its RAW-only PIEX dependency was fetched despite the fail-closed DEPS removal.'
}

$gnCommand = Join-Path $skiaRoot 'bin\gn.exe'
$gnOutput = Join-Path $skiaRoot "out\$variant\x64"
if (-not (Test-Path -LiteralPath $gnCommand -PathType Leaf) -or -not (Test-Path -LiteralPath $gnOutput -PathType Container)) {
    throw 'The expected GN executable or generated x64 output directory was not produced.'
}

$dngArgument = @(Invoke-Captured $gnCommand @('args', $gnOutput, '--list=skia_use_dng_sdk', '--short') $skiaRoot)
if (($dngArgument -join "`n") -notmatch '(?m)^skia_use_dng_sdk\s*=\s*false\s*$') {
    throw "GN did not evaluate skia_use_dng_sdk=false:`n$($dngArgument -join [Environment]::NewLine)"
}
$evaluatedArguments = @(Invoke-Captured $gnCommand @('args', $gnOutput, '--list', '--short') $skiaRoot)
$evaluatedArguments | Set-Content -LiteralPath (Join-Path $outputRoot 'evaluated-gn-args.txt') -Encoding utf8

$gnDependencies = @(Invoke-Captured $gnCommand @('desc', $gnOutput, '//:SkiaSharp', 'deps', '--all', '--tree') $skiaRoot)
$gnDependencies | Set-Content -LiteralPath (Join-Path $outputRoot 'gn-dependencies.txt') -Encoding utf8
if (($gnDependencies -join "`n") -match '(?i)dng_sdk|SkRawCodec|third_party/piex') {
    throw 'The generated SkiaSharp dependency closure still contains DNG/raw/PIEX code.'
}

$generatedBuildFiles = @(Get-ChildItem -LiteralPath $gnOutput -Recurse -File |
        Where-Object Extension -eq '.ninja')
$generatedMarkerMatches = @($generatedBuildFiles | Select-String -Pattern 'dng_sdk|SkRawCodec|third_party[\\/]piex' -CaseSensitive:$false)
if ($generatedMarkerMatches.Count -gt 0) {
    $details = $generatedMarkerMatches | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }
    throw "Generated build files still reference DNG/raw/PIEX:`n$($details -join [Environment]::NewLine)"
}

$builtDll = Join-Path $checkoutRoot "output\native\$variant\x64\libSkiaSharp.dll"
if (-not (Test-Path -LiteralPath $builtDll -PathType Leaf)) {
    throw "DNG-free libSkiaSharp.dll was not produced at '$builtDll'."
}

$strongDngMarkers = @(
    'dng_pixel_buffer',
    'dng_negative',
    'dng_sdk',
    'DNG SDK',
    'SkRawCodec',
    'SkDngImage',
    '.?AVdng_'
)
Assert-NoBinaryMarkers -Path $builtDll -Markers $strongDngMarkers

$officialExports = @(Get-NormalizedExports -Dumpbin $dumpbinCommand -Dll $officialReference)
$replacementExports = @(Get-NormalizedExports -Dumpbin $dumpbinCommand -Dll $builtDll)
$exportDifferences = @(Compare-Object -ReferenceObject $officialExports -DifferenceObject $replacementExports)
if ($exportDifferences.Count -gt 0) {
    $differenceText = $exportDifferences | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "The DNG-free native C export set differs from official SkiaSharp 3.119.4:`n$($differenceText -join [Environment]::NewLine)"
}
$exportProof = @(
    '# Official SkiaSharp.NativeAssets.Win32 3.119.4 exports',
    $officialExports,
    '',
    '# DNG-free replacement exports',
    $replacementExports,
    '',
    '# Comparison',
    "Identical normalized export sets: $($officialExports.Count) exports"
)
$exportProof | Set-Content -LiteralPath (Join-Path $outputRoot 'exports.txt') -Encoding utf8

$nativeDependencies = @(Invoke-Captured $dumpbinCommand @('/nologo', '/dependents', $builtDll))
$nativeDependencies | Set-Content -LiteralPath (Join-Path $outputRoot 'native-dependencies.txt') -Encoding utf8

$signature = Get-AuthenticodeSignature -LiteralPath $builtDll
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
    throw "Expected the project-built native DLL to be NotSigned; Authenticode status was '$($signature.Status)'."
}

$destinationDll = Join-Path $outputRoot 'libSkiaSharp.dll'
Copy-Item -LiteralPath $builtDll -Destination $destinationDll
$replacementHash = Get-Sha256 $destinationDll
$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($destinationDll)

@(
    'PASS',
    'Evaluated GN argument: skia_use_dng_sdk=false',
    'DNG dependency source: not fetched',
    'GN closure: no dng_sdk, SkRawCodec, or PIEX dependency',
    "Binary markers absent: $($strongDngMarkers -join ', ')",
    "Normalized C exports match official 3.119.4: $($officialExports.Count)",
    "Authenticode status: $($signature.Status)"
) | Set-Content -LiteralPath (Join-Path $outputRoot 'verification.txt') -Encoding utf8

$sourceStatus = @(Invoke-Captured $gitCommand @('status', '--short'))
$provenance = [ordered]@{
    schemaVersion = 1
    component = 'libSkiaSharp.dll'
    purpose = 'OmniBrille Windows x64 SkiaSharp native asset with Adobe DNG/RAW support excluded'
    skiaSharpVersion = $skiaSharpVersion
    architecture = $architecture
    configuration = $configuration
    source = [ordered]@{
        repository = $skiaSharpRepository
        commit = $skiaSharpCommit
        skiaRepository = 'https://github.com/mono/skia.git'
        skiaCommit = $skiaCommit
        depotToolsRepository = 'https://chromium.googlesource.com/chromium/tools/depot_tools.git'
        depotToolsCommit = $depotToolsCommit
        sourceTreeStatus = @($sourceStatus)
        depsOriginalSha256 = $originalDepsHash
        depsPatchedSha256 = $patchedDepsHash
        removedDependencies = @(
            "https://android.googlesource.com/platform/external/dng_sdk.git@$dngRevision",
            "https://android.googlesource.com/platform/external/piex.git@$piexRevision"
        )
        patch = 'skia-deps-dng-removal.patch'
    }
    build = [ordered]@{
        upstreamTarget = 'externals-windows'
        variant = $variant
        gnArgs = 'skia_use_dng_sdk=false'
        evaluatedArgs = 'evaluated-gn-args.txt'
        dependencyClosure = 'gn-dependencies.txt'
        log = 'build.log'
    }
    toolchain = [ordered]@{
        dotnetSdk = $dotnetVersion
        cake = $cakeVersion
        python = ($pythonVersion -join ' ')
        llvm = $expectedLlvmVersion
        llvmHome = [System.IO.Path]::GetFullPath($LlvmHome)
        visualStudioInstall = $VisualStudioInstall
        dumpbin = $dumpbinCommand
        spectreLibraryDirectories = @($spectreDirectories | ForEach-Object { $_.FullName })
    }
    reference = [ordered]@{
        description = 'Official SkiaSharp.NativeAssets.Win32 3.119.4 runtimes/win-x64/native/libSkiaSharp.dll'
        sha256 = $officialReferenceHash
        normalizedExports = $officialExports.Count
    }
    artifact = [ordered]@{
        fileName = 'libSkiaSharp.dll'
        bytes = (Get-Item -LiteralPath $destinationDll).Length
        sha256 = $replacementHash
        fileVersion = $fileVersion.FileVersion
        productVersion = $fileVersion.ProductVersion
        authenticodeStatus = [string] $signature.Status
    }
    verification = [ordered]@{
        dngDependencyFetched = $false
        piexDependencyFetched = $false
        dngBuildArgument = $false
        dngInGnDependencyClosure = $false
        dngMarkersInBinary = $false
        normalizedExportsMatchOfficial = $true
        proof = 'verification.txt'
    }
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
$provenance | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $outputRoot 'provenance.json') -Encoding utf8

$proofFiles = @(Get-ChildItem -LiteralPath $outputRoot -File | Where-Object Name -ne 'proof-bundle.sha256' | Sort-Object Name)
$checksums = foreach ($proofFile in $proofFiles) {
    "$(Get-Sha256 $proofFile.FullName) *$($proofFile.Name)"
}
$checksums | Set-Content -LiteralPath (Join-Path $outputRoot 'proof-bundle.sha256') -Encoding ascii

[pscustomobject]@{
    Artifact = $destinationDll
    Sha256 = $replacementHash
    Provenance = Join-Path $outputRoot 'provenance.json'
    ProofChecksums = Join-Path $outputRoot 'proof-bundle.sha256'
    WorkDirectory = $workRoot
    AuthenticodeStatus = [string] $signature.Status
}
