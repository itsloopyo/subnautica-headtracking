using BepInEx.Logging;

namespace SubnauticaHeadTracking.State
{
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
    }
}
