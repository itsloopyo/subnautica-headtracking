using UnityEngine;
using BepInEx.Logging;
using CameraUnlock.Core.Protocol;

namespace SubnauticaHeadTracking.Input
{
    /// <summary>
    /// Monitors keyboard input for the four mod hotkeys.
    /// Each action is bound to BOTH a nav-cluster key (configurable via the cfg file)
    /// AND a hardcoded Ctrl+Shift+&lt;letter&gt; chord, drawn from the T/Y/U/G/H/J cluster
    /// per the CameraUnlock chord-binding standard. Either binding fires the same action.
    /// Called from HeadTrackingPlugin's per-frame camera callback.
    /// </summary>
    public static class HotkeyHandler
    {
        private static ManualLogSource Logger => HeadTrackingPlugin.ModLogger;
        private static bool _hasLoggedFirstCheck = false;

        private static KeyCode _cachedToggleHotkey;
        private static KeyCode _cachedRecenterHotkey;
        private static KeyCode _cachedCycleTrackingModeHotkey;
        private static KeyCode _cachedCyclePortHotkey;
        private static bool _cacheInitialized = false;

        /// <summary>
        /// Invalidates the cached hotkey values, forcing them to be reloaded from config.
        /// Call this when configuration changes.
        /// </summary>
        public static void InvalidateCache()
        {
            _cacheInitialized = false;
        }

        /// <summary>
        /// Checks for hotkey presses and executes corresponding actions.
        /// Uses UnityEngine.Input.GetKeyDown() for single-frame key press detection.
        /// </summary>
        public static void CheckHotkeys()
        {
            if (!_hasLoggedFirstCheck)
            {
                Logger.LogInfo("HotkeyHandler: First check - hotkey system active");
                _hasLoggedFirstCheck = true;
            }

            if (!_cacheInitialized)
            {
                _cachedToggleHotkey = Config.ConfigurationManager.ToggleHotkey.Value;
                _cachedRecenterHotkey = Config.ConfigurationManager.RecenterHotkey.Value;
                _cachedCycleTrackingModeHotkey = Config.ConfigurationManager.CycleTrackingModeHotkey.Value;
                _cachedCyclePortHotkey = Config.ConfigurationManager.CyclePortHotkey.Value;
                _cacheInitialized = true;
            }

            bool chordModifiers = IsCtrlShiftHeld();

            // Recenter: Home or Ctrl+Shift+T
            if (UnityEngine.Input.GetKeyDown(_cachedRecenterHotkey)
                || (chordModifiers && UnityEngine.Input.GetKeyDown(KeyCode.T)))
            {
                HandleRecenterHotkey();
            }

            // Toggle tracking: End or Ctrl+Shift+Y
            if (UnityEngine.Input.GetKeyDown(_cachedToggleHotkey)
                || (chordModifiers && UnityEngine.Input.GetKeyDown(KeyCode.Y)))
            {
                HandleToggleHotkey();
            }

            // Cycle tracking mode: Page Up or Ctrl+Shift+G
            if (UnityEngine.Input.GetKeyDown(_cachedCycleTrackingModeHotkey)
                || (chordModifiers && UnityEngine.Input.GetKeyDown(KeyCode.G)))
            {
                HandleCycleTrackingModeHotkey();
            }

            // Cycle UDP port: Page Down or Ctrl+Shift+H
            if (UnityEngine.Input.GetKeyDown(_cachedCyclePortHotkey)
                || (chordModifiers && UnityEngine.Input.GetKeyDown(KeyCode.H)))
            {
                HeadTrackingPlugin.CyclePort();
            }
        }

        private static bool IsCtrlShiftHeld()
        {
            bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl)
                     || UnityEngine.Input.GetKey(KeyCode.RightControl);
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift)
                      || UnityEngine.Input.GetKey(KeyCode.RightShift);
            return ctrl && shift;
        }

        /// <summary>
        /// Handles the toggle hotkey press.
        /// Toggles tracking enabled/disabled state.
        /// </summary>
        private static void HandleToggleHotkey()
        {
            State.TrackingState.ToggleTracking();

            string message = State.TrackingState.IsEnabled
                ? "Head Tracking: ENABLED"
                : "Head Tracking: DISABLED";

            Logger.LogInfo($"Toggle hotkey pressed: {message}");
        }

        /// <summary>
        /// Advances the three-state tracking-mode cycle:
        /// Full → rotation only (position disabled) → position only (rotation disabled) → Full.
        /// </summary>
        private static void HandleCycleTrackingModeHotkey()
        {
            State.TrackingState.CycleMode();
        }

        /// <summary>
        /// Handles the recenter hotkey press.
        /// Captures current head position as neutral position.
        /// </summary>
        private static void HandleRecenterHotkey()
        {
            if (!State.TrackingState.IsEnabled)
            {
                Logger.LogWarning("Recenter hotkey pressed but tracking is disabled - enable tracking first");
                return;
            }

            OpenTrackReceiver receiver = HeadTrackingPlugin.Receiver;
            if (receiver == null)
            {
                Logger.LogWarning("Recenter hotkey pressed but no receiver available");
                return;
            }

            if (!receiver.IsReceiving)
            {
                Logger.LogWarning("Recenter hotkey pressed but tracking data is stale (no recent OpenTrack packets)");
                return;
            }

            receiver.GetRawRotation(out float yaw, out float pitch, out float roll);
            Camera.CameraRotationApplicator.Recenter(yaw, pitch, roll);
            Logger.LogInfo("Recenter hotkey pressed: Head tracking recentered to current position");
        }
    }
}
