[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $DestinationDirectory,
    [string] $CacheDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$destination = [System.IO.Path]::GetFullPath($DestinationDirectory)
if (-not $destination.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Voice bundle output must remain under '$artifactsRoot'."
}
if ([string]::IsNullOrWhiteSpace($CacheDirectory)) {
    $CacheDirectory = Join-Path $artifactsRoot 'vendor-cache\voice'
}
$cache = [System.IO.Path]::GetFullPath($CacheDirectory)
if (-not $cache.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Voice bundle cache must remain under '$artifactsRoot'."
}

$runtimeVersion = 'v1.9.2'
$runtimeCommit = '306c88f4d1286aec1bf96e544632897886af5501'
$runtimeArchiveName = 'whisper-bin-x64.zip'
$runtimeArchiveUri = "https://github.com/ggml-org/whisper.cpp/releases/download/$runtimeVersion/$runtimeArchiveName"
$runtimeArchiveSha256 = '49DCC16DE826F20BD53D44F947A1AE49DFA81F86CAD67A64D80820CB192D674A'
$modelCommit = 'c521a4b02f422512d734391fdf08bb08c0862f68'
$modelName = 'ggml-base.en-q5_1.bin'
$modelUri = "https://huggingface.co/ggerganov/whisper.cpp/resolve/$modelCommit/${modelName}?download=true"
$modelSha256 = '4BAF70DD0D7C4247BA2B81FAFD9C01005AC77C2F9EF064E00DCF195D0E2FDD2F'
$runtimeFiles = [ordered]@{
    'ggml-base.dll' = '1482359D921B4C1B183D49DB1D770F9B5E90D86A618B8B648D4845C2471AD6B0'
    'ggml-cpu-alderlake.dll' = 'D1C5411561361F7CE71FF8455ECF01F666F581B0608FA91A1DFE7D3FD6A25BD1'
    'ggml-cpu-cannonlake.dll' = '2EF36F05FA252FF4FDCB8D42EBCE1CEBA4F3D3DE12B93BED15BDEE6237DCCD63'
    'ggml-cpu-cascadelake.dll' = '505899AAF3F99C5D714361640F561458EA97F8A09EB0614568A66BEAD2115CB0'
    'ggml-cpu-haswell.dll' = 'F8CF2F35A06498D783D77FDE42004DD54D2F8236B0D42AC323B94BBA65A603C4'
    'ggml-cpu-icelake.dll' = '78AD143EE2E674D037B4840EF33B5748A0659762A26E0AE2B621C4F9451CBDE8'
    'ggml-cpu-sandybridge.dll' = 'EE47DB7DC40FB30ECA73E62A05306059C2C3C42AECDDF2E8D6AD7E530069B815'
    'ggml-cpu-skylakex.dll' = '164E2793897944A43EE071CE6C0B09018088BDF4DD8B14AC0755C58849CF8C50'
    'ggml-cpu-sse42.dll' = '7318A9A3B95A85B2453C437B274412BBBAE89E5ECDF5BABB19B99EDC06DED063'
    'ggml-cpu-x64.dll' = 'AF0F1C2F28FF9E3F472481DD969907BDA85FA39D4FDE17617D4BB0B389301B60'
    'ggml.dll' = '894C6237EE7849843213906A2B6A0B371AAA6234048D465F206D910AE846FAFB'
    'whisper-cli.exe' = '95E3C0B0E778AD9499EB0125F97C1DCF437DD9EB4EA77050B043574F93C2631D'
    'whisper.dll' = '792FC523C7AD16E6B9C348E30AD5E5F591165CBCF6A80CA8D0DB02A38CE3EEA2'
}

function Get-VerifiedDownload {
    param(
        [Parameter(Mandatory)][string] $Uri,
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Sha256
    )

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha256) {
            return
        }
        Remove-Item -LiteralPath $Path -Force
    }

    Invoke-WebRequest -Uri $Uri -OutFile $Path
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $Sha256) {
        Remove-Item -LiteralPath $Path -Force
        throw "Downloaded voice asset '$([System.IO.Path]::GetFileName($Path))' failed SHA-256 verification."
    }
}

