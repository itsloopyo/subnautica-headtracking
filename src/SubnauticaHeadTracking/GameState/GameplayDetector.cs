using UnityEngine;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.GameState
{
    /// <summary>
    /// Detects whether the game is in active gameplay (not paused, not in menus).
    /// Uses GameTypeResolver for cached reflection access to Subnautica's game types.
    /// </summary>
    internal static class GameplayDetector
    {
        /// <summary>
        /// Checks whether the game is in active gameplay.
        /// Returns false if paused, in main menu, or in ingame menu.
        /// </summary>
        internal static bool IsInActiveGameplay()
        {
            if (Time.timeScale <= 0f) return false;

            if (GameTypeResolver.GetPlayer() == null) return false;

            if (GameTypeResolver.MainMenuMain != null)
            {
                var mainMenu = GameTypeResolver.MainMenuMain() as Component;
                if (mainMenu != null && mainMenu.gameObject.activeInHierarchy)
                    return false;
            }

            if (GameTypeResolver.IngameMenuMain != null && GameTypeResolver.IngameMenuSelected != null)
            {
                var menu = GameTypeResolver.IngameMenuMain() as Component;
                if (menu != null && menu.gameObject.activeInHierarchy && (bool)GameTypeResolver.IngameMenuSelected(menu))
                    return false;
            }

            return true;
        }
    }
}
