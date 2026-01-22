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

            GameTypeResolver.EnsureSearched();

            if (GameTypeResolver.PlayerType == null || GameTypeResolver.PlayerMainField == null)
                return false;

            var player = GameTypeResolver.PlayerMainField.GetValue(null) as UnityEngine.Object;
            if (player == null) return false;

            if (GameTypeResolver.MainMenuType != null && GameTypeResolver.MainMenuMainField != null)
            {
                var mainMenu = GameTypeResolver.MainMenuMainField.GetValue(null) as Component;
                if (mainMenu != null && mainMenu.gameObject.activeInHierarchy)
                    return false;
            }

            if (GameTypeResolver.IngameMenuType != null && GameTypeResolver.IngameMenuMainField != null)
            {
                var menu = GameTypeResolver.IngameMenuMainField.GetValue(null) as Component;
                if (menu != null && menu.gameObject.activeInHierarchy)
                {
                    if (GameTypeResolver.IngameMenuSelectedField != null)
                    {
                        var isSelected = (bool)GameTypeResolver.IngameMenuSelectedField.GetValue(menu);
                        if (isSelected) return false;
                    }
                }
            }

            return true;
        }
    }
}
