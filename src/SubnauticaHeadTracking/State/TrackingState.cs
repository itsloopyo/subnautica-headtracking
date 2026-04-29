using BepInEx.Logging;

namespace SubnauticaHeadTracking.State
{
    public enum TrackingMode
    {
        Full = 0,
        RotationOnly = 1,
        PositionOnly = 2
    }

    /// <summary>
    /// Manages tracking enable/disable state.
    /// Static class for global state access from both hotkey handler and camera applicator.
    /// </summary>
    public static class TrackingState
    {
        private static ManualLogSource Logger => HeadTrackingPlugin.ModLogger;

        /// <summary>
        /// Gets whether head tracking is currently enabled.
        /// Defaults to true (tracking enabled) on game launch.
        /// </summary>
        public static bool IsEnabled { get; private set; } = true;

        public static TrackingMode Mode { get; private set; } = TrackingMode.Full;

        public static bool IsRotationEnabled => Mode != TrackingMode.PositionOnly;
        public static bool IsPositionEnabled => Mode != TrackingMode.RotationOnly;

        /// <summary>
        /// Toggles tracking enabled/disabled state.
        /// </summary>
        public static void ToggleTracking()
        {
            IsEnabled = !IsEnabled;
            Logger.LogInfo($"Head tracking {(IsEnabled ? "ENABLED" : "DISABLED")}");

            if (!IsEnabled)
            {
                Logger.LogInfo("Camera control returned to mouse/keyboard");
            }
        }

        /// <summary>
        /// Advances the three-state tracking-mode cycle:
        /// Full → RotationOnly (position disabled) → PositionOnly (rotation disabled) → Full.
        /// </summary>
        public static void CycleMode()
        {
            Mode = (TrackingMode)(((int)Mode + 1) % 3);
            string desc = Mode switch
            {
                TrackingMode.Full => "rotation + position",
                TrackingMode.RotationOnly => "rotation only (position disabled)",
                TrackingMode.PositionOnly => "position only (rotation disabled)",
                _ => Mode.ToString()
            };
            Logger.LogInfo($"Tracking mode: {desc}");
        }
    }
}
