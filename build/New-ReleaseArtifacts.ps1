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
$resolvedPackage = [System.IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $resolvedPackage -PathType Leaf)) {
    throw "Installer was not found at '$resolvedPackage'."
}

if ([string]::IsNullOrWhiteSpace($CommitSha)) {
    $CommitSha = (& git -C $repositoryRoot rev-parse HEAD).Trim()
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
$previewNotesPath = Join-Path $outputDirectory "$baseName-private-preview-notes.md"

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
    schemaVersion = 2
    product = 'OmniBrille'
    version = $Version
    fileVersion = $NumericVersion
    commitSha = $CommitSha.ToLowerInvariant()
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
    scope = 'packaged desktop runtime dependencies'
    format = 'OmniBrille dependency manifest; not a formal SPDX or CycloneDX SBOM'
    projects = @($projects)
}
$dependencies | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $dependencyPath -Encoding UTF8

$previewTemplatePath = Join-Path $repositoryRoot 'docs\private-preview.md'
if (-not (Test-Path -LiteralPath $previewTemplatePath -PathType Leaf)) {
    throw "Tester-facing preview notes were not found at '$previewTemplatePath'."
}
$signingDescription = if ($Signed) {
    'Authenticode signed; verify the publisher and Valid signature before installation.'
} else {
    'Unsigned private preview; Windows may show Unknown Publisher or SmartScreen reputation warnings.'
}
$workflowDescription = if ($null -ne $workflow) {
    "GitHub Actions run: $($workflow.runUrl)"
} else {
    'Build workflow: local validated release check'
}
$previewBody = Get-Content -Raw -LiteralPath $previewTemplatePath
$previewNotes = @"
# OmniBrille $Version private preview

Installer: $($package.Name)
Commit: $($CommitSha.ToLowerInvariant())
SHA-256: $hash
Signing: $signingDescription
$workflowDescription

$previewBody
"@
Set-Content -LiteralPath $previewNotesPath -Encoding UTF8 -Value $previewNotes

[pscustomobject]@{
    Checksum = $checksumPath
    Manifest = $manifestPath
    DependencyManifest = $dependencyPath
    PreviewNotes = $previewNotesPath
    Sha256 = $hash
    SignatureStatus = [string] $signature.Status
}
