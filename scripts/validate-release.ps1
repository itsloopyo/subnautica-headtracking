#!/usr/bin/env pwsh
# Validate release readiness before tagging

$ErrorActionPreference = "Stop"

Write-Host "Validating release readiness..." -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
Import-Module (Join-Path $projectDir "cameraunlock-core/powershell/ReleaseWorkflow.psm1") -Force

$csprojPath = Join-Path $projectDir "src/SubnauticaHeadTracking/SubnauticaHeadTracking.csproj"
$version = Get-CsprojVersion $csprojPath

Write-Host "Current version in .csproj: $version" -ForegroundColor Yellow

# Check 1: Verify version format is semantic versioning
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "❌ FAIL: Version '$version' is not valid semantic versioning (x.y.z)" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Version format is valid" -ForegroundColor Green

# Check 2: Verify CHANGELOG.md exists
if (-not (Test-Path "CHANGELOG.md")) {
    Write-Host "❌ FAIL: CHANGELOG.md does not exist" -ForegroundColor Red
    Write-Host "Create a CHANGELOG.md file to track version history" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ CHANGELOG.md exists" -ForegroundColor Green

# Check 3: Verify CHANGELOG has entry for current version
$changelog = Get-Content "CHANGELOG.md" -Raw
if ($changelog -notmatch "\[?$version\]?") {
    Write-Host "❌ FAIL: CHANGELOG.md missing entry for version $version" -ForegroundColor Red
    Write-Host "Add a changelog entry with heading: ## [$version] - $(Get-Date -Format 'yyyy-MM-dd')" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ CHANGELOG.md contains entry for v$version" -ForegroundColor Green

# Check 4: Verify tag doesn't already exist
$tagExists = git tag -l "v$version" 2>$null
if ($LASTEXITCODE -eq 0 -and $tagExists) {
    Write-Host "❌ FAIL: Git tag v$version already exists" -ForegroundColor Red
    Write-Host "Bump the version in the .csproj to create a new release" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ Tag v$version does not yet exist" -ForegroundColor Green

# Check 5: Verify launcher-manifest.json exists, parses, and matches the csproj version
$manifestPath = Join-Path $projectDir "launcher-manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Host "❌ FAIL: launcher-manifest.json does not exist" -ForegroundColor Red
    exit 1
}
try {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
} catch {
    Write-Host "❌ FAIL: launcher-manifest.json is not valid JSON: $_" -ForegroundColor Red
    exit 1
}
if ($manifest.mod_info.version -ne $version) {
    Write-Host "❌ FAIL: launcher-manifest.json mod_info.version '$($manifest.mod_info.version)' does not match csproj version '$version'" -ForegroundColor Red
    Write-Host "release.ps1 stamps this automatically; if editing by hand keep them in sync" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ launcher-manifest.json present, valid, and version matches" -ForegroundColor Green

Write-Host ""
Write-Host "🎉 All validation checks passed!" -ForegroundColor Green
Write-Host ""
Write-Host "Ready to release v$version!" -ForegroundColor Cyan
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. git tag v$version" -ForegroundColor White
Write-Host "  2. git push origin main --tags" -ForegroundColor White
Write-Host ""
