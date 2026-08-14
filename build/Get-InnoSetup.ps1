[CmdletBinding()]
param(
    [string] $Destination
)

$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $scriptDirectory '..\artifacts\tools\inno-setup-6.7.3'
}
$innoVersion = '6.7.3'
$downloadUri = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe'
$expectedSha256 = '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'
$resolvedDestination = [System.IO.Path]::GetFullPath($Destination)
$compiler = Join-Path $resolvedDestination 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) {
    Write-Output $compiler
    return
}

$downloadDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'OmniBrillePackaging'
New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
$installer = Join-Path $downloadDirectory "innosetup-$innoVersion.exe"
Invoke-WebRequest -Uri $downloadUri -OutFile $installer
$actualSha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "Inno Setup download hash mismatch. Expected $expectedSha256, received $actualSha256."
}

New-Item -ItemType Directory -Path $resolvedDestination -Force | Out-Null
$process = Start-Process -FilePath $installer -ArgumentList @(
    '/VERYSILENT',
    '/SUPPRESSMSGBOXES',
    '/NORESTART',
    "/DIR=$resolvedDestination"
) -Wait -PassThru
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) {
    throw "Inno Setup $innoVersion bootstrap failed with exit code $($process.ExitCode)."
}

Write-Output $compiler
