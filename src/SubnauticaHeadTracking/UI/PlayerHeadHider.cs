using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.UI
{
    /// <summary>
    /// Sets the player's head mesh to shadow-only rendering while head tracking
    /// is active. The head is right at the camera and clips into view constantly.
    /// The body stays fully visible - arms, torso, legs, flippers all render normally.
    /// Saves and restores each renderer's original ShadowCastingMode so toggling
    /// tracking off returns to the exact stock game state.
    /// </summary>
    internal static class PlayerHeadHider
    {
        private static readonly List<Renderer> _renderers = new List<Renderer>();
        private static readonly List<ShadowCastingMode> _originalModes = new List<ShadowCastingMode>();
        private static bool _hidden;

        // The player the renderers were collected from. A new Player (a save loaded after
        // returning to the main menu) is searched again, whatever the old search found.
        private static Component _searchedPlayer;

        private static readonly HashSet<string> ShadowOnlyMeshes = new HashSet<string>
        {
            "diveSuit_head_geo",
            "player_head",
            "radiationSuit_head_geo",
            "radiationSuit_helmet_geo",
            "radiationSuit_helmet_geo 1",
            "scuba_head",
        };

        internal static void Hide()
        {
            var player = GameTypeResolver.GetPlayer();
            if (player == null) return;

            if (!ReferenceEquals(player, _searchedPlayer))
            {
                // The old player's renderers are destroyed with it; nothing to restore.
                _renderers.Clear();
                _originalModes.Clear();
                _hidden = false;
                _searchedPlayer = player;
                Collect(player);
            }

            if (_hidden) return;
            for (int i = 0; i < _renderers.Count; i++)
                _renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            _hidden = true;
        }

        internal static void Show()
        {
            if (!_hidden) return;
            _hidden = false;
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].shadowCastingMode = _originalModes[i];
            }
        }

        private static void Collect(Component player)
        {
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (ShadowOnlyMeshes.Contains(r.gameObject.name))
                {
                    _renderers.Add(r);
                    _originalModes.Add(r.shadowCastingMode);
                    HeadTrackingPlugin.ModLogger?.LogInfo(
                        $"PlayerHeadHider: found {r.gameObject.name} (original mode={r.shadowCastingMode})");
                }
            }

            if (_renderers.Count == 0)
            {
                HeadTrackingPlugin.ModLogger?.LogWarning("PlayerHeadHider: no matching head meshes found");
            }
        }
    }
}
