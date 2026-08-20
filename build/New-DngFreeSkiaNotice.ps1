[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $UpstreamNoticePath,

    [string] $OutputPath = (Join-Path $PSScriptRoot '..\THIRD-PARTY-LICENSES\SkiaSharp-HarfBuzz-THIRD-PARTY-NOTICES.txt')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedUpstreamSha256 = '21504C46C4C58AA64C1055BD2DCBC5F9A136B4B8C412ED3CC6740E22C5B127F5'
$upstream = [System.IO.Path]::GetFullPath($UpstreamNoticePath)
$output = [System.IO.Path]::GetFullPath($OutputPath)

if (-not (Test-Path -LiteralPath $upstream -PathType Leaf)) {
    throw "Upstream SkiaSharp notice was not found at '$upstream'."
}

$upstreamHash = (Get-FileHash -LiteralPath $upstream -Algorithm SHA256).Hash.ToUpperInvariant()
if ($upstreamHash -ne $expectedUpstreamSha256) {
    throw "Upstream SkiaSharp 3.119.4 notice changed from '$expectedUpstreamSha256' to '$upstreamHash'. Review the complete notice before deriving a new distribution copy."
}

$text = [System.IO.File]::ReadAllText($upstream).Replace("`r`n", "`n")
$sectionRule = '(?ms)^#{{80}}\n# {0}\n#{{80}}\n.*?^#{{80}}\n# END: {0}\n#{{80}}\n\n?'
foreach ($section in @('DNG SDK', 'piex')) {
    $pattern = $sectionRule -f [regex]::Escape($section)
    $matches = [regex]::Matches($text, $pattern)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one '$section' notice section; found $($matches.Count)."
    }
    $text = [regex]::Replace($text, $pattern, '', 1)
}

$header = @'
################################################################################
# OMNIBRILLE DNG-FREE SKIASHARP 3.119.4 NOTICE
################################################################################

This notice is derived from the official SkiaSharp.NativeAssets.Win32 3.119.4
THIRD-PARTY-NOTICES.txt (SHA-256
21504C46C4C58AA64C1055BD2DCBC5F9A136B4B8C412ED3CC6740E22C5B127F5).

OmniBrille's pinned native build does not fetch or link the Adobe DNG SDK or
the RAW-only PIEX dependency. Their two upstream notice sections are therefore
omitted. All other upstream notice sections are retained conservatively.
Build provenance and exclusion checks are documented in docs/native-skia.md.

'@
$derived = $header.Replace("`r`n", "`n") + "`n" + $text

foreach ($forbidden in @('DNG SDK License Agreement', '# DNG SDK', '# piex', 'external/dng_sdk', 'external/piex')) {
    if ($derived.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Derived SkiaSharp notice still contains excluded component marker '$forbidden'."
    }
}

$outputDirectory = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}
[System.IO.File]::WriteAllText($output, $derived, [System.Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Output = $output
    Sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToUpperInvariant()
    UpstreamSha256 = $upstreamHash
    RemovedSections = @('DNG SDK', 'piex')
}
