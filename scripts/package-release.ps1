#!/usr/bin/env pwsh
#Requires -Version 5.1
# Custom packaging for Subnautica Head Tracking.
# Produces two ZIPs:
#   - SubnauticaHeadTracking-v{version}-installer.zip    (GitHub Release: install.cmd + plugins/ + docs)
#   - SubnauticaHeadTracking-v{version}-nexus.zip         (Nexus Mods: extract-to-game-folder layout)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

$csprojPath = Join-Path $projectDir "src\SubnauticaHeadTracking\SubnauticaHeadTracking.csproj"
$version = Get-CsprojVersion $csprojPath

$buildOutputDir = Join-Path $projectDir "src\SubnauticaHeadTracking\bin\Release\net48"
$scriptsDir = Join-Path $projectDir "scripts"
$releaseDir = Join-Path $projectDir "release"

$modDlls = @("SubnauticaHeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")

# cameraunlock-core is MIT under a different copyright holder than this mod's
# own LICENSE, so its notice has to travel with the binary it is compiled into
# rather than being treated as covered by ours.
$coreLicenseEntry = "licenses/cameraunlock-core-LICENSE.txt"
$coreLicenseSource = Join-Path $projectDir "cameraunlock-core\LICENSE"
$requiredZipEntries = @("LICENSE", "THIRD-PARTY-NOTICES.md", $coreLicenseEntry)

# Every published ZIP is a binary distribution, so a missing licence is a
# compliance failure and must stop the build rather than produce a green one.
function Copy-RequiredFile {
    param([string]$Source, [string]$Destination, [string]$Label)

    if (-not (Test-Path $Source)) {
        throw "Required notice file not found: $Source. Every published ZIP is a binary distribution and must carry it."
    }
    Copy-Item $Source -Destination $Destination -Force
    Write-Host "  $Label" -ForegroundColor Green
}

# Verify against the built archive rather than trusting the staging steps.
# Inlined instead of using the shared helper: cameraunlock-core at the pinned
# commit does not export one, so a call would fail in CI.
function Assert-ZipCarriesNotices {
    param([string]$ZipPath, [string[]]$EntryNames)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = $zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' }
    } finally {
        $zip.Dispose()
    }

    $missing = $EntryNames | Where-Object { $entries -notcontains $_ }
    if ($missing) {
        throw "$(Split-Path -Leaf $ZipPath) is missing required licence entries: $($missing -join ', ')"
    }
    Write-Host "  notices verified in archive: $($EntryNames -join ', ')" -ForegroundColor Green
}

Write-Host "=== Subnautica Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

# Validate all DLLs exist
foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutputDir $dll
    if (-not (Test-Path $dllPath)) {
        throw "Required DLL not found: $dllPath"
    }
}

# Validate required scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptsDir $script
    if (-not (Test-Path $scriptPath)) {
        throw "Required script not found: $scriptPath"
    }
}

# Create release directory
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Vendored loader is the install-time source of truth (CLAUDE.md
# "Vendoring Third-Party Dependencies"). Refreshed manually with
# `pixi run update-deps`; the packager only validates and bundles.
$vendorBepDir = Join-Path $projectDir "vendor\bepinex"
$vendorBepZip = Join-Path $vendorBepDir "BepInEx_win_x64.zip"
if (-not (Test-Path $vendorBepZip)) {
    throw "Bundled BepInEx missing: $vendorBepZip. Run 'pixi run update-deps' first."
}

# --- GitHub Release ZIP (with installer) ---

Write-Host "--- GitHub Release ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $releaseDir "staging-github"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

# Copy install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptsDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# Launcher manifest - the contract lopari consumes to deploy without parsing
# scripts. The launcher reads it from the ZIP root as launcher-manifest.json.
$manifestPath = Join-Path $projectDir "launcher-manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "Launcher manifest not found: $manifestPath"
}
$stagedManifest = Join-Path $ghStagingDir "launcher-manifest.json"
Copy-Item $manifestPath -Destination $stagedManifest -Force
# csproj is the version source of truth; stamp the shipped manifest's
# mod_info.version so it can never disagree with the built DLL.
$manifestText = Get-Content $stagedManifest -Raw
$manifestText = $manifestText -replace '("mod_info":\s*\{[^}]*?"version":\s*")[^"]*(")', "`${1}$version`$2"
Set-Content $stagedManifest $manifestText -NoNewline
Write-Host "  launcher-manifest.json (version $version)" -ForegroundColor Green

