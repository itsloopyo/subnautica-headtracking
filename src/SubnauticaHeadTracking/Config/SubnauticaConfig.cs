using CameraUnlock.Core.Config;

namespace SubnauticaHeadTracking.Config
{
    /// <summary>
    /// Everything the mod reads from BepInEx\config\CameraUnlock.ini. Unity-free, so the tests
    /// compile it and hold the committed file to it.
    /// </summary>
    public sealed class SubnauticaConfig : HeadTrackingConfigData
    {
        public const string DisplayName = "Subnautica";

        public SubnauticaConfig()
        {
            // Swimming has no stable up, so yaw starts around the camera's own up axis
            // (data/config-format.json per_game).
            WorldSpaceYaw = false;
        }

        public string CyclePortKeyName { get; set; } = "Ctrl+Shift+J";

        public static ConfigTable<SubnauticaConfig> Table()
        {
            return HeadTrackingConfigTable.Create<SubnauticaConfig>(
                    ConfigConcepts.UdpPort,
                    ConfigConcepts.EnableOnStartup,
                    ConfigConcepts.WorldSpaceYaw,
                    ConfigConcepts.RotationEnabled,
                    ConfigConcepts.LocalSmoothing,
                    ConfigConcepts.RemoteSmoothing,
                    ConfigConcepts.PositionEnabled,
                    ConfigConcepts.PositionLimitX,
                    ConfigConcepts.PositionLimitY,
                    ConfigConcepts.PositionLimitYDown,
                    ConfigConcepts.PositionLimitZ,
                    ConfigConcepts.PositionLimitZBack,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.WorldSpaceYaw).PerGame().Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable()
                .Local("Hotkeys", "CyclePortKey", c => c.CyclePortKeyName, (c, v) => c.CyclePortKeyName = v,
                    new HotkeyCodec(),
                    "Moves the tracker port to the next of 4242, 4243, 4244 and 4245, for a second copy\n" +
                    "of the game on this PC. The port goes back to UdpPort when the game restarts.");
        }
    }
}
