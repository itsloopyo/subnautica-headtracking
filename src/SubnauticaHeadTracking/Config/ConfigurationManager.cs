using BepInEx.Configuration;
using UnityEngine;

namespace SubnauticaHeadTracking.Config
{
    /// <summary>
    /// Manages all mod configuration settings with persistence via BepInEx.Configuration.
    /// All settings auto-save to BepInEx/config/com.cameraunlock.subnautica.headtracking.cfg as INI format.
    /// </summary>
    public static class ConfigurationManager
    {
        public static ConfigEntry<int> UdpPort { get; private set; }
        public static ConfigEntry<string> BindAddress { get; private set; }

        public static ConfigEntry<float> YawSensitivity { get; private set; }
        public static ConfigEntry<float> PitchSensitivity { get; private set; }
        public static ConfigEntry<float> RollSensitivity { get; private set; }

        public static ConfigEntry<float> YawDeadzone { get; private set; }
        public static ConfigEntry<float> PitchDeadzone { get; private set; }
        public static ConfigEntry<float> RollDeadzone { get; private set; }

        public static ConfigEntry<bool> YawInvert { get; private set; }
        public static ConfigEntry<bool> PitchInvert { get; private set; }
        public static ConfigEntry<bool> RollInvert { get; private set; }

        public static ConfigEntry<KeyCode> ToggleHotkey { get; private set; }
        public static ConfigEntry<KeyCode> RecenterHotkey { get; private set; }
        public static ConfigEntry<KeyCode> CycleTrackingModeHotkey { get; private set; }
        public static ConfigEntry<KeyCode> CyclePortHotkey { get; private set; }

        public static ConfigEntry<float> SmoothingFactor { get; private set; }

        // Position settings
        public static ConfigEntry<bool> PositionEnabled { get; private set; }
        public static ConfigEntry<float> PositionSensitivityX { get; private set; }
        public static ConfigEntry<float> PositionSensitivityY { get; private set; }
        public static ConfigEntry<float> PositionSensitivityZ { get; private set; }
        public static ConfigEntry<float> PositionLimitX { get; private set; }
        public static ConfigEntry<float> PositionLimitY { get; private set; }
        public static ConfigEntry<float> PositionLimitYDown { get; private set; }
        public static ConfigEntry<float> PositionLimitZ { get; private set; }
        public static ConfigEntry<float> PositionLimitZBack { get; private set; }
        public static ConfigEntry<float> PositionSmoothing { get; private set; }


