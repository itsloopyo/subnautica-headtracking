using BepInEx.Logging;
using CameraUnlock.Core.Tracking;
using SubnauticaHeadTracking.Config;

namespace SubnauticaHeadTracking.State
{
    /// <summary>
    /// Tracking enable/disable state, the tracking mode and the yaw mode.
    /// Static class for global state access from both hotkey handler and camera applicator.
    /// </summary>
    public static class TrackingState
    {
        private static ManualLogSource Logger => HeadTrackingPlugin.ModLogger;

        /// <summary>Whether head tracking is on. The toggle changes this session only.</summary>
        public static bool IsEnabled { get; private set; }

        public static TrackingMode Mode { get; private set; }

        public static bool IsRotationEnabled => Mode != TrackingMode.PositionOnly;
        public static bool IsPositionEnabled => Mode != TrackingMode.RotationOnly;

        /// <summary>
        /// When true, yaw rotates around world up (gravity) instead of the camera-local
        /// up axis. Camera-local is horizon-independent and so behaves correctly while
        /// swimming at any orientation.
        /// </summary>
        public static bool WorldSpaceYaw { get; private set; }

        public static void Initialize(SubnauticaConfig config)
        {
            StartupState startup = StartupState.From(config);
            IsEnabled = startup.Enabled;
            Mode = startup.Mode;
            WorldSpaceYaw = startup.WorldSpaceYaw;
        }

        /// <summary>
        /// Toggles tracking enabled/disabled state. Never saved: EnableOnStartup decides the next start.
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
        /// Advances the three-state tracking-mode cycle and saves it:
        /// rotation and position → rotation only → position only → rotation and position.
        /// </summary>
        public static void CycleMode()
        {
            Mode = (TrackingMode)(((int)Mode + 1) % 3);
            string desc = Mode switch
            {
                TrackingMode.RotationAndPosition => "rotation + position",
                TrackingMode.RotationOnly => "rotation only (position disabled)",
                TrackingMode.PositionOnly => "position only (rotation disabled)",
                _ => Mode.ToString()
            };
            Logger.LogInfo($"Tracking mode: {desc}");

            TrackingModeChannels.Encode(Mode, out bool rotation, out bool position);
            Settings.Save(c =>
            {
                c.RotationEnabled = rotation;
                c.PositionEnabled = position;
            });
        }

        /// <summary>
        /// Toggles yaw between camera-local and world-space (gravity-aligned) and saves it.
        /// </summary>
        public static void ToggleYawMode()
        {
            WorldSpaceYaw = !WorldSpaceYaw;
            Logger.LogInfo($"Yaw mode: {(WorldSpaceYaw ? "world-space (gravity-aligned)" : "camera-local")}");

            bool worldSpaceYaw = WorldSpaceYaw;
            Settings.Save(c => c.WorldSpaceYaw = worldSpaceYaw);
        }
    }
}
