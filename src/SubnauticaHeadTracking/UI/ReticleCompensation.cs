using UnityEngine;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.UI
{
    /// <summary>
    /// Offsets the entire HandReticle UI hierarchy to match the game's actual aim point
    /// on the head-tracked view. Moves the HandReticle root RectTransform so all children
    /// (icon, interaction text, hand icon, prompts) move together.
    /// </summary>
    internal static class ReticleCompensation
    {
        private const float MaxRaycastDistance = 1000f;
        private const float MinRaycastDistance = 0.5f;
        private const float DistanceSmoothingRate = 15f;
        private static int _raycastLayerMask = -1;

        private static float _lastHitDistance = 100f;

        private static RectTransform _handReticleRect;
        private static Canvas _cachedReticleCanvas;
#if DEBUG
        private static bool _hierarchyLogged;
#endif

#if DEBUG
        private static void LogHierarchy(Transform root, int depth = 0)
        {
            var indent = new string(' ', depth * 2);
            var rect = root as RectTransform;
            var components = root.GetComponents<Component>();
            var compNames = new System.Text.StringBuilder();
            foreach (var c in components)
            {
                if (c != null)
                    compNames.Append(c.GetType().Name).Append(", ");
            }
            HeadTrackingPlugin.ModLogger?.LogInfo(
                $"{indent}{root.name} [{compNames}] " +
                (rect != null ? $"pos=({rect.anchoredPosition.x:F0},{rect.anchoredPosition.y:F0})" : ""));

            for (int i = 0; i < root.childCount; i++)
                LogHierarchy(root.GetChild(i), depth + 1);
        }
#endif

        internal static void UpdatePosition(UnityEngine.Camera cam)
        {
            GameTypeResolver.EnsureSearched();

            if (GameTypeResolver.HandReticleType == null || GameTypeResolver.HandReticleMainField == null)
                return;

            var handReticle = GameTypeResolver.HandReticleMainField.GetValue(null);
            if (handReticle == null) return;

            // Unity destroyed-object check: clear stale refs after scene reload
            if (_handReticleRect != null && !_handReticleRect)
            {
                _handReticleRect = null;
                _cachedReticleCanvas = null;
#if DEBUG
                _hierarchyLogged = false;
#endif
            }

            // Cache the HandReticle's root RectTransform (moves everything)
            if (_handReticleRect == null)
            {
                var mb = handReticle as MonoBehaviour;
                if (mb == null) return;
                _handReticleRect = mb.transform as RectTransform;
                if (_handReticleRect == null) return;

                _cachedReticleCanvas = _handReticleRect.GetComponentInParent<Canvas>();

                HeadTrackingPlugin.ModLogger?.LogInfo(
                    $"HandReticle root: {_handReticleRect.name} " +
                    $"(children: {_handReticleRect.childCount}, " +
                    $"canvas: {(_cachedReticleCanvas != null ? _cachedReticleCanvas.name : "null")}, " +
                    $"scaleFactor: {(_cachedReticleCanvas != null ? _cachedReticleCanvas.scaleFactor : 1f)})");
            }

#if DEBUG
            // One-time hierarchy dump for diagnostics
            if (!_hierarchyLogged && _handReticleRect != null)
            {
                _hierarchyLogged = true;
                HeadTrackingPlugin.ModLogger?.LogInfo("HandReticle hierarchy:");
                LogHierarchy(_handReticleRect);
            }
#endif

            float scaleFactor = _cachedReticleCanvas != null ? _cachedReticleCanvas.scaleFactor : 1f;

            Vector3 aimOrigin = cam.transform.position;
            Vector3 aimDir = cam.transform.forward;

            // Exclude Player layer to avoid hitting held items (seaglide, scanner, etc.)
            if (_raycastLayerMask == -1)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                _raycastLayerMask = playerLayer >= 0
                    ? Physics.DefaultRaycastLayers & ~(1 << playerLayer)
                    : Physics.DefaultRaycastLayers;
            }

            RaycastHit hit;
            if (Physics.Raycast(aimOrigin, aimDir, out hit, MaxRaycastDistance,
                    _raycastLayerMask, QueryTriggerInteraction.Ignore)
                && hit.distance >= MinRaycastDistance)
            {
                float t = 1f - Mathf.Exp(-DistanceSmoothingRate * Time.deltaTime);
                _lastHitDistance = Mathf.Lerp(_lastHitDistance, hit.distance, t);
            }

            // Project aim point through our modified view+projection matrices explicitly.
            // cam.WorldToScreenPoint may not reflect the custom worldToCameraMatrix
            // (including position offset) within the same frame in all Unity versions.
            Vector3 aimWorldPoint = aimOrigin + aimDir * _lastHitDistance;
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            Vector4 clip = vp * new Vector4(aimWorldPoint.x, aimWorldPoint.y, aimWorldPoint.z, 1f);

            if (clip.w <= 0f)
            {
                _handReticleRect.anchoredPosition = new Vector2(Screen.width * 10f, 0f);
                return;
            }

            float halfW = Screen.width * 0.5f;
            float halfH = Screen.height * 0.5f;
            Vector2 offset = new Vector2(
                clip.x / clip.w * halfW,
                clip.y / clip.w * halfH);

            if (scaleFactor > 0f && scaleFactor != 1f)
                offset /= scaleFactor;

            // Move the entire HandReticle root — all children (icon, text, prompts) follow
            _handReticleRect.anchoredPosition = offset;
        }
    }
}
