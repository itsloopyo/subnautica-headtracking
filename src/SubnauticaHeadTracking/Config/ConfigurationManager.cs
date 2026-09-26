using BepInEx.Configuration;
using SubnauticaHeadTracking.Legacy;

namespace SubnauticaHeadTracking.Config
{
    /// <summary>
    /// The settings the mod runs on, read from BepInEx/config/com.cameraunlock.subnautica.headtracking.cfg
    /// by the frozen v1.4.0 reader.
    /// </summary>
    public static class ConfigurationManager
    {
        public static LegacyConfig Values { get; private set; }

        /// <summary>
        /// Called once from HeadTrackingPlugin.Awake().
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            Values = LegacyConfigReader.Read(config, out _);

            // v1.4.0 saved the file after every Bind; one save after them all leaves the same file.
            config.SaveOnConfigSet = true;
            config.Save();

            // An edit through BepInEx's ConfigurationManager applies without a restart, as before.
            config.SettingChanged += (sender, args) => Values = LegacyConfigReader.Capture(config);
        }
    }
}
