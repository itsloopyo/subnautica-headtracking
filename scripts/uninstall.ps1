#!/usr/bin/env pwsh
# Remove HeadTracking mod only (keeps BepInEx for other mods)
# Usage: pixi run uninstall

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

$pluginsPath = Join-Path $gamePath "BepInEx\plugins"

Write-Host "Uninstalling HeadTracking mod..." -ForegroundColor Cyan
Write-Host "  Game path: $gamePath" -ForegroundColor Gray

$removed = $false

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

if (-not $removed) {
    Write-Host "  No mod files found - already uninstalled" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "HeadTracking mod uninstalled" -ForegroundColor Cyan
Write-Host "BepInEx remains intact for other mods" -ForegroundColor Gray
Write-Host "Run 'pixi run install' to reinstall" -ForegroundColor Gray
