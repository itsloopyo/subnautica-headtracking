using UnityEngine;
using BepInEx.Logging;
using CameraUnlock.Core.Protocol;

namespace SubnauticaHeadTracking.Input
{
    /// <summary>
    /// Monitors keyboard input for toggle and recenter hotkeys.
    /// Called from HeadTrackingPlugin.Update() every frame.
    /// </summary>
    public static class HotkeyHandler
    {
        private static ManualLogSource Logger => HeadTrackingPlugin.ModLogger;
        private static bool _hasLoggedFirstCheck = false;

        private static KeyCode _cachedToggleHotkey;
        private static KeyCode _cachedRecenterHotkey;
        private static KeyCode _cachedPositionToggleHotkey;
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
                _cachedPositionToggleHotkey = Config.ConfigurationManager.PositionToggleHotkey.Value;
                _cachedCyclePortHotkey = Config.ConfigurationManager.CyclePortHotkey.Value;
                _cacheInitialized = true;
            }

            if (UnityEngine.Input.GetKeyDown(_cachedToggleHotkey))
            {
                HandleToggleHotkey();
            }

            if (UnityEngine.Input.GetKeyDown(_cachedRecenterHotkey))
            {
                HandleRecenterHotkey();
            }

            if (UnityEngine.Input.GetKeyDown(_cachedPositionToggleHotkey))
            {
                HandlePositionToggleHotkey();
            }

            if (UnityEngine.Input.GetKeyDown(_cachedCyclePortHotkey))
            {
                HeadTrackingPlugin.CyclePort();
            }
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
        /// Handles the position toggle hotkey press.
        /// Toggles positional tracking on/off.
        /// </summary>
        private static void HandlePositionToggleHotkey()
        {
            Camera.CameraRotationApplicator.PositionEnabled = !Camera.CameraRotationApplicator.PositionEnabled;
            Logger.LogInfo($"Position tracking {(Camera.CameraRotationApplicator.PositionEnabled ? "enabled" : "disabled")}");
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
