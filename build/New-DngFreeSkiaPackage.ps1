[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ProofDirectory,

    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\packages')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedDllSha256 = 'A5A4C1EECE528A5BED7C98889435BD8214BBA610F963FE80E35256A91508B5DD'
$expectedNoticeSha256 = 'D865C31394CD46C76DDBA4405E96650D3EFA6066C553BD9BCF60D48B4DD6880B'
$packageName = 'OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng.3.119.4.2.nupkg'
$proof = [System.IO.Path]::GetFullPath($ProofDirectory)
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$project = Join-Path $PSScriptRoot 'native-skia\OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng.csproj'
$notice = Join-Path $PSScriptRoot '..\THIRD-PARTY-LICENSES\SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt'

foreach ($required in @('libSkiaSharp.dll', 'provenance.json', 'verification.txt', 'proof-bundle.sha256', 'sdk-selection.patch', 'skia-deps-dng-removal.patch', 'windows-reproducibility.patch')) {
    if (-not (Test-Path -LiteralPath (Join-Path $proof $required) -PathType Leaf)) {
        throw "Native proof bundle is missing '$required'."
    }
}

foreach ($line in Get-Content -LiteralPath (Join-Path $proof 'proof-bundle.sha256')) {
    if ($line -notmatch '^([0-9A-F]{64}) \*(.+)$') {
        throw "Malformed proof checksum line '$line'."
    }
    $actual = (Get-FileHash -LiteralPath (Join-Path $proof $Matches[2]) -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $Matches[1]) {
        throw "Native proof checksum failed for '$($Matches[2])'."
    }
}

$dll = Join-Path $proof 'libSkiaSharp.dll'
$dllHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToUpperInvariant()
if ($dllHash -ne $expectedDllSha256) {
    throw "Native DLL hash was '$dllHash'; expected '$expectedDllSha256'."
}
if ((Get-AuthenticodeSignature -LiteralPath $dll).Status -ne [System.Management.Automation.SignatureStatus]::NotSigned) {
    throw 'The reviewed project-built native DLL must remain explicitly NotSigned.'
}

$binaryText = [System.Text.Encoding]::Latin1.GetString([System.IO.File]::ReadAllBytes($dll))
$forbiddenMarkers = @('dng_pixel_buffer', 'dng_negative', 'dng_priority_manager', 'dng_sdk', 'DNG SDK', 'SkRawCodec', 'SkDngHost', 'SkDngImage', '.?AVdng_')
$found = @($forbiddenMarkers | Where-Object { $binaryText.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
if ($found.Count -gt 0) {
    throw "Native DLL contains excluded DNG/raw markers: $($found -join ', ')."
}

$provenance = Get-Content -Raw -LiteralPath (Join-Path $proof 'provenance.json') | ConvertFrom-Json
if ($provenance.artifact.sha256 -ne $expectedDllSha256 -or
    -not $provenance.verification.normalizedExportsMatchOfficial -or
    $provenance.verification.dngDependencyFetched -or
    $provenance.verification.piexDependencyFetched -or
    $provenance.verification.dngBuildArgument -or
    $provenance.verification.dngInGnDependencyClosure -or
    $provenance.verification.dngMarkersInBinary) {
    throw 'Native provenance does not contain the required DNG-free and ABI-equivalence results.'
}

$noticeHash = (Get-FileHash -LiteralPath $notice -Algorithm SHA256).Hash.ToUpperInvariant()
if ($noticeHash -ne $expectedNoticeSha256) {
    throw "Derived SkiaSharp notice hash was '$noticeHash'; expected '$expectedNoticeSha256'."
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$packagePath = Join-Path $output $packageName
if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

& dotnet pack $project --configuration Release --output $output "-p:NativeProofDirectory=$proof"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw 'DNG-free SkiaSharp native package creation failed.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    $requiredEntries = @(
        'runtimes/win-x64/native/libSkiaSharp.dll',
        'LICENSE.txt',
        'THIRD-PARTY-NOTICES.txt',
        'provenance/build.log',
        'provenance/clang-version.txt',
        'provenance/dotnet-info.txt',
        'provenance/evaluated-gn-args.txt',
        'provenance/exports.txt',
        'provenance/gn-dependencies.txt',
        'provenance/native-dependencies.txt',
        'provenance/provenance.json',
        'provenance/verification.txt',
        'provenance/proof-bundle.sha256',
        'provenance/windows-reproducibility.patch'
    )
    foreach ($requiredEntry in $requiredEntries) {
        if ($requiredEntry -notin $entries) {
            throw "Package is missing '$requiredEntry'."
        }
    }
    $nativeEntries = @($entries | Where-Object { $_ -like 'runtimes/*/native/libSkiaSharp.dll' })
    if ($nativeEntries.Count -ne 1 -or $nativeEntries[0] -ne 'runtimes/win-x64/native/libSkiaSharp.dll') {
        throw "Package must contain exactly one Windows x64 native DLL; found '$($nativeEntries -join ', ')'."
    }
}
finally {
    $archive.Dispose()
}

[pscustomobject]@{
    Package = $packagePath
    Sha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToUpperInvariant()
    NativeDllSha256 = $dllHash
    NoticeSha256 = $noticeHash
}
