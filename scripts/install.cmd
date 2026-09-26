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
set "MOD_VERSION=1.4.0"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=BepInEx"
set "BEPINEX_ARCH=x64"
set "BEPINEX_VENDOR_ZIP_NAME="
set "BEPINEX_SUBFOLDER="
set "MOD_CONTROLS=Controls:&echo   End          - Toggle head tracking on/off&echo   Page Up      - Cycle tracking mode&echo   Page Down    - Toggle yaw mode (world/local)&echo   Ctrl+Shift+J - Cycle UDP port (4242-4245)&echo   An update keeps the keys you had. Change them in BepInEx\config\CameraUnlock.ini."
:: Not used by this mod. Set blank so a value another mod's wrapper left in
:: the same console does not reach the body.
set "PLUGIN_SUBFOLDER="
set "IL2CPP_VENDOR_DIR_NAME="
set "IL2CPP_VENDOR_ZIP_NAME="
set "IL2CPP_PLUGIN_DIR_NAME="
set "IL2CPP_MOD_DLLS="
:: --- END CONFIG BLOCK ---

:: Pin delayed expansion off before `%*` is expanded on the `call` below.
:: Under `cmd /V:ON`, or with DelayedExpansion=1 in
:: HKCU\Software\Microsoft\Command Processor, cmd.exe eats a `!` out of the
:: expanded line, and a real game path like C:\Games\Oh! My Game reaches the
:: body already mangled. The body pins expansion off at its own outer scope
:: too, but that is one `call` too late to save the argument it was handed.
setlocal disabledelayedexpansion

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