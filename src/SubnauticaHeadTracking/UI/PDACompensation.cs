using UnityEngine;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.UI
{
    /// <summary>
    /// Detects PDA open/close state. Head tracking is suppressed while the PDA is open
    /// because the PDA uses a separate WorldSpace canvas + camera that can't be cleanly
    /// compensated without breaking input hit testing.
    /// Reads Player.main.pda.isInUse; the PDA's GameObject is inactive while it is closed,
    /// so it cannot be found by FindObjectOfType.
    /// </summary>
    internal static class PDACompensation
    {
        /// <summary>
        /// True when the PDA tablet is currently visible on screen.
        /// </summary>
        internal static bool IsPDAOpen { get; private set; }

        /// <summary>
        /// Checks PDA open state and updates IsPDAOpen. Logs transitions.
        /// </summary>
        internal static void UpdateState()
        {
            bool wasOpen = IsPDAOpen;
            IsPDAOpen = ReadIsOpen();

            if (IsPDAOpen && !wasOpen)
            {
                HeadTrackingPlugin.ModLogger?.LogInfo("PDA opened - head tracking suppressed");
            }
            else if (!IsPDAOpen && wasOpen)
            {
                HeadTrackingPlugin.ModLogger?.LogInfo("PDA closed - head tracking restored");
            }
        }

        private static bool ReadIsOpen()
        {
            if (GameTypeResolver.PlayerPda == null || GameTypeResolver.PdaIsInUse == null) return false;

            var player = GameTypeResolver.GetPlayer();
            if (player == null) return false;

            Component pda = GameTypeResolver.PlayerPda(player);
            return pda != null && (bool)GameTypeResolver.PdaIsInUse(pda);
        }
    }
}
