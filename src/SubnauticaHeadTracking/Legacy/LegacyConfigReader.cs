using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using UnityEngine;

namespace SubnauticaHeadTracking.Legacy
{
    /// <summary>
    /// Frozen: the settings reader of v1.4.0, the last build before the canonical config. Its Bind
    /// calls are that build's, section, key, type, default, description and acceptable values
    /// unchanged, so a player's .cfg is imported exactly as the build they ran read it. It fills a
    /// <see cref="LegacyConfig"/> and writes nothing. Never edit this file.
    /// </summary>
    public static class LegacyConfigReader
    {
        /// <summary>Every section and key <see cref="Capture"/> binds.</summary>
        public static readonly LegacyKey[] Keys =
        {
            new LegacyKey("Network", "UdpPort"),
            new LegacyKey("Network", "BindAddress"),
            new LegacyKey("Sensitivity", "Yaw"),
            new LegacyKey("Sensitivity", "Pitch"),
            new LegacyKey("Sensitivity", "Roll"),
            new LegacyKey("Deadzone", "Yaw"),
            new LegacyKey("Deadzone", "Pitch"),
            new LegacyKey("Deadzone", "Roll"),
            new LegacyKey("Inversion", "YawInvert"),
            new LegacyKey("Inversion", "PitchInvert"),
            new LegacyKey("Inversion", "RollInvert"),
            new LegacyKey("Hotkeys", "Toggle"),
            new LegacyKey("Hotkeys", "CycleTrackingMode"),
            new LegacyKey("Hotkeys", "ToggleYawMode"),
            new LegacyKey("Hotkeys", "CyclePort"),
            new LegacyKey("Advanced", "LocalSmoothing"),
            new LegacyKey("Advanced", "RemoteSmoothing"),
            new LegacyKey("Position", "PositionEnabled"),
            new LegacyKey("Position", "PositionSensitivityX"),
            new LegacyKey("Position", "PositionSensitivityY"),
            new LegacyKey("Position", "PositionSensitivityZ"),
            new LegacyKey("Position", "PositionLimitX"),
            new LegacyKey("Position", "PositionLimitY"),
            new LegacyKey("Position", "PositionLimitYDown"),
            new LegacyKey("Position", "PositionLimitZ"),
            new LegacyKey("Position", "PositionLimitZBack"),
        };

        /// <summary>
        /// Reads <paramref name="config"/>'s file as v1.4.0 did. BepInEx's ConfigFile read the
        /// file in its constructor, before the caller held it, so it is read again here, with
        /// SaveOnConfigSet off first so that no Bind writes it. A missing file is not read, and
        /// every setting keeps v1.4.0's default, as that build ran without one.
        /// </summary>
        public static LegacyConfig Read(ConfigFile config, out bool fileFound)
        {
            config.SaveOnConfigSet = false;
            fileFound = File.Exists(config.ConfigFilePath);
            if (fileFound) config.Reload();
            return Capture(config);
        }

