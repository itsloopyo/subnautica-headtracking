#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper - dev-deploy orchestration lives in
# cameraunlock-core/powershell/DevDeploy.psm1.

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory=$false, Position=1)]
    [string]$GivenPath,
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\DevDeploy.psm1") -Force
$buildOutput = Join-Path $projectRoot "src\SubnauticaHeadTracking\bin\$Configuration\net48"
$result = Invoke-DevDeployBepInEx `
    -GameId 'subnautica' `
    -GameDisplayName 'Subnautica' `
    -BuildOutputPath $buildOutput `
    -ModDllName 'SubnauticaHeadTracking.dll' `
    -ExtraDlls @('CameraUnlock.Core.dll', 'CameraUnlock.Core.Unity.dll') `
    -GivenPath $GivenPath `
    -EnsureLoader

# The shared Write-DeploymentSuccess only prints Recenter/Toggle; this mod has a
# fuller control set, so print the success block directly to keep it accurate.
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Head Tracking mod has been deployed to:" -ForegroundColor White
Write-Host "  $($result.DeployedDllPath)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Start the game to use head tracking!" -ForegroundColor White
Write-Host ""
Write-Host "Controls:" -ForegroundColor Yellow
Write-Host "  Home      - Recenter head tracking" -ForegroundColor Gray
Write-Host "  End       - Toggle head tracking on/off" -ForegroundColor Gray
Write-Host "  Page Up   - Cycle tracking mode (full / rotation-only / position-only)" -ForegroundColor Gray
Write-Host "  Insert    - Toggle yaw mode (world / local)" -ForegroundColor Gray
Write-Host "  Page Down - Cycle UDP port (4242-4245)" -ForegroundColor Gray
Write-Host ""
Write-Host "  No nav cluster? Chords: Ctrl+Shift+ T=Recenter Y=Toggle G=Mode U=Yaw H=Port" -ForegroundColor DarkGray
Write-Host ""