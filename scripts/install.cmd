@echo off
:: ============================================
:: Subnautica - Install
:: ============================================
:: Thin wrapper - install body lives in cameraunlock-core/scripts/install-body-bepinex.cmd.

:: --- CONFIG BLOCK ---
set "GAME_ID=subnautica"
set "MOD_DISPLAY_NAME=Subnautica Head Tracking"
set "MOD_DLLS=SubnauticaHeadTracking.dll CameraUnlock.Core.dll CameraUnlock.Core.Unity.dll"
set "MOD_INTERNAL_NAME=SubnauticaHeadTracking"
set "MOD_VERSION=1.0.0"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=BepInEx"
set "BEPINEX_ARCH=x64"
set "BEPINEX_VENDOR_ZIP_NAME="
set "BEPINEX_SUBFOLDER="
set "MOD_CONTROLS=Controls:&echo   Home    - Recenter head tracking&echo   End     - Toggle head tracking on/off&echo   Page Up - Toggle position tracking on/off"
:: --- END CONFIG BLOCK ---

set "WRAPPER_DIR=%~dp0"
set "_BODY=%WRAPPER_DIR%shared\install-body-bepinex.cmd"
if not exist "%_BODY%" set "_BODY=%WRAPPER_DIR%..\cameraunlock-core\scripts\install-body-bepinex.cmd"
if not exist "%_BODY%" (
    echo ERROR: install-body-bepinex.cmd not found in shared\ or ..\cameraunlock-core\scripts\.
    echo If this is a release ZIP, re-download it from GitHub ^(corrupt installer^).
    echo If this is the dev tree, run: git submodule update --init --recursive
    exit /b 1
)
call "%_BODY%" %*
exit /b %errorlevel%