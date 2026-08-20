[CmdletBinding()]
param(
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

$errors = [System.Collections.Generic.List[string]]::new()
$requiredPaths = @(
    'AGENTS.md',
    'docs/engineering/README.md',
    'docs/architecture.md'
)

foreach ($relativePath in $requiredPaths) {
    $fullPath = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $errors.Add("Required engineering entry point is missing: $relativePath")
    }
}

$excludedDirectoryPattern = '[\\/](\.git|bin|obj|artifacts|TestResults)[\\/]'
$markdownFiles = @(
    Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -File -Filter '*.md' |
        Where-Object { $_.FullName -notmatch $excludedDirectoryPattern }
)
$relativeLinkCount = 0
$mermaidDiagramCount = 0
$linkPattern = [regex]'(?<!!)\[[^\]]+\]\((?<target>[^)]+)\)'

foreach ($file in $markdownFiles) {
    $rootPrefix = $RepositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $relativeFile = $file.FullName.Substring($rootPrefix.Length)
    $text = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($match in $linkPattern.Matches($text)) {
        $target = $match.Groups['target'].Value.Trim()
        if ($target.StartsWith('<', [StringComparison]::Ordinal)) {
            $closingBracket = $target.IndexOf('>')
            if ($closingBracket -lt 1) {
                $errors.Add("Malformed angle-bracket link in ${relativeFile}: $target")
                continue
            }

            $target = $target.Substring(1, $closingBracket - 1)
        }
        else {
            $target = ($target -split '\s+', 2)[0]
        }

        if ([string]::IsNullOrWhiteSpace($target) -or
            $target.StartsWith('#', [StringComparison]::Ordinal) -or
            $target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
            continue
        }

        $target = [Uri]::UnescapeDataString(($target -split '[?#]', 2)[0])
        if ([string]::IsNullOrWhiteSpace($target)) {
            continue
        }

        $relativeLinkCount++
        $resolvedTarget = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $target))
        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            $errors.Add("Broken repository-relative link in ${relativeFile}: $target")
        }
    }

    $insideFence = $false
    $insideMermaid = $false
    $mermaidStartLine = 0
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if (-not $insideFence -and $line -match '^\s*```(?<language>[A-Za-z0-9_-]*)\s*$') {
            $insideFence = $true
            $insideMermaid = $Matches['language'] -eq 'mermaid'
            if ($insideMermaid) {
                $mermaidStartLine = $lineNumber
            }

            continue
        }

        if ($insideFence -and $line -match '^\s*```\s*$') {
            if ($insideMermaid) {
                $mermaidDiagramCount++
            }

            $insideFence = $false
            $insideMermaid = $false
        }
    }

    if ($insideFence) {
        if ($insideMermaid) {
            $description = "Mermaid fence opened at line $mermaidStartLine"
        }
        else {
            $description = 'code fence'
        }

        $errors.Add("Unclosed $description in $relativeFile")
    }
}

if ($mermaidDiagramCount -eq 0) {
    $errors.Add('No closed Mermaid diagram was found in repository documentation.')
}

if ($errors.Count -gt 0) {
    throw "Engineering documentation validation failed:`n$($errors -join [Environment]::NewLine)"
}

Write-Host "Engineering documentation validation passed: $($markdownFiles.Count) Markdown files, $relativeLinkCount relative links, $mermaidDiagramCount Mermaid diagrams."
