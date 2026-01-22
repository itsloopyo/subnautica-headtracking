#!/usr/bin/env pwsh
# Revert to vanilla (unmodded) game
# Removes HeadTracking mod, and BepInEx ONLY if we installed it
# Usage: pixi run vanilla

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$StateFileName = ".headtracking-state.json"

$gamePath = if ($env:SubnauticaDir -and (Test-Path $env:SubnauticaDir)) {
    $env:SubnauticaDir
} elseif (Test-Path 'C:\Program Files (x86)\Steam\steamapps\common\Subnautica') {
    'C:\Program Files (x86)\Steam\steamapps\common\Subnautica'
} elseif (Test-Path 'C:\Program Files\Steam\steamapps\common\Subnautica') {
    'C:\Program Files\Steam\steamapps\common\Subnautica'
} elseif (Test-Path 'C:\Program Files\Epic Games\Subnautica') {
    'C:\Program Files\Epic Games\Subnautica'
} else {
    Write-Host 'ERROR: Could not find Subnautica installation.' -ForegroundColor Red
    Write-Host 'Set SubnauticaDir environment variable' -ForegroundColor Yellow
    exit 1
}

Write-Host "Reverting to vanilla (unmodded) game..." -ForegroundColor Cyan
Write-Host "  Game path: $gamePath" -ForegroundColor Gray
Write-Host ""

# Read state file
$stateFile = Join-Path $gamePath $StateFileName
$frameworkInstalledByUs = $false

if (Test-Path $stateFile) {
    try {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        $frameworkInstalledByUs = $state.framework.installed_by_us
        Write-Host "  Found state file - respecting installation history" -ForegroundColor Gray
    } catch {
        Write-Host "  Warning: Could not read state file, assuming full removal" -ForegroundColor Yellow
        $frameworkInstalledByUs = $true
    }
} else {
    Write-Host "  No state file found - will remove everything" -ForegroundColor Yellow
    $frameworkInstalledByUs = $true
}

$removed = $false

# Remove HeadTracking mod files
$pluginsPath = Join-Path $gamePath "BepInEx\plugins"
$modFiles = @(
    "SubnauticaHeadTracking.dll",
    "SubnauticaHeadTracking.pdb",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll"
)
foreach ($file in $modFiles) {
    $path = Join-Path $pluginsPath $file
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "  Removed: BepInEx\plugins\$file" -ForegroundColor Green
        $removed = $true
    }
}

# Only remove BepInEx if we installed it
if ($frameworkInstalledByUs) {
    $bepinexDir = Join-Path $gamePath "BepInEx"
    if (Test-Path $bepinexDir) {
        Remove-Item $bepinexDir -Recurse -Force
        Write-Host "  Removed: BepInEx\ (entire folder)" -ForegroundColor Green
        $removed = $true
    }

    $doorstopFiles = @("winhttp.dll", "doorstop_config.ini", ".doorstop_version")
    foreach ($file in $doorstopFiles) {
        $path = Join-Path $gamePath $file
        if (Test-Path $path) {
            Remove-Item $path -Force
            Write-Host "  Removed: $file" -ForegroundColor Green
            $removed = $true
        }
    }
} else {
    Write-Host "  BepInEx preserved (was not installed by us)" -ForegroundColor Cyan
}

# Remove state file
if (Test-Path $stateFile) {
    Remove-Item $stateFile -Force
    Write-Host "  Removed: $StateFileName" -ForegroundColor Gray
}

if (-not $removed) {
    Write-Host "  No mod files found - game is already vanilla" -ForegroundColor Yellow
}

Write-Host ""
if ($frameworkInstalledByUs) {
    Write-Host "Game is now completely vanilla (unmodded)" -ForegroundColor Cyan
} else {
    Write-Host "HeadTracking removed, BepInEx preserved for other mods" -ForegroundColor Cyan
}
Write-Host "Use 'pixi run uninstall' to remove only HeadTracking mod" -ForegroundColor Gray
