using System;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.GameState
{
    /// <summary>
    /// Detects whether the player is currently swimming (underwater or on surface).
    /// Used by CameraRotationApplicator to offset the camera and avoid clipping the player body.
    /// </summary>
    internal static class SwimDetector
    {
        private static bool _failed;

        /// <summary>
        /// Returns true if the player's motor mode is swimming or diving.
        /// Returns false if detection fails or player is not swimming.
        /// </summary>
        internal static bool IsPlayerSwimming()
        {
            if (_failed) return false;

            GameTypeResolver.EnsureSearched();

            if (GameTypeResolver.PlayerMainField == null)
                return false;

            if (GameTypeResolver.MotorModeField == null && GameTypeResolver.MotorModeProp == null)
            {
                _failed = true;
                return false;
            }

            var player = GameTypeResolver.PlayerMainField.GetValue(null);
            if (ReferenceEquals(player, null)) return false;

            object motorMode = GameTypeResolver.MotorModeField != null
                ? GameTypeResolver.MotorModeField.GetValue(player)
                : GameTypeResolver.MotorModeProp.GetValue(player, null);

            int mode = Convert.ToInt32(motorMode);
            return mode == 1 || mode == 2;
        }
    }
}
