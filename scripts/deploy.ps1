#!/usr/bin/env pwsh
# Deploy built mod to Subnautica BepInEx plugins folder
# Usage: deploy.ps1 [Configuration]
# Example: deploy.ps1 Debug  or  deploy.ps1 Release

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = "Stop"

# Import shared game detection module
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$modulePath = Join-Path $projectRoot "cameraunlock-core\powershell\GamePathDetection.psm1"
Import-Module $modulePath -Force

$gameId = 'Subnautica'
$config = Get-GameConfig -GameId $gameId

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Subnautica' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

$pluginsPath = Join-Path $gamePath "BepInEx/plugins"
if (-not (Test-Path $pluginsPath)) {
    New-Item -ItemType Directory -Path $pluginsPath -Force | Out-Null
    Write-Host "Created plugins folder: $pluginsPath" -ForegroundColor Gray
}

$buildPath = "src/SubnauticaHeadTracking/bin/$Configuration/net48"

# Validate build output exists
if (-not (Test-Path $buildPath)) {
    Write-Host "ERROR: Build output not found at $buildPath" -ForegroundColor Red
    Write-Host "Please run 'pixi run build' or 'pixi run build-release' first" -ForegroundColor Yellow
    exit 1
}

Write-Host "Deploying HeadTracking ($Configuration) to BepInEx..." -ForegroundColor Green
Write-Host "  Source: $buildPath" -ForegroundColor Gray
Write-Host "  Target: $pluginsPath" -ForegroundColor Gray

# Copy DLLs
Copy-Item "$buildPath/SubnauticaHeadTracking.dll" $pluginsPath -Force
Copy-Item "$buildPath/CameraUnlock.Core.dll" $pluginsPath -Force
Copy-Item "$buildPath/CameraUnlock.Core.Unity.dll" $pluginsPath -Force
Write-Host "  Copied: SubnauticaHeadTracking.dll, CameraUnlock.Core.dll, CameraUnlock.Core.Unity.dll" -ForegroundColor Gray

# Copy PDB if it exists (Debug builds only)
$pdbPath = "$buildPath/SubnauticaHeadTracking.pdb"
if (Test-Path $pdbPath) {
    Copy-Item $pdbPath $pluginsPath -Force
    Write-Host "  Copied: SubnauticaHeadTracking.pdb (debug symbols)" -ForegroundColor Gray
}

# Deploy config file
$configPath = Join-Path $gamePath "BepInEx/config"
if (-not (Test-Path $configPath)) {
    New-Item -ItemType Directory -Path $configPath -Force | Out-Null
}
$configSamplePath = "config/com.cameraunlock.subnautica.headtracking.cfg.sample"
$configTargetPath = Join-Path $configPath "com.cameraunlock.subnautica.headtracking.cfg"
if (Test-Path $configSamplePath) {
    Copy-Item $configSamplePath $configTargetPath -Force
    Write-Host "  Copied: com.cameraunlock.subnautica.headtracking.cfg (config)" -ForegroundColor Gray
}

Write-Host '' -ForegroundColor Green
Write-Host "[OK] Deployment complete!" -ForegroundColor Green
Write-Host "DLL location: $pluginsPath/SubnauticaHeadTracking.dll" -ForegroundColor Cyan
Write-Host "Config location: $configTargetPath" -ForegroundColor Cyan
Write-Host '' -ForegroundColor Green
Write-Host "Launch Subnautica to test your changes." -ForegroundColor Yellow
