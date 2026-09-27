using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using SubnauticaHeadTracking.Camera;
using SubnauticaHeadTracking.Legacy;
using UnityEngine;

namespace SubnauticaHeadTracking.Config
{
    /// <summary>
    /// The import of the .cfg every build before CameraUnlock.ini read: the frozen v1.4.0
    /// reader, then a map of what it read into a <see cref="SubnauticaConfig"/>, field by field.
    /// </summary>
    public static class SubnauticaConfigImport
    {
        /// <param name="configFor">The BepInEx ConfigFile of the legacy file at the path given:
        /// the plugin's own Config in the mod.</param>
        public static LegacyImport<SubnauticaConfig> Create(Func<string, ConfigFile> configFor)
        {
            if (configFor == null) throw new ArgumentNullException("configFor");
            return new LegacyImport<SubnauticaConfig>((input, config) =>
            {
                ConfigFile file = configFor(input.Path);
                LegacyConfig legacy = LegacyConfigReader.Read(file, out bool found);
                // Unbind what the import bound, so BepInEx's ConfigurationManager lists nothing.
                file.Clear();
                return Map(legacy, found, config);
            }, LegacyConfigReader.Keys);
        }

        /// <summary>
        /// Everything v1.4.0 ran on, in the new config. Its startup state was fixed in code:
        /// head tracking on, rotation and position, yaw around the camera's own up axis. Its
        /// [Position] PositionEnabled and [Network] BindAddress were read and never used, so
        /// neither is carried. The sensitivities, deadzones and inversions are pose shaping:
        /// the values it shipped are now the mod's own axis code (<see cref="PoseConversion"/>),
        /// and one a player changed is dropped. A row the player left at v1.4.0's default, and
        /// the startup state no player could change, follow Defaults.ini, and a port key left at
        /// v1.4.0's default takes this version's.
        /// </summary>
        public static ImportResult Map(LegacyConfig legacy, bool found, SubnauticaConfig config)
        {
            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();

            LegacyPoseShaping.Record(legacy.YawSensitivity, 1f, "Sensitivity", "Yaw", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, 1f, "Sensitivity", "Pitch", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, 1f, "Sensitivity", "Roll", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.YawDeadzone, 0f, "Deadzone", "Yaw", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchDeadzone, 0f, "Deadzone", "Pitch", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollDeadzone, 0f, "Deadzone", "Roll", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.YawInvert, PoseConversion.InvertYaw, "Inversion", "YawInvert", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchInvert, PoseConversion.InvertPitch, "Inversion", "PitchInvert", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollInvert, PoseConversion.InvertRoll, "Inversion", "RollInvert", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityX, PoseConversion.PositionScale, "Position", "PositionSensitivityX", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityY, PoseConversion.PositionScale, "Position", "PositionSensitivityY", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityZ, PoseConversion.PositionScale, "Position", "PositionSensitivityZ", poseShaping, dropped);

            config.UdpPort = legacy.UdpPort;
            config.EnableOnStartup = true;
            config.WorldSpaceYaw = false;
            config.RotationEnabled = true;
            config.PositionEnabled = true;
            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            PositionSettings p = config.Position;
            config.Position = new PositionSettings(
                sensitivityX: p.SensitivityX, sensitivityY: p.SensitivityY, sensitivityZ: p.SensitivityZ,
                limitX: legacy.PositionLimitX, limitY: legacy.PositionLimitY, limitYDown: legacy.PositionLimitYDown,
                limitZ: legacy.PositionLimitZ, limitZBack: legacy.PositionLimitZBack,
                localSmoothing: legacy.LocalSmoothing, remoteSmoothing: legacy.RemoteSmoothing,
                invertX: p.InvertX, invertY: p.InvertY, invertZ: p.InvertZ);

            var unnamed = new List<string>();
            config.ToggleKeyName = KeyList(legacy.ToggleHotkey, KeyCode.Y, "Toggle", dropped, unnamed);
            config.CycleTrackingModeKeyName = KeyList(legacy.CycleTrackingModeHotkey, KeyCode.G, "CycleTrackingMode", dropped, unnamed);
            config.YawModeKeyName = KeyList(legacy.ToggleYawModeHotkey, KeyCode.U, "ToggleYawMode", dropped, unnamed);
            config.CyclePortKeyName = KeyList(legacy.CyclePortHotkey, KeyCode.H, "CyclePort", dropped, unnamed);
            LegacyConfig shipped = Shipped();
            bool portUnchanged = legacy.CyclePortHotkey == shipped.CyclePortHotkey;
            if (portUnchanged) config.CyclePortKeyName = new SubnauticaConfig().CyclePortKeyName;
            if (unnamed.Count > 0)
            {
                return ImportResult.Undecodable(string.Join(" and ", unnamed.ToArray())
                    + " names no key this version can write");
            }

            var follows = new LegacyFollowsDefaultsIni();
            follows.Setting(ConfigConcepts.UdpPort, legacy.UdpPort, shipped.UdpPort);
            follows.NotInLegacy(ConfigConcepts.EnableOnStartup);
            follows.TrackingMode(true);
            follows.Setting(ConfigConcepts.LocalSmoothing, legacy.LocalSmoothing, shipped.LocalSmoothing);
            follows.Setting(ConfigConcepts.RemoteSmoothing, legacy.RemoteSmoothing, shipped.RemoteSmoothing);
            follows.Setting(ConfigConcepts.PositionLimitX, legacy.PositionLimitX, shipped.PositionLimitX);
            follows.Setting(ConfigConcepts.PositionLimitY, legacy.PositionLimitY, shipped.PositionLimitY);
            follows.Setting(ConfigConcepts.PositionLimitYDown, legacy.PositionLimitYDown, shipped.PositionLimitYDown);
            follows.Setting(ConfigConcepts.PositionLimitZ, legacy.PositionLimitZ, shipped.PositionLimitZ);
            follows.Setting(ConfigConcepts.PositionLimitZBack, legacy.PositionLimitZBack, shipped.PositionLimitZBack);
            follows.Setting(ConfigConcepts.ToggleKey, legacy.ToggleHotkey, shipped.ToggleHotkey);
            follows.Setting(ConfigConcepts.CycleTrackingModeKey, legacy.CycleTrackingModeHotkey, shipped.CycleTrackingModeHotkey);
            // The yaw mode's built-in keys are v1.4.0's port keys, PageDown and Ctrl+Shift+H. A port
            // key the player changed keeps Ctrl+Shift+H, so the yaw mode then keeps v1.4.0's keys.
            follows.Setting(ConfigConcepts.YawModeKey, legacy.ToggleYawModeHotkey == shipped.ToggleYawModeHotkey && portUnchanged);

            return found
                ? ImportResult.Imported(dropped, poseShaping, follows.Concepts)
                : ImportResult.Absent(dropped, poseShaping, follows.Concepts);
        }

        // v1.4.0's defaults: its Bind calls on a ConfigFile with no file behind it, which reads
        // nothing and, with SaveOnConfigSet off, writes nothing.
        private static LegacyConfig Shipped()
        {
            var file = new ConfigFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cfg"), false);
            file.SaveOnConfigSet = false;
            return LegacyConfigReader.Capture(file);
        }

        // v1.4.0 fired an action on its configured key, which never fires as KeyCode.None, or on
        // the Ctrl+Shift chord written in code. A key on Ctrl, Shift or Alt alone is unbound and
        // logged (N3). A number BepInEx read that no KeyCode names has no key name to write, so
        // the list keeps the chord alone and the key is reported.
        private static string KeyList(KeyCode key, KeyCode chordLetter, string legacyKey, List<DroppedValue> dropped,
            List<string> unnamed)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            string plain;
            try
            {
                plain = LegacyNormalisations.KeyCodeToBindings((int)key, "Hotkeys", legacyKey, dropped);
            }
            catch (ArgumentException)
            {
                unnamed.Add("[Hotkeys] " + legacyKey + "=" + (int)key);
                return chord;
            }
            return plain.Length == 0 ? chord : plain + ", " + chord;
        }
    }
}
