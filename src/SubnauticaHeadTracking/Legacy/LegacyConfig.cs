using UnityEngine;

namespace SubnauticaHeadTracking.Legacy
{
    /// <summary>
    /// Frozen: every setting v1.4.0 bound on the plugin's .cfg, as <see cref="LegacyConfigReader"/>
    /// reads it. The reader fills every field from its Bind call, whose default is v1.4.0's.
    /// Never edit this file.
    /// </summary>
    public sealed class LegacyConfig
    {
        public int UdpPort;
        public string BindAddress;

        public float YawSensitivity;
        public float PitchSensitivity;
        public float RollSensitivity;

        public float YawDeadzone;
        public float PitchDeadzone;
        public float RollDeadzone;

        public bool YawInvert;
        public bool PitchInvert;
        public bool RollInvert;

        public KeyCode ToggleHotkey;
        public KeyCode CycleTrackingModeHotkey;
        public KeyCode ToggleYawModeHotkey;
        public KeyCode CyclePortHotkey;

        public float LocalSmoothing;
        public float RemoteSmoothing;

        public bool PositionEnabled;
        public float PositionSensitivityX;
        public float PositionSensitivityY;
        public float PositionSensitivityZ;
        public float PositionLimitX;
        public float PositionLimitY;
        public float PositionLimitYDown;
        public float PositionLimitZ;
        public float PositionLimitZBack;
    }
}
