using System;
using CameraUnlock.Core.Tracking;
using SubnauticaHeadTracking.Config;

namespace SubnauticaHeadTracking.State
{
    /// <summary>What the mod starts with: whether head tracking is on, the tracking mode and the yaw mode.</summary>
    public sealed class StartupState
    {
        public StartupState(bool enabled, TrackingMode mode, bool worldSpaceYaw)
        {
            Enabled = enabled;
            Mode = mode;
            WorldSpaceYaw = worldSpaceYaw;
        }

        public bool Enabled { get; }
        public TrackingMode Mode { get; }
        public bool WorldSpaceYaw { get; }

        public static StartupState From(SubnauticaConfig config)
        {
            TrackingMode? mode = TrackingModeChannels.Decode(config.RotationEnabled, config.PositionEnabled);
            if (mode == null)
            {
                throw new InvalidOperationException("RotationEnabled and PositionEnabled are both false, "
                    + "which the config table reads as their defaults");
            }
            return new StartupState(config.EnableOnStartup, mode.Value, config.WorldSpaceYaw);
        }
    }
}
