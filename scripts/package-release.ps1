#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper: calls shared packaging script with Subnautica values.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

& "$projectDir/cameraunlock-core/scripts/package-bepinex-mod.ps1" `
    -ModName "SubnauticaHeadTracking" `
    -CsprojPath "src/SubnauticaHeadTracking/SubnauticaHeadTracking.csproj" `
    -BuildOutputDir "src/SubnauticaHeadTracking/bin/Release/net48" `
    -ModDlls @("SubnauticaHeadTracking.dll","CameraUnlock.Core.dll","CameraUnlock.Core.Unity.dll") `
    -ProjectRoot $projectDir `
    -CreateNexusZip
