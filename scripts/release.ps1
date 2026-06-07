#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Subnautica Head Tracking mod.

.DESCRIPTION
    Fully unattended:
    1. Validate version arg (semver, or major|minor|patch keyword)
    2. Verify on main, clean tree, tag absent
    3. Update version in csproj
    4. pixi run build (release)
    5. Generate CHANGELOG from commits since last tag
    6. Commit "Release v<version>"
    7. Annotated tag v<version>
    8. Push commits + tag (CI release workflow takes over)

.PARAMETER Version
    Concrete X.Y.Z, or one of: major | minor | patch

.EXAMPLE
    pixi run release patch
.EXAMPLE
    pixi run release 1.2.3
#>
param(
    [Parameter(Position=0)]
    [string]$Version = "",
    # Ship a release even when there are no user-facing commits since the
    # last tag (writes a maintenance changelog entry instead of aborting).
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\SubnauticaHeadTracking\SubnauticaHeadTracking.csproj"
$manifestPath = Join-Path $projectDir "launcher-manifest.json"
$changelogPath = Join-Path $projectDir "CHANGELOG.md"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

# Mirrors New-ChangelogFromCommits' insertion so a -Force maintenance entry
# lands in the same place with the same shape.
function Add-MaintenanceChangelogEntry {
    param([string]$Path, [string]$NewVersion)
    $date = Get-Date -Format 'yyyy-MM-dd'
    $entry = "## [$NewVersion] - $date`n`n### Changed`n`n- Maintenance release (no user-facing changes).`n`n"
    $changelog = Get-Content $Path -Raw
    if ($changelog -match '(?s)(# Changelog.*?)(## \[)') {
        $changelog = $changelog -replace '(?s)(# Changelog.*?\n\n)', "`$1$entry"
    } else {
        $changelog = $changelog -replace '(?s)(# Changelog.*?\n)', "`$1$entry"
    }
    $changelog = $changelog.TrimEnd() + "`n"
    Set-Content $Path $changelog -NoNewline
}

Write-Host "=== Subnautica Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage:   pixi run release <major|minor|patch|X.Y.Z>" -ForegroundColor Yellow
    Write-Host "Example: pixi run release patch" -ForegroundColor Yellow
    exit 0
}

try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Error: '$Version' is not valid semver (X.Y.Z)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

$status = git status --porcelain
if ($status) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host $status -ForegroundColor Gray
    exit 1
}

$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Generate CHANGELOG from commits since last tag. This is the gate that
# aborts when there are no user-facing commits, so run it BEFORE mutating
# any version files - a failure here then leaves a clean tree instead of
# stranding a half-applied version bump with no tag.
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    $date = Get-Date -Format 'yyyy-MM-dd'
    $firstEntry = "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n"
    Set-Content $changelogPath $firstEntry
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    try {
        New-ChangelogFromCommits `
            -ChangelogPath $changelogPath `
            -Version $Version `
            -ArtifactPaths @(
                "src/SubnauticaHeadTracking/",
                "cameraunlock-core",
                "scripts/install.cmd",
                "scripts/uninstall.cmd"
            )
    } catch {
        if (-not $Force) {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "No user-facing changes to release. Re-run with -Force for a maintenance release." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "No user-facing commits since last tag - writing maintenance entry (-Force)." -ForegroundColor Yellow
        Add-MaintenanceChangelogEntry -Path $changelogPath -NewVersion $Version
    }
}

Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

$manifestText = Get-Content $manifestPath -Raw
$manifestText = $manifestText -replace '("mod_info":\s*\{[^}]*?"version":\s*")[^"]*(")', "`${1}$Version`$2"
Set-Content $manifestPath $manifestText -NoNewline

Write-Host "Building release..." -ForegroundColor Cyan
pixi run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath $changelogPath $manifestPath
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Commit failed" -ForegroundColor Red
    exit 1
}

Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"

Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
git push origin $tagName

Write-Host ""
Write-Host "Release $tagName initiated." -ForegroundColor Green
Write-Host "GitHub Actions will build and publish the release." -ForegroundColor Yellow
Write-Host "  https://github.com/udkyo/subnautica-head-tracking/actions" -ForegroundColor Cyan