# Copy mod DLLs to plugins subfolder
$pluginsDir = Join-Path $ghStagingDir "plugins"
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $pluginsDir -Force
    Write-Host "  plugins/$dll" -ForegroundColor Green
}

# Bundle vendored BepInEx (LGPL-2.1) - the install-time source of truth.
$ghVendorDir = Join-Path $ghStagingDir "vendor\bepinex"
New-Item -ItemType Directory -Path $ghVendorDir -Force | Out-Null
# The LGPL binary is redistributed here, so its licence file is not optional.
foreach ($vendorFile in @("BepInEx_win_x64.zip", "LICENSE", "README.md")) {
    $src = Join-Path $vendorBepDir $vendorFile
    if (-not (Test-Path $src)) {
        throw "Required vendor file missing: $src"
    }
    Copy-Item $src -Destination $ghVendorDir -Force
    Write-Host "  vendor/bepinex/$vendorFile" -ForegroundColor Green
}

# Bundle the shared detection bundle for install.cmd's shim.
Copy-SharedBundle -StagingDir $ghStagingDir -CoreRoot (Join-Path $projectDir 'cameraunlock-core')

# Copy documentation
foreach ($doc in @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")) {
    Copy-RequiredFile -Source (Join-Path $projectDir $doc) -Destination $ghStagingDir -Label $doc
}

$ghLicensesDir = Join-Path $ghStagingDir "licenses"
New-Item -ItemType Directory -Path $ghLicensesDir -Force | Out-Null
Copy-RequiredFile -Source $coreLicenseSource `
    -Destination (Join-Path $ghLicensesDir "cameraunlock-core-LICENSE.txt") `
    -Label $coreLicenseEntry

$ghZipName = "SubnauticaHeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $releaseDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating GitHub ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

Assert-ZipCarriesNotices -ZipPath $ghZipPath -EntryNames ($requiredZipEntries + "vendor/bepinex/LICENSE")

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

# --- Nexus Mods ZIP (extract-to-game-folder) ---

Write-Host ""
Write-Host "--- Nexus Mods ZIP ---" -ForegroundColor Yellow
Write-Host ""

$nexusStagingDir = Join-Path $releaseDir "staging-nexus"
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }

# Mirror game directory structure: BepInEx/plugins/
# Users extract to game root, DLLs land in <game>/BepInEx/plugins/
# Does NOT include BepInEx itself (dependency)
$nexusPluginsDir = Join-Path $nexusStagingDir "BepInEx\plugins"
New-Item -ItemType Directory -Path $nexusPluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $nexusPluginsDir -Force
    Write-Host "  BepInEx/plugins/$dll" -ForegroundColor Green
}

$nexusZipName = "SubnauticaHeadTracking-v$version-nexus.zip"
$nexusZipPath = Join-Path $releaseDir $nexusZipName
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Write-Host ""
Write-Host "Creating Nexus ZIP..." -ForegroundColor Cyan

# The Nexus ZIP is a binary distribution too: the licences of everything
# compiled into the payload require their notices to travel with it, so
# LICENSE, THIRD-PARTY-NOTICES.md and the core licence ship at its root.
foreach ($noticeDoc in @("LICENSE", "THIRD-PARTY-NOTICES.md", "README.md")) {
    Copy-RequiredFile -Source (Join-Path $projectDir $noticeDoc) -Destination $nexusStagingDir -Label $noticeDoc
}

$nexusLicensesDir = Join-Path $nexusStagingDir "licenses"
New-Item -ItemType Directory -Path $nexusLicensesDir -Force | Out-Null
Copy-RequiredFile -Source $coreLicenseSource `
    -Destination (Join-Path $nexusLicensesDir "cameraunlock-core-LICENSE.txt") `
    -Label $coreLicenseEntry

Push-Location $nexusStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $nexusStagingDir

Assert-ZipCarriesNotices -ZipPath $nexusZipPath -EntryNames $requiredZipEntries

$nexusZipSize = (Get-Item $nexusZipPath).Length / 1KB
Write-Host ("  $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host ("GitHub Release:  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green
Write-Host ("Nexus Mods:      $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# Output all zip paths for CI capture (one per line)
Write-Output $ghZipPath
Write-Output $nexusZipPath
