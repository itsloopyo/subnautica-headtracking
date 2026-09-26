using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CameraUnlock.Core.Config;

namespace SubnauticaHeadTracking.Config
{
    /// <summary>
    /// The settings the mod runs on. They live in BepInEx\config\CameraUnlock.ini, read and
    /// written by core's config owner, with the rows set to default following the player's
    /// Defaults.ini. Nothing is bound through BepInEx's ConfigFile, so BepInEx's
    /// ConfigurationManager does not list them. The .cfg every earlier build read is imported
    /// once, while CameraUnlock.ini is absent, and never written.
    /// </summary>
    internal static class Settings
    {
        // A load or save takes milliseconds; a holder past this is hung, not busy.
        private const int LockTimeoutMs = 5000;
        private const string LockName = PluginInfo.PLUGIN_GUID + ".config";

        private static ConfigOwner<SubnauticaConfig> _owner;
        private static ConfigFileLock _lock;
        private static ManualLogSource _log;
        private static string _path;

        public static SubnauticaConfig Current { get; private set; }

        /// <param name="pluginConfig">The plugin's own ConfigFile, which BaseUnityPlugin built on
        /// the legacy .cfg: the import reads it through that.</param>
        public static void Load(ConfigFile pluginConfig, ManualLogSource log)
        {
            _log = log;
            ConfigOwnerOptions<SubnauticaConfig> options = SubnauticaConfigOwner.Options(Paths.ConfigPath, path =>
            {
                if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(pluginConfig.ConfigFilePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("the legacy import was asked for " + path
                        + ", and the plugin's ConfigFile is " + pluginConfig.ConfigFilePath);
                }
                return pluginConfig;
            }, DefaultsFile.PerUser());
            _path = options.Path;
            _owner = new ConfigOwner<SubnauticaConfig>(options);
            _lock = new ConfigFileLock(LockName, LockTimeoutMs);

            ConfigLoadResult<SubnauticaConfig> loaded = null;
            if (!_lock.TryRun(() => loaded = _owner.Load()))
            {
                throw new TimeoutException("Another process held the mutex " + LockName + " for over "
                    + LockTimeoutMs + "ms, so " + _path + " was not loaded.");
            }
            Current = loaded.Config;

            // The owner writes each diagnostic as "<path>: <description>" among lines that only
            // report what it did, so the complaints are picked out by their text.
            var complaints = new HashSet<string>();
            foreach (CanonicalDiagnostic diagnostic in loaded.Diagnostics) complaints.Add(_path + ": " + diagnostic.Describe());
            bool usable = loaded.Status == ConfigLoadStatus.Canonical || loaded.Status == ConfigLoadStatus.Migrated
                          || loaded.Status == ConfigLoadStatus.Created;
            foreach (string line in loaded.Log)
            {
                if (usable && !complaints.Contains(line)) _log.LogInfo(line);
                else _log.LogWarning(line);
            }
            if (loaded.Reason.Length > 0) _log.LogWarning(loaded.Reason);
            _log.LogInfo("Config " + _path + ": " + loaded.Status + ". Loads and saves hold the mutex " + LockName
                         + ", so a second copy of the game started from this folder cannot load or save it in between.");
        }

        /// <summary>
        /// Called after the new value is applied. A save that fails is logged, and the session
        /// keeps the new value.
        /// </summary>
        public static void Save(Action<SubnauticaConfig> change)
        {
            ConfigSaveResult saved = null;
            if (!_lock.TryRun(() => saved = _owner.Save(change)))
            {
                _log.LogWarning("Setting not saved: another process held " + LockName + " for over " + LockTimeoutMs
                                + "ms. The change applies to this session only.");
                return;
            }
            if (saved.Status == ConfigSaveStatus.Saved)
            {
                // A row that held default and now holds a value, so it no longer follows Defaults.ini.
                foreach (string line in saved.Log) _log.LogInfo(line);
                return;
            }
            foreach (string line in saved.Log) _log.LogWarning(line);
            _log.LogWarning(_path + ": " + saved.Status + ": " + saved.Reason + " The change applies to this session only.");
        }
    }
}
