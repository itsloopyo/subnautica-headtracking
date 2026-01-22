using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.UI
{
    /// <summary>
    /// Sets the player's head mesh to shadow-only rendering while head tracking
    /// is active. The head is right at the camera and clips into view constantly.
    /// The body stays fully visible — arms, torso, legs, flippers all render normally.
    /// Saves and restores each renderer's original ShadowCastingMode so toggling
    /// tracking off returns to the exact stock game state.
    /// </summary>
    internal static class PlayerHeadHider
    {
        private static readonly List<Renderer> _renderers = new List<Renderer>();
        private static readonly List<ShadowCastingMode> _originalModes = new List<ShadowCastingMode>();
        private static bool _hidden;
        private static bool _searchDone;

        private static readonly HashSet<string> ShadowOnlyMeshes = new HashSet<string>
        {
            "diveSuit_head_geo",
            "player_head",
            "radiationSuit_head_geo",
            "radiationSuit_helmet_geo",
            "radiationSuit_helmet_geo 1",
            "scuba_head",
        };

        internal static void TryFind()
        {
            if (_renderers.Count > 0 && _renderers[0] == null)
            {
                _renderers.Clear();
                _originalModes.Clear();
                _hidden = false;
                _searchDone = false;
            }

            if (_renderers.Count > 0) return;
            if (_searchDone) return;

            try
            {
                GameTypeResolver.EnsureSearched();
                if (GameTypeResolver.PlayerMainField == null) return;

                var player = GameTypeResolver.PlayerMainField.GetValue(null) as Component;
                if (player == null) return;

                _searchDone = true;

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
                    HeadTrackingPlugin.ModLogger?.LogWarning(
                        "PlayerHeadHider: no matching head meshes found");
                }
            }
            catch (Exception ex)
            {
                HeadTrackingPlugin.ModLogger?.LogError(
                    $"PlayerHeadHider search failed: {ex.Message}");
                _searchDone = true;
            }
        }

        internal static void Hide()
        {
            if (_hidden) return;
            for (int i = 0; i < _renderers.Count; i++)
                _renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            _hidden = true;
        }

        internal static void Show()
        {
            if (!_hidden) return;
            for (int i = 0; i < _renderers.Count; i++)
                _renderers[i].shadowCastingMode = _originalModes[i];
            _hidden = false;
        }

        internal static void Reset()
        {
            Show();
            _renderers.Clear();
            _originalModes.Clear();
            _hidden = false;
            _searchDone = false;
        }
    }
}
