namespace SubnauticaHeadTracking
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.cameraunlock.subnautica.headtracking";
        public const string PLUGIN_NAME = "Head Tracking";
        public const string PLUGIN_VERSION = "1.0.15";

        public const int UDP_DEFAULT_PORT = 4242;
        public const string UDP_DEFAULT_ADDRESS = "0.0.0.0";

        public const float DEFAULT_YAW_SENSITIVITY = 1.0f;
        public const float DEFAULT_PITCH_SENSITIVITY = 1.0f;
        public const float DEFAULT_ROLL_SENSITIVITY = 1.0f;

        public const float DEFAULT_SMOOTHING_FACTOR = 0.0f;  // 0 = no smoothing (instant response)
        // Minimum smoothing baseline uses SmoothingUtils.BaselineSmoothing from CameraUnlock.Core

        public const float DEFAULT_YAW_DEADZONE = 0.0f;
        public const float DEFAULT_PITCH_DEADZONE = 0.0f;
        public const float DEFAULT_ROLL_DEADZONE = 0.0f;

        public const float MIN_SENSITIVITY = 0.1f;
        public const float MAX_SENSITIVITY = 3.0f;

        public const float MIN_DEADZONE = 0.0f;
        public const float MAX_DEADZONE = 10.0f;
    }
}