New-Item -ItemType Directory -Path $cache -Force | Out-Null
$runtimeArchive = Join-Path $cache "$runtimeVersion-$runtimeArchiveName"
$modelCache = Join-Path $cache "$modelCommit-$modelName"
Get-VerifiedDownload -Uri $runtimeArchiveUri -Path $runtimeArchive -Sha256 $runtimeArchiveSha256
Get-VerifiedDownload -Uri $modelUri -Path $modelCache -Sha256 $modelSha256

$extractRoot = Join-Path $cache "extract-$runtimeVersion"
if (Test-Path -LiteralPath $extractRoot) {
    $resolvedExtract = [System.IO.Path]::GetFullPath($extractRoot)
    if (-not $resolvedExtract.StartsWith($cache + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected extraction directory '$resolvedExtract'."
    }
    Remove-Item -LiteralPath $resolvedExtract -Recurse -Force
}
Expand-Archive -LiteralPath $runtimeArchive -DestinationPath $extractRoot -Force
$sourceRuntime = Join-Path $extractRoot 'Release'

if (Test-Path -LiteralPath $destination) {
    $resolvedDestination = [System.IO.Path]::GetFullPath($destination)
    if (-not $resolvedDestination.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected voice bundle directory '$resolvedDestination'."
    }
    Remove-Item -LiteralPath $resolvedDestination -Recurse -Force
}
$runtimeDestination = Join-Path $destination 'Runtime'
$modelDestination = Join-Path $destination 'Models'
New-Item -ItemType Directory -Path $runtimeDestination,$modelDestination -Force | Out-Null

$manifestFiles = @()
foreach ($entry in $runtimeFiles.GetEnumerator()) {
    $source = Join-Path $sourceRuntime $entry.Key
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Pinned runtime archive omitted '$($entry.Key)'."
    }
    $actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($actual -ne $entry.Value) {
        throw "Pinned runtime file '$($entry.Key)' failed SHA-256 verification."
    }
    $target = Join-Path $runtimeDestination $entry.Key
    Copy-Item -LiteralPath $source -Destination $target
    $manifestFiles += [ordered]@{ path = "Runtime/$($entry.Key)"; sha256 = $actual; bytes = (Get-Item -LiteralPath $target).Length }
}
$installedModel = Join-Path $modelDestination $modelName
Copy-Item -LiteralPath $modelCache -Destination $installedModel
$manifestFiles += [ordered]@{ path = "Models/$modelName"; sha256 = $modelSha256; bytes = (Get-Item -LiteralPath $installedModel).Length }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-LICENSES\whisper.cpp-MIT.txt') -Destination (Join-Path $destination 'whisper.cpp-MIT.txt')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-LICENSES\OpenAI-Whisper-MIT.txt') -Destination (Join-Path $destination 'OpenAI-Whisper-MIT.txt')

$manifest = [ordered]@{
    schemaVersion = 1
    runtime = [ordered]@{ project = 'ggml-org/whisper.cpp'; version = $runtimeVersion; commit = $runtimeCommit; archiveSha256 = $runtimeArchiveSha256; source = $runtimeArchiveUri }
    model = [ordered]@{ repository = 'ggerganov/whisper.cpp'; commit = $modelCommit; name = $modelName; sha256 = $modelSha256; source = $modelUri }
    processing = 'Local CPU process; no installed-app download path'
    files = $manifestFiles
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'voice-bundle-manifest.json') -Encoding UTF8

[pscustomobject]@{
    Destination = $destination
    RuntimeVersion = $runtimeVersion
    Model = $modelName
    Files = $manifestFiles.Count
    Bytes = (Get-ChildItem -LiteralPath $destination -Recurse -File | Measure-Object -Property Length -Sum).Sum
}
