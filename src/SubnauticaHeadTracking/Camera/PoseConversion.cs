using CameraUnlock.Core.Data;

namespace SubnauticaHeadTracking.Camera
{
    /// <summary>
    /// The conversion from the tracker's pose to this view matrix: axis signs and the position
    /// scale. Every build up to v1.4.0 shipped these as settings (PitchInvert=true,
    /// PositionSensitivityX/Y/Z=2), so each value here is the one those builds shipped.
    /// </summary>
    public static class PoseConversion
    {
        public const bool InvertYaw = false;
        public const bool InvertPitch = true;
        public const bool InvertRoll = false;

        /// <summary>
        /// What the tracker's position is multiplied by, on every axis, before the position
        /// limits clamp it.
        /// </summary>
        public const float PositionScale = 2f;

        public static SensitivitySettings Rotation
        {
            get
            {
                return new SensitivitySettings(1f, 1f, 1f, InvertYaw, InvertPitch, InvertRoll);
            }
        }
    }
}
