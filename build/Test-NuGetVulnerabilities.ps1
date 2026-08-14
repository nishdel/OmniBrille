[CmdletBinding()]
param(
    [string] $Solution
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Solution)) {
    $Solution = Join-Path $repositoryRoot 'OmniBrille.sln'
}

$json = & dotnet list $Solution package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) { throw 'NuGet vulnerability audit failed to run.' }
$report = ($json -join [Environment]::NewLine) | ConvertFrom-Json
$findings = @(
    foreach ($project in $report.projects) {
        foreach ($framework in $project.frameworks) {
            foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
                if ($null -ne $package.vulnerabilities -and @($package.vulnerabilities).Count -gt 0) {
                    [pscustomobject]@{
                        Project = [System.IO.Path]::GetFileNameWithoutExtension([string] $project.path)
                        Framework = [string] $framework.framework
                        Package = [string] $package.id
                        Version = [string] $package.resolvedVersion
                        Vulnerabilities = @($package.vulnerabilities).Count
                    }
                }
            }
        }
    }
)

if ($findings.Count -gt 0) {
    $findings | Format-Table -AutoSize | Out-String | Write-Error
    throw "$($findings.Count) vulnerable NuGet package reference(s) were detected."
}

Write-Output 'NuGet vulnerability audit passed: no known vulnerable packages were reported.'
