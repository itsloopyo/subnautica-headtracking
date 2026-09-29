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
        private static int _raycastLayerMask = -1;

        private static RectTransform _handReticleRect;
        private static Canvas _cachedReticleCanvas;

        // The game never writes the HandReticle root's anchoredPosition, so whatever this
        // leaves there stays after tracking stops unless it is put back.
        private static Vector2 _originalAnchoredPosition;
        private static bool _moved;
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
            if (GameTypeResolver.HandReticleMain == null) return;

            var handReticle = GameTypeResolver.HandReticleMain() as MonoBehaviour;
            if (handReticle == null) return;

            // Cache the HandReticle's root RectTransform (moves everything). Unity's null
            // check is also true once the cached one was destroyed with its scene.
            if (_handReticleRect == null)
            {
                _handReticleRect = handReticle.transform as RectTransform;
                if (_handReticleRect == null) return;

                _originalAnchoredPosition = _handReticleRect.anchoredPosition;
                _moved = false;
                _cachedReticleCanvas = _handReticleRect.GetComponentInParent<Canvas>();

                HeadTrackingPlugin.ModLogger?.LogInfo(
                    $"HandReticle root: {_handReticleRect.name} " +
                    $"(children: {_handReticleRect.childCount}, " +
                    $"canvas: {(_cachedReticleCanvas != null ? _cachedReticleCanvas.name : "null")}, " +
                    $"scaleFactor: {(_cachedReticleCanvas != null ? _cachedReticleCanvas.scaleFactor : 1f)})");
#if DEBUG
                HeadTrackingPlugin.ModLogger?.LogInfo("HandReticle hierarchy:");
                LogHierarchy(_handReticleRect);
#endif
            }

            float scaleFactor = _cachedReticleCanvas != null ? _cachedReticleCanvas.scaleFactor : 1f;

            Transform camTransform = cam.transform;
            Vector3 aimOrigin = camTransform.position;
            Vector3 aimDir = camTransform.forward;

            // Exclude Player layer to avoid hitting held items (seaglide, scanner, etc.)
            if (_raycastLayerMask == -1)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                _raycastLayerMask = playerLayer >= 0
                    ? Physics.DefaultRaycastLayers & ~(1 << playerLayer)
                    : Physics.DefaultRaycastLayers;
            }

            // The live aim point on a hit; with nothing hit, the aim direction itself (w = 0),
            // which projects to where a point at infinity along the aim would be drawn. The ray
            // starts MinRaycastDistance out, so nothing nearer than that is taken as the target.
            Vector4 aimPoint;
            if (Physics.Raycast(aimOrigin + aimDir * MinRaycastDistance, aimDir, out RaycastHit hit,
                    MaxRaycastDistance - MinRaycastDistance, _raycastLayerMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = hit.point;
                aimPoint = new Vector4(p.x, p.y, p.z, 1f);
            }
            else
            {
                aimPoint = new Vector4(aimDir.x, aimDir.y, aimDir.z, 0f);
            }

            // Project aim point through our modified view+projection matrices explicitly.
            // cam.WorldToScreenPoint may not reflect the custom worldToCameraMatrix
            // (including position offset) within the same frame in all Unity versions.
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            Vector4 clip = vp * aimPoint;

            _moved = true;
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

            // Move the entire HandReticle root - all children (icon, text, prompts) follow
            _handReticleRect.anchoredPosition = offset;
        }

        /// <summary>
        /// Puts the reticle back where the game had it, for when the tracked view is no longer drawn.
        /// </summary>
        internal static void Restore()
        {
            if (!_moved) return;
            _moved = false;
            if (_handReticleRect != null)
                _handReticleRect.anchoredPosition = _originalAnchoredPosition;
        }
    }
}
