#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Subnautica Head Tracking mod.

.DESCRIPTION
    This script:
    1. Updates version in csproj
    2. Generates CHANGELOG from commits
    3. Commits the version change
    4. Creates and pushes a git tag to trigger CI release

.PARAMETER Version
    The version to release (e.g., "1.0.0", "1.2.3")

.EXAMPLE
    pixi run release 1.0.0

.NOTES
    Run via: pixi run release <version>
#>
param(
    [Parameter(Position=0)]
    [string]$Version = "",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\SubnauticaHeadTracking\SubnauticaHeadTracking.csproj"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host "=== Subnautica Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

# If no version provided, show current and exit
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <version>" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release 1.1.0" -ForegroundColor White
    exit 0
}

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Error: Invalid version format '$Version'" -ForegroundColor Red
    Write-Host "Use semantic versioning: X.Y.Z (e.g., 1.0.0, 1.2.3)" -ForegroundColor Yellow
    exit 1
}

$tagName = "v$Version"

if (-not $Force) {
    # Check if we're on main branch
    $currentBranch = git rev-parse --abbrev-ref HEAD
    if ($currentBranch -ne "main") {
        Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
        exit 1
    }

    # Check for uncommitted changes
    $status = git status --porcelain
    if ($status) {
        Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
        Write-Host $status -ForegroundColor Gray
        Write-Host "Please commit or stash changes before releasing" -ForegroundColor Yellow
        exit 1
    }

    # Check if tag already exists
    $existingTag = git tag -l $tagName
    if ($existingTag) {
        Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "WARNING: --force mode, skipping git checks" -ForegroundColor Yellow
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Confirm
Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  1. Update version in csproj to $Version" -ForegroundColor White
Write-Host "  2. Generate CHANGELOG from commits" -ForegroundColor White
Write-Host "  3. Commit the change" -ForegroundColor White
Write-Host "  4. Create tag $tagName and push (triggers release workflow)" -ForegroundColor White
Write-Host ""

$confirm = Read-Host "Continue? (y/N)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""

# Step 1: Update version
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

# Step 2: Generate CHANGELOG
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    $date = Get-Date -Format 'yyyy-MM-dd'
    $firstEntry = "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n"
    Set-Content $changelogPath $firstEntry
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    $changelogArgs = @{
        ChangelogPath = $changelogPath
        Version = $Version
        ArtifactPaths = @(
            "src/SubnauticaHeadTracking/",
            "cameraunlock-core",
            "scripts/install.cmd",
            "scripts/uninstall.cmd"
        )
    }
    if ($Force) { $changelogArgs.IncludeAll = $true }
    New-ChangelogFromCommits @changelogArgs
}

# Step 3: Commit
Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath $changelogPath
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Commit failed!" -ForegroundColor Red
    exit 1
}

# Step 4: Create annotated tag
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"

# Step 5: Push
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
git push origin $tagName

Write-Host ""
Write-Host "Release $tagName initiated!" -ForegroundColor Green
Write-Host ""
Write-Host "The GitHub Actions release workflow will now:" -ForegroundColor Yellow
Write-Host "  - Build the release" -ForegroundColor White
Write-Host "  - Create GitHub release with installer + Nexus Mods artifacts" -ForegroundColor White
Write-Host ""
Write-Host "Watch progress at:" -ForegroundColor Yellow
Write-Host "  https://github.com/udkyo/subnautica-head-tracking/actions" -ForegroundColor Cyan
