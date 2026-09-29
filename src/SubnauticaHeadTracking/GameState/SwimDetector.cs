using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.GameState
{
    /// <summary>
    /// Detects whether the player is currently swimming (underwater or on surface).
    /// Used by CameraRotationApplicator to offset the camera and avoid clipping the player body.
    /// </summary>
    internal static class SwimDetector
    {
        // Player.MotorMode: Walk, Dive, Seaglide, Vehicle, Mech, Run.
        private const int Dive = 1;
        private const int Seaglide = 2;

        /// <summary>
        /// Returns true if the player's motor mode is swimming or diving.
        /// Returns false if detection is unavailable or player is not swimming.
        /// </summary>
        internal static bool IsPlayerSwimming()
        {
            if (GameTypeResolver.MotorMode == null) return false;

            var player = GameTypeResolver.GetPlayer();
            if (player == null) return false;

            int mode = GameTypeResolver.MotorMode(player);
            return mode == Dive || mode == Seaglide;
        }
    }
}
