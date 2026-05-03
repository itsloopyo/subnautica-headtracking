@echo off
:: ============================================
:: Subnautica - Uninstall
:: ============================================
:: Thin wrapper - uninstall body lives in cameraunlock-core/scripts/uninstall-body.cmd
:: (one body, framework-aware via FRAMEWORK_TYPE).

:: --- CONFIG BLOCK ---
set "GAME_ID=subnautica"
set "MOD_DISPLAY_NAME=Subnautica Head Tracking"
set "MOD_DLLS=SubnauticaHeadTracking.dll CameraUnlock.Core.dll CameraUnlock.Core.Unity.dll"
set "MOD_INTERNAL_NAME=SubnauticaHeadTracking"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=BepInEx"
set "LEGACY_DLLS=SubnauticaHeadTracking.pdb"

set "MANAGED_SUBFOLDER="
set "ASSEMBLY_DLL="
set "MANAGED_EXTRAS="
set "ASI_LOADER_NAME=winmm.dll"
:: --- END CONFIG BLOCK ---

set "WRAPPER_DIR=%~dp0"
set "_BODY=%WRAPPER_DIR%shared\uninstall-body.cmd"
if not exist "%_BODY%" set "_BODY=%WRAPPER_DIR%..\cameraunlock-core\scripts\uninstall-body.cmd"
if not exist "%_BODY%" (
    echo ERROR: uninstall-body.cmd not found in shared\ or ..\cameraunlock-core\scripts\.
    echo If this is a release ZIP, re-download it from GitHub ^(corrupt installer^).
    echo If this is the dev tree, run: git submodule update --init --recursive
    exit /b 1
)
call "%_BODY%" %*
exit /b %errorlevel%