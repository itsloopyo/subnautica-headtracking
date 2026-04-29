# BepInEx Project Setup Verification Script
# Verifies all dependencies and build prerequisites are correctly configured

param(
    [string]$SubnauticaPath = ""
)

$ErrorActionPreference = "Continue"
$script:FailureCount = 0

function Write-Status {
    param([string]$Message, [string]$Status)
    $color = switch ($Status) {
        "OK" { "Green" }
        "WARN" { "Yellow" }
        "FAIL" { "Red" }
        default { "White" }
    }
    Write-Host "[$Status] " -ForegroundColor $color -NoNewline
    Write-Host $Message
    if ($Status -eq "FAIL") { $script:FailureCount++ }
}

Write-Host "`n=== BepInEx Project Setup Verification ===`n" -ForegroundColor Cyan

# Check .NET Framework 4.7.2
Write-Host "Checking .NET Framework 4.7.2..." -ForegroundColor Yellow
$netFramework = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\' -ErrorAction SilentlyContinue | Get-ItemProperty -Name Release -ErrorAction SilentlyContinue
if ($netFramework.Release -ge 461808) {
    Write-Status ".NET Framework 4.7.2 or newer installed" "OK"
} else {
    Write-Status ".NET Framework 4.7.2 not found - required for build" "FAIL"
}

# Check build tools
Write-Host "`nChecking build tools..." -ForegroundColor Yellow
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    $dotnetVersion = & dotnet --version
    Write-Status "dotnet CLI found: v$dotnetVersion" "OK"
} else {
    Write-Status "dotnet CLI not found - install .NET SDK" "FAIL"
}

$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
if ($msbuild) {
    Write-Status "MSBuild found at: $($msbuild.Source)" "OK"
} else {
    Write-Host "[INFO] MSBuild not in PATH (optional - dotnet build works fine)" -ForegroundColor Gray
}

# Check Subnautica installation
Write-Host "`nChecking Subnautica installation..." -ForegroundColor Yellow

if (-not $SubnauticaPath) {
    $commonPaths = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Subnautica",
        "C:\Program Files\Epic Games\Subnautica",
        "D:\SteamLibrary\steamapps\common\Subnautica",
        "E:\SteamLibrary\steamapps\common\Subnautica"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $SubnauticaPath = $path
            break
        }
    }
}

if ($SubnauticaPath -and (Test-Path $SubnauticaPath)) {
    Write-Status "Subnautica found at: $SubnauticaPath" "OK"

    # Check BepInEx installation
    $bepinexCore = Join-Path $SubnauticaPath "BepInEx\core"
    if (Test-Path (Join-Path $bepinexCore "BepInEx.dll")) {
        Write-Status "BepInEx.dll found" "OK"
    } else {
        Write-Status "BepInEx.dll not found - install BepInEx 5.4.22" "FAIL"
    }

    if (Test-Path (Join-Path $bepinexCore "0Harmony.dll")) {
        Write-Status "0Harmony.dll found" "OK"
    } else {
        Write-Status "0Harmony.dll not found - install BepInEx 5.4.22" "FAIL"
    }

    # Check Unity assemblies
    $managedPath = Join-Path $SubnauticaPath "Subnautica_Data\Managed"
    $requiredUnityDlls = @(
        "UnityEngine.dll",
        "UnityEngine.CoreModule.dll",
        "UnityEngine.InputLegacyModule.dll",
        "UnityEngine.IMGUIModule.dll",
        "Assembly-CSharp.dll"
    )

    foreach ($dll in $requiredUnityDlls) {
        $dllPath = Join-Path $managedPath $dll
        if (Test-Path $dllPath) {
            Write-Status "$dll found" "OK"
        } else {
            Write-Status "$dll not found" "FAIL"
        }
    }

    # Check publicized assembly (may not exist on first build)
    $publicizedPath = Join-Path $managedPath "publicized_assemblies\Assembly-CSharp_publicized.dll"
    if (Test-Path $publicizedPath) {
        Write-Status "Assembly-CSharp_publicized.dll found" "OK"
    } else {
        Write-Status "Assembly-CSharp_publicized.dll not found - will be generated on first build" "WARN"
    }
} else {
    Write-Status "Subnautica installation not found" "FAIL"
    Write-Host "  Please specify path: .\verify-setup.ps1 -SubnauticaPath 'C:\Path\To\Subnautica'" -ForegroundColor Gray
}

# Check project files
Write-Host "`nChecking project files..." -ForegroundColor Yellow
$projectFiles = @{
    "HeadTracking.csproj" = "Project file"
    "HeadTracking.sln" = "Solution file"
    "Directory.Build.props" = "Build properties"
    "nuget.config" = "NuGet configuration"
}

foreach ($file in $projectFiles.Keys) {
    if (Test-Path $file) {
        Write-Status "$($projectFiles[$file]) found: $file" "OK"
    } else {
        Write-Status "$($projectFiles[$file]) missing: $file" "FAIL"
    }
}

# Check NuGet restore
Write-Host "`nChecking NuGet packages..." -ForegroundColor Yellow
if ($dotnet) {
    Write-Host "  Running dotnet restore..." -ForegroundColor Gray
    $restoreOutput = & dotnet restore 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Status "NuGet packages restored successfully" "OK"
    } else {
        Write-Status "NuGet restore failed" "FAIL"
        Write-Host $restoreOutput -ForegroundColor Red
    }
} else {
    Write-Status "Cannot verify NuGet packages - dotnet CLI not found" "WARN"
}

# Summary
Write-Host "`n=== Verification Summary ===" -ForegroundColor Cyan
if ($script:FailureCount -eq 0) {
    Write-Host "All checks passed! Ready to build." -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "  1. Build the project: dotnet build -c Release"
    Write-Host "  2. Check output: bin\Release\net472\HeadTracking.dll"
    Write-Host "  3. DLL auto-copies to: Subnautica\BepInEx\plugins\"
    exit 0
} else {
    Write-Host "$($script:FailureCount) check(s) failed. Please fix issues before building." -ForegroundColor Red
    Write-Host "`nCommon fixes:" -ForegroundColor Yellow
    Write-Host "  - Install BepInEx 5.4.22: https://github.com/BepInEx/BepInEx/releases"
    Write-Host "  - Install .NET SDK 8.0+: https://dotnet.microsoft.com/download"
    Write-Host "  - Update Directory.Build.props with correct Subnautica path"
    exit 1
}
