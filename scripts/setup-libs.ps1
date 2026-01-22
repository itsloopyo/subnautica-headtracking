#!/usr/bin/env pwsh
# Setup Subnautica DLLs - validates and installs dependencies
# Automatically installs BepInEx if not present

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "Checking Subnautica setup..." -ForegroundColor Cyan

# Import shared modules
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$sharedModulesPath = Join-Path $projectRoot "cameraunlock-core\powershell"
Import-Module (Join-Path $sharedModulesPath "GamePathDetection.psm1") -Force
Import-Module (Join-Path $sharedModulesPath "ModLoaderSetup.psm1") -Force

$gameId = 'Subnautica'
$config = Get-GameConfig -GameId $gameId

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Subnautica' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Found Subnautica at: $gamePath" -ForegroundColor Green

# Verify game DLLs exist
$managedPath = Get-ManagedPath -GamePath $gamePath -DataFolder $config.DataFolder
if (Test-Path $managedPath) {
    Write-Host "Game DLLs found at: $managedPath" -ForegroundColor Green
} else {
    Write-Host "ERROR: Managed folder not found at $managedPath" -ForegroundColor Red
    exit 1
}

# Install BepInEx if not present (uses shared module)
# Subnautica uses BepInEx 5.x (stable) with x64 architecture
$bepinexResult = Install-BepInEx -GamePath $gamePath -Architecture x64 -MajorVersion 5 -EnableConsole $true

if ($bepinexResult.AlreadyInstalled) {
    Write-Host "BepInEx found at: $(Get-BepInExCorePath -GamePath $gamePath)" -ForegroundColor Green
}

# Copy required DLLs to local libs folder for compilation
$libsDir = Join-Path $projectRoot "src\SubnauticaHeadTracking\libs"
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force | Out-Null
}

# BepInEx DLLs
$bepinexCorePath = Get-BepInExCorePath -GamePath $gamePath
$bepinexDlls = @("BepInEx.dll", "0Harmony.dll")
foreach ($dll in $bepinexDlls) {
    $source = Join-Path $bepinexCorePath $dll
    if (Test-Path $source) {
        Copy-Item $source $libsDir -Force
        Write-Host "  Copied $dll (BepInEx)" -ForegroundColor Gray
    } else {
        Write-Warning "  Not found: $source"
    }
}

# Unity/game DLLs
$managedDlls = @(
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.UIModule.dll"
)
foreach ($dll in $managedDlls) {
    $source = Join-Path $managedPath $dll
    if (Test-Path $source) {
        Copy-Item $source $libsDir -Force
        Write-Host "  Copied $dll" -ForegroundColor Gray
    } else {
        Write-Warning "  Not found: $source"
    }
}

Write-Host ""
Write-Host "Setup verified! Ready to build." -ForegroundColor Green