        /// <summary>
        /// Initializes all configuration entries.
        /// Called once from HeadTrackingPlugin.Awake().
        /// </summary>
        /// <param name="config">BepInEx ConfigFile instance</param>
        public static void Initialize(ConfigFile config)
        {
            UdpPort = config.Bind(
                "Network",
                "UdpPort",
                PluginInfo.UDP_DEFAULT_PORT,
                new ConfigDescription(
                    "UDP port for OpenTrack packets. Restart required after change.",
                    new AcceptableValueRange<int>(1024, 65535)
                )
            );

            BindAddress = config.Bind(
                "Network",
                "BindAddress",
                PluginInfo.UDP_DEFAULT_ADDRESS,
                "IP address to bind UDP listener to. Use 127.0.0.1 for local OpenTrack, or 0.0.0.0 to accept from any network interface. Restart required after change."
            );

            YawSensitivity = config.Bind(
                "Sensitivity",
                "Yaw",
                PluginInfo.DEFAULT_YAW_SENSITIVITY,
                new ConfigDescription(
                    "Yaw (left/right) sensitivity multiplier. Higher values = more sensitive.",
                    new AcceptableValueRange<float>(PluginInfo.MIN_SENSITIVITY, PluginInfo.MAX_SENSITIVITY)
                )
            );

            PitchSensitivity = config.Bind(
                "Sensitivity",
                "Pitch",
                PluginInfo.DEFAULT_PITCH_SENSITIVITY,
                new ConfigDescription(
                    "Pitch (up/down) sensitivity multiplier. Higher values = more sensitive.",
                    new AcceptableValueRange<float>(PluginInfo.MIN_SENSITIVITY, PluginInfo.MAX_SENSITIVITY)
                )
            );

            RollSensitivity = config.Bind(
                "Sensitivity",
                "Roll",
                PluginInfo.DEFAULT_ROLL_SENSITIVITY,
                new ConfigDescription(
                    "Roll (tilt) sensitivity multiplier. Higher values = more sensitive.",
                    new AcceptableValueRange<float>(PluginInfo.MIN_SENSITIVITY, PluginInfo.MAX_SENSITIVITY)
                )
            );

            YawDeadzone = config.Bind(
                "Deadzone",
                "Yaw",
                PluginInfo.DEFAULT_YAW_DEADZONE,
                new ConfigDescription(
                    "Yaw deadzone in degrees. Rotation below this threshold is ignored. Helps reduce jitter.",
                    new AcceptableValueRange<float>(PluginInfo.MIN_DEADZONE, PluginInfo.MAX_DEADZONE)
                )
            );

            PitchDeadzone = config.Bind(
                "Deadzone",
                "Pitch",
                PluginInfo.DEFAULT_PITCH_DEADZONE,
                new ConfigDescription(
                    "Pitch deadzone in degrees. Rotation below this threshold is ignored. Helps reduce jitter.",
                    new AcceptableValueRange<float>(PluginInfo.MIN_DEADZONE, PluginInfo.MAX_DEADZONE)
                )
            );

            RollDeadzone = config.Bind(
                "Deadzone",
                "Roll",
                PluginInfo.DEFAULT_ROLL_DEADZONE,
                new ConfigDescription(
                    "Roll deadzone in degrees. Rotation below this threshold is ignored. Helps reduce jitter.",
                    new AcceptableValueRange<float>(PluginInfo.MIN_DEADZONE, PluginInfo.MAX_DEADZONE)
                )
            );

            YawInvert = config.Bind(
                "Inversion",
                "YawInvert",
                false,
                "Invert yaw axis. Enable if head turning left moves camera right."
            );

            PitchInvert = config.Bind(
                "Inversion",
                "PitchInvert",
                true,
                "Invert pitch axis. Enable if looking up moves camera down."
            );

            RollInvert = config.Bind(
                "Inversion",
                "RollInvert",
                false,
                "Invert roll axis. Enable if tilting head left tilts camera right."
            );

            ToggleHotkey = config.Bind(
                "Hotkeys",
                "Toggle",
                KeyCode.End,
                "Hotkey to enable/disable head tracking."
            );

            RecenterHotkey = config.Bind(
                "Hotkeys",
                "Recenter",
                KeyCode.Home,
                "Hotkey to recenter head tracking. Treats current head position as neutral."
            );

            CycleTrackingModeHotkey = config.Bind(
                "Hotkeys",
                "CycleTrackingMode",
                KeyCode.PageUp,
                "Hotkey to cycle tracking mode: full → rotation only (position disabled) → position only (rotation disabled) → full."
            );

            CyclePortHotkey = config.Bind(
                "Hotkeys",
                "CyclePort",
                KeyCode.PageDown,
                "Cycle UDP listen port through 4242-4245. For couch co-op with multiple game instances on the same PC."
            );

            SmoothingFactor = config.Bind(
                "Advanced",
                "SmoothingFactor",
                PluginInfo.DEFAULT_SMOOTHING_FACTOR,
                new ConfigDescription(
                    "Camera rotation smoothing factor. Higher values = faster response. 0 = instant (no smoothing).",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );

            // Position section
            PositionEnabled = config.Bind(
                "Position",
                "PositionEnabled",
                true,
                "Enable positional tracking (lean in/out/side-to-side)"
            );

            PositionSensitivityX = config.Bind(
                "Position",
                "PositionSensitivityX",
                2.0f,
                new ConfigDescription(
                    "Multiplier for lateral (left/right) position",
                    new AcceptableValueRange<float>(0f, 3.0f)
                )
            );

            PositionSensitivityY = config.Bind(
                "Position",
                "PositionSensitivityY",
                2.0f,
                new ConfigDescription(
                    "Multiplier for vertical (up/down) position",
                    new AcceptableValueRange<float>(0f, 3.0f)
                )
            );

            PositionSensitivityZ = config.Bind(
                "Position",
                "PositionSensitivityZ",
                2.0f,
                new ConfigDescription(
                    "Multiplier for depth (forward/back) position",
                    new AcceptableValueRange<float>(0f, 3.0f)
                )
            );

            PositionLimitX = config.Bind(
                "Position",
                "PositionLimitX",
                0.30f,
                new ConfigDescription(
                    "Maximum lateral displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)
                )
            );

            PositionLimitY = config.Bind(
                "Position",
                "PositionLimitY",
                0.15f,
                new ConfigDescription(
                    "Maximum upward vertical displacement in meters",
                    new AcceptableValueRange<float>(0f, 0.5f)
                )
            );

            PositionLimitYDown = config.Bind(
                "Position",
                "PositionLimitYDown",
                0.01f,
                new ConfigDescription(
                    "Maximum downward vertical displacement in meters. Keep low to avoid seeing your own neck.",
                    new AcceptableValueRange<float>(0f, 0.5f)
                )
            );

            PositionLimitZ = config.Bind(
                "Position",
                "PositionLimitZ",
                0.40f,
                new ConfigDescription(
                    "Maximum forward depth displacement in meters",
                    new AcceptableValueRange<float>(0.01f, 0.5f)
                )
            );

            PositionLimitZBack = config.Bind(
                "Position",
                "PositionLimitZBack",
                0.02f,
                new ConfigDescription(
                    "Maximum backward depth displacement in meters. Keep low to avoid seeing your own neck.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)
                )
            );

            PositionSmoothing = config.Bind(
                "Position",
                "PositionSmoothing",
                0.15f,
                new ConfigDescription(
                    "Smoothing for positional tracking (0 = instant, 1 = very slow)",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );


        }
    }
}