        /// <summary>
        /// v1.4.0's Bind calls on <paramref name="config"/>, without reading its file. An entry
        /// already bound keeps the value it holds.
        /// </summary>
        public static LegacyConfig Capture(ConfigFile config)
        {
            var c = new LegacyConfig();

            c.UdpPort = config.Bind(
                "Network",
                "UdpPort",
                Frozen.UDP_DEFAULT_PORT,
                new ConfigDescription(
                    "UDP port for OpenTrack packets. Restart required after change.",
                    new AcceptableValueRange<int>(1024, 65535)
                )
            ).Value;

            c.BindAddress = config.Bind(
                "Network",
                "BindAddress",
                Frozen.UDP_DEFAULT_ADDRESS,
                "IP address to bind UDP listener to. Use 127.0.0.1 for local OpenTrack, or 0.0.0.0 to accept from any network interface. Restart required after change."
            ).Value;

            c.YawSensitivity = config.Bind(
                "Sensitivity",
                "Yaw",
                Frozen.DEFAULT_YAW_SENSITIVITY,
                new ConfigDescription(
                    "Yaw (left/right) sensitivity multiplier. Higher values = more sensitive.",
                    new AcceptableValueRange<float>(Frozen.MIN_SENSITIVITY, Frozen.MAX_SENSITIVITY)
                )
            ).Value;

            c.PitchSensitivity = config.Bind(
                "Sensitivity",
                "Pitch",
                Frozen.DEFAULT_PITCH_SENSITIVITY,
                new ConfigDescription(
                    "Pitch (up/down) sensitivity multiplier. Higher values = more sensitive.",
                    new AcceptableValueRange<float>(Frozen.MIN_SENSITIVITY, Frozen.MAX_SENSITIVITY)
                )
            ).Value;

            c.RollSensitivity = config.Bind(
                "Sensitivity",
                "Roll",
                Frozen.DEFAULT_ROLL_SENSITIVITY,
                new ConfigDescription(
                    "Roll (tilt) sensitivity multiplier. Higher values = more sensitive.",
                    new AcceptableValueRange<float>(Frozen.MIN_SENSITIVITY, Frozen.MAX_SENSITIVITY)
                )
            ).Value;

            c.YawDeadzone = config.Bind(
                "Deadzone",
                "Yaw",
                Frozen.DEFAULT_YAW_DEADZONE,
                new ConfigDescription(
                    "Yaw deadzone in degrees. Rotation below this threshold is ignored. Helps reduce jitter.",
                    new AcceptableValueRange<float>(Frozen.MIN_DEADZONE, Frozen.MAX_DEADZONE)
                )
            ).Value;

            c.PitchDeadzone = config.Bind(
                "Deadzone",
                "Pitch",
                Frozen.DEFAULT_PITCH_DEADZONE,
                new ConfigDescription(
                    "Pitch deadzone in degrees. Rotation below this threshold is ignored. Helps reduce jitter.",
                    new AcceptableValueRange<float>(Frozen.MIN_DEADZONE, Frozen.MAX_DEADZONE)
                )
            ).Value;

            c.RollDeadzone = config.Bind(
                "Deadzone",
                "Roll",
                Frozen.DEFAULT_ROLL_DEADZONE,
                new ConfigDescription(
                    "Roll deadzone in degrees. Rotation below this threshold is ignored. Helps reduce jitter.",
                    new AcceptableValueRange<float>(Frozen.MIN_DEADZONE, Frozen.MAX_DEADZONE)
                )
            ).Value;

            c.YawInvert = config.Bind(
                "Inversion",
                "YawInvert",
                false,
                "Invert yaw axis. Enable if head turning left moves camera right."
            ).Value;

            c.PitchInvert = config.Bind(
                "Inversion",
                "PitchInvert",
                true,
                "Invert pitch axis. Enable if looking up moves camera down."
            ).Value;

            c.RollInvert = config.Bind(
                "Inversion",
                "RollInvert",
                false,
                "Invert roll axis. Enable if tilting head left tilts camera right."
            ).Value;

            c.ToggleHotkey = config.Bind(
                "Hotkeys",
                "Toggle",
                KeyCode.End,
                "Hotkey to enable/disable head tracking."
            ).Value;

            c.CycleTrackingModeHotkey = config.Bind(
                "Hotkeys",
                "CycleTrackingMode",
                KeyCode.PageUp,
                "Hotkey to cycle tracking mode: full → rotation only (position disabled) → position only (rotation disabled) → full."
            ).Value;

            c.ToggleYawModeHotkey = config.Bind(
                "Hotkeys",
                "ToggleYawMode",
                KeyCode.Insert,
                "Hotkey to toggle yaw between camera-local and world-space (gravity-aligned)."
            ).Value;

            c.CyclePortHotkey = config.Bind(
                "Hotkeys",
                "CyclePort",
                KeyCode.PageDown,
                "Cycle UDP listen port through 4242-4245. For couch co-op with multiple game instances on the same PC."
            ).Value;

            // Smoothing covers both rotation and position. The value used is selected
            // per connection from the packet source address, so a local tracker and a
            // phone on WiFi each get their own setting without a restart.
            c.LocalSmoothing = config.Bind(
                "Advanced",
                "LocalSmoothing",
                Frozen.DEFAULT_LOCAL_SMOOTHING,
                new ConfigDescription(
                    "Smoothing applied when the tracker runs on this machine (loopback). 0 = no smoothing, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            ).Value;

            c.RemoteSmoothing = config.Bind(
                "Advanced",
                "RemoteSmoothing",
                Frozen.DEFAULT_REMOTE_SMOOTHING,
                new ConfigDescription(
                    "Smoothing applied when the tracker is a remote device on the network. 0 = no smoothing, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            ).Value;

            // Position section
            c.PositionEnabled = config.Bind(
                "Position",
                "PositionEnabled",
                true,
                "Enable positional tracking (lean in/out/side-to-side)"
            ).Value;

            c.PositionSensitivityX = config.Bind(
                "Position",
                "PositionSensitivityX",
                2.0f,
                new ConfigDescription(
                    "Multiplier for lateral (left/right) position",
                    new AcceptableValueRange<float>(0f, 3.0f)
                )
            ).Value;

            c.PositionSensitivityY = config.Bind(
                "Position",
                "PositionSensitivityY",
                2.0f,
                new ConfigDescription(
                    "Multiplier for vertical (up/down) position",
                    new AcceptableValueRange<float>(0f, 3.0f)
                )
            ).Value;

            c.PositionSensitivityZ = config.Bind(
                "Position",
                "PositionSensitivityZ",
                2.0f,
                new ConfigDescription(
                    "Multiplier for depth (forward/back) position",
                    new AcceptableValueRange<float>(0f, 3.0f)
                )
            ).Value;

            c.PositionLimitX = config.Bind(
                "Position",
                "PositionLimitX",
                0.30f,
                new ConfigDescription(
                    "Maximum lateral displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)
                )
            ).Value;

            c.PositionLimitY = config.Bind(
                "Position",
                "PositionLimitY",
                0.15f,
                new ConfigDescription(
                    "Maximum upward vertical displacement in meters",
                    new AcceptableValueRange<float>(0f, 0.5f)
                )
            ).Value;

            c.PositionLimitYDown = config.Bind(
                "Position",
                "PositionLimitYDown",
                0.01f,
                new ConfigDescription(
                    "Maximum downward vertical displacement in meters. Keep low to avoid seeing your own neck.",
                    new AcceptableValueRange<float>(0f, 0.5f)
                )
            ).Value;

            c.PositionLimitZ = config.Bind(
                "Position",
                "PositionLimitZ",
                0.40f,
                new ConfigDescription(
                    "Maximum forward depth displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)
                )
            ).Value;

            c.PositionLimitZBack = config.Bind(
                "Position",
                "PositionLimitZBack",
                0.02f,
                new ConfigDescription(
                    "Maximum backward depth displacement in meters. Keep low to avoid seeing your own neck.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)
                )
            ).Value;

            return c;
        }

        // v1.4.0's PluginInfo constants, which its Bind calls named.
        private static class Frozen
        {
            public const int UDP_DEFAULT_PORT = 4242;
            public const string UDP_DEFAULT_ADDRESS = "0.0.0.0";

            public const float DEFAULT_YAW_SENSITIVITY = 1.0f;
            public const float DEFAULT_PITCH_SENSITIVITY = 1.0f;
            public const float DEFAULT_ROLL_SENSITIVITY = 1.0f;

            public const float DEFAULT_LOCAL_SMOOTHING = 0.0f;
            public const float DEFAULT_REMOTE_SMOOTHING = 0.15f;

            public const float DEFAULT_YAW_DEADZONE = 0.0f;
            public const float DEFAULT_PITCH_DEADZONE = 0.0f;
            public const float DEFAULT_ROLL_DEADZONE = 0.0f;

            public const float MIN_SENSITIVITY = 0.1f;
            public const float MAX_SENSITIVITY = 3.0f;

            public const float MIN_DEADZONE = 0.0f;
            public const float MAX_DEADZONE = 10.0f;
        }
    }
}
