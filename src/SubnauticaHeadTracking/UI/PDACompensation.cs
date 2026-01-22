using System;
using UnityEngine;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.UI
{
    /// <summary>
    /// Detects PDA open/close state. Head tracking is suppressed while the PDA is open
    /// because the PDA uses a separate WorldSpace canvas + camera that can't be cleanly
    /// compensated without breaking input hit testing.
    /// Uses PDA.isInUse via reflection to detect the actual open state.
    /// </summary>
    internal static class PDACompensation
    {
        private static object _pdaInstance;
        private static bool _searchFailed;

        /// <summary>
        /// True when the PDA tablet is currently visible on screen.
        /// </summary>
        internal static bool IsPDAOpen { get; private set; }

        internal static void TryFind()
        {
            // Unity destroyed-object check: clear stale ref after scene reload
            if (_pdaInstance is Component c && c == null)
            {
                _pdaInstance = null;
                IsPDAOpen = false;
            }

            if (_pdaInstance != null) return;
            if (_searchFailed) return;

            try
            {
                GameTypeResolver.EnsureSearched();
                if (GameTypeResolver.PDAType == null) return;

                if (GameTypeResolver.PDAIsInUseField == null && GameTypeResolver.PDAIsInUseProp == null)
                {
                    _searchFailed = true;
                    HeadTrackingPlugin.ModLogger?.LogWarning(
                        "PDA.isInUse not found — PDA detection disabled");
                    return;
                }

                var instance = UnityEngine.Object.FindObjectOfType(GameTypeResolver.PDAType) as Component;
                if (instance == null) return; // Not spawned yet — retry next frame

                _pdaInstance = instance;
                string memberName = GameTypeResolver.PDAIsInUseField != null
                    ? $"field:{GameTypeResolver.PDAIsInUseField.Name}"
                    : $"prop:{GameTypeResolver.PDAIsInUseProp.Name}";
                HeadTrackingPlugin.ModLogger?.LogInfo($"PDA found: {instance.name} (using {memberName})");
            }
            catch (Exception ex)
            {
                HeadTrackingPlugin.ModLogger?.LogError($"Error finding PDA: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks PDA open state and updates IsPDAOpen. Logs transitions.
        /// </summary>
        internal static void UpdateState()
        {
            if (_pdaInstance == null) return;

            bool wasOpen = IsPDAOpen;

            if (GameTypeResolver.PDAIsInUseField != null)
                IsPDAOpen = (bool)GameTypeResolver.PDAIsInUseField.GetValue(_pdaInstance);
            else if (GameTypeResolver.PDAIsInUseProp != null)
                IsPDAOpen = (bool)GameTypeResolver.PDAIsInUseProp.GetValue(_pdaInstance);

            if (IsPDAOpen && !wasOpen)
            {
                HeadTrackingPlugin.ModLogger?.LogInfo("PDA opened — head tracking suppressed");
            }
            else if (!IsPDAOpen && wasOpen)
            {
                HeadTrackingPlugin.ModLogger?.LogInfo("PDA closed — head tracking restored");
            }
        }
    }
}
