using System;
using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;

namespace SubnauticaHeadTracking.Config
{
    /// <summary>The config owner's options, for the mod and its tests alike.</summary>
    public static class SubnauticaConfigOwner
    {
        public const string FileName = "CameraUnlock.ini";
        public const string LegacyFileName = PluginInfo.PLUGIN_GUID + ".cfg";

        /// <param name="folder">BepInEx\config, which holds both files.</param>
        /// <param name="legacyConfigFor">The BepInEx ConfigFile of the legacy file at the path given.</param>
        /// <param name="defaults"><see cref="DefaultsFile.PerUser"/> in the mod.</param>
        public static ConfigOwnerOptions<SubnauticaConfig> Options(string folder, Func<string, ConfigFile> legacyConfigFor,
            DefaultsFile defaults)
        {
            return new ConfigOwnerOptions<SubnauticaConfig>
            {
                Path = Path.Combine(folder, FileName),
                Table = SubnauticaConfig.Table(),
                Header = new RenderHeader(SubnauticaConfig.DisplayName),
                Import = SubnauticaConfigImport.Create(legacyConfigFor),
                LegacySourcePath = Path.Combine(folder, LegacyFileName),
                Defaults = defaults,
            };
        }
    }
}
