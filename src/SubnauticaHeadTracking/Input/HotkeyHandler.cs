using System;
using BepInEx.Logging;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Unity.Extensions;
using SubnauticaHeadTracking.Config;

namespace SubnauticaHeadTracking.Input
{
    /// <summary>
    /// Monitors keyboard input for the four mod hotkeys. Each is a key list from
    /// CameraUnlock.ini, its Ctrl+Shift chord an ordinary item of the list.
    /// Called from HeadTrackingPlugin's per-frame camera callback.
    /// </summary>
    public static class HotkeyHandler
    {
        private static ManualLogSource Logger => HeadTrackingPlugin.ModLogger;
        private static bool _hasLoggedFirstCheck = false;

        private static KeyBinding[] _toggle;
        private static KeyBinding[] _cycleTrackingMode;
        private static KeyBinding[] _yawMode;
        private static KeyBinding[] _cyclePort;

        public static void Initialize(SubnauticaConfig config)
        {
            _toggle = Parse(config.ToggleKeyName);
            _cycleTrackingMode = Parse(config.CycleTrackingModeKeyName);
            _yawMode = Parse(config.YawModeKeyName);
            _cyclePort = Parse(config.CyclePortKeyName);
        }

        /// <summary>
        /// Checks for hotkey presses and executes corresponding actions.
        /// </summary>
        public static void CheckHotkeys()
        {
            if (!_hasLoggedFirstCheck)
            {
                Logger.LogInfo("HotkeyHandler: First check - hotkey system active");
                _hasLoggedFirstCheck = true;
            }

            if (KeyBindingInput.IsTriggered(_toggle))
            {
                HandleToggleHotkey();
            }

            if (KeyBindingInput.IsTriggered(_cycleTrackingMode))
            {
                State.TrackingState.CycleMode();
            }

            if (KeyBindingInput.IsTriggered(_yawMode))
            {
                State.TrackingState.ToggleYawMode();
            }

            if (KeyBindingInput.IsTriggered(_cyclePort))
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

        // The table's hotkey codec has already read each list, so a failure here is a
        // disagreement between it and KeyBindings, not a player's typo.
        private static KeyBinding[] Parse(string text)
        {
            if (!KeyBindings.TryParse(text, out KeyBinding[] bindings, out string error))
            {
                throw new InvalidOperationException("Hotkey list '" + text + "' does not parse: " + error);
            }
            return bindings;
        }
    }
}
