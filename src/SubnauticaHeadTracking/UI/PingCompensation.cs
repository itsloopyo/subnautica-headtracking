using System;
using UnityEngine;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.UI
{
    /// <summary>
    /// Repositions ping UI elements to compensate for head tracking rotation and position.
    /// Uses a fixed reference distance for projection (pings are distant markers).
    /// </summary>
    internal static class PingCompensation
    {
        private const float PingReferenceDistance = 30f;

        private static RectTransform _pingCanvasTransform;
        private static bool _pingCanvasSearched;

        /// <summary>
        /// Finds the ping canvas (done once per gameplay session).
        /// </summary>
        internal static void TryFindCanvas()
        {
            // Unity destroyed-object check: clear stale ref after scene reload
            if (_pingCanvasTransform != null && !_pingCanvasTransform)
            {
                _pingCanvasTransform = null;
                _pingCanvasSearched = false;
            }

            if (_pingCanvasSearched) return;

            _pingCanvasSearched = true;
            try
            {
                GameTypeResolver.EnsureSearched();
                if (GameTypeResolver.PingsType == null || GameTypeResolver.PingCanvasField == null) return;

                var pingsInstance = UnityEngine.Object.FindObjectOfType(GameTypeResolver.PingsType) as MonoBehaviour;
                if (pingsInstance == null) return;

                _pingCanvasTransform = GameTypeResolver.PingCanvasField.GetValue(pingsInstance) as RectTransform;
                HeadTrackingPlugin.ModLogger?.LogInfo($"Ping canvas found: {_pingCanvasTransform?.name ?? "null"}");
            }
            catch (Exception ex)
            {
                HeadTrackingPlugin.ModLogger?.LogError($"Error finding ping canvas: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Repositions all ping children to compensate for head tracking rotation and position.
        ///
        /// Each ping is reprojected individually: its game-set screen position encodes a clean
        /// view-space direction, which is reconstructed, placed at a fixed reference distance,
        /// and projected through BOTH the clean and the head-tracked view+projection matrices.
        /// The ping is shifted by the difference. This is per-marker (so off-centre pings move
        /// correctly, not just by the centre's offset), matrix-driven (so it tracks pitch, yaw -
        /// local OR world-space - roll, and the positional lean automatically), and exact-identity
        /// when no head rotation is applied (clean and tracked matrices coincide, delta is zero).
        ///
        /// A fixed 30m reference distance is used for the lean parallax: at typical ping distances
        /// (10-500m) the parallax from a 0.4m lean is under 1 degree, which is sufficient.
        /// </summary>
        internal static void Reposition(UnityEngine.Camera cam)
        {
            if (_pingCanvasTransform == null) return;

            float canvasWidth = _pingCanvasTransform.rect.width;
            float canvasHeight = _pingCanvasTransform.rect.height;
            float halfWidth = canvasWidth * 0.5f;
            float halfHeight = canvasHeight * 0.5f;
            if (halfWidth <= 0f || halfHeight <= 0f) return;

            // Clean (game) and head-tracked view+projection matrices. OriginalViewMatrix is the
            // game-computed view captured this frame before head tracking was applied; the camera's
            // current worldToCameraMatrix is the head-tracked override.
            Matrix4x4 proj = cam.projectionMatrix;
            Matrix4x4 cleanVP = proj * Camera.CameraRotationApplicator.OriginalViewMatrix;
            Matrix4x4 trackedVP = proj * cam.worldToCameraMatrix;

            // Tangent half-extents, for reconstructing a clean world direction from a ping's NDC.
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * cam.aspect;
            Vector3 camPos = cam.transform.position;
            Quaternion camRot = cam.transform.rotation;

            int childCount = _pingCanvasTransform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = _pingCanvasTransform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;

                RectTransform rectTransform = child as RectTransform;
                if (rectTransform == null) continue;

                Vector2 pos = rectTransform.anchoredPosition;
                float ndcX = (pos.x - halfWidth) / halfWidth;
                float ndcY = (pos.y - halfHeight) / halfHeight;

                // Reconstruct the world point this ping currently projects to (clean camera),
                // at the fixed reference distance. transform-local forward is +Z, so a centre
                // ping (ndc 0,0) maps to camRot * (0,0,1) = camera forward.
                Vector3 localDir = new Vector3(ndcX * tanH, ndcY * tanV, 1f);
                Vector3 worldPoint = camPos + (camRot * localDir).normalized * PingReferenceDistance;
                var worldPoint4 = new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1f);

                Vector4 trackedClip = trackedVP * worldPoint4;
                if (trackedClip.w <= 0f)
                {
                    // Ping is behind the head-tracked view (extreme turn). Park it off-screen.
                    rectTransform.anchoredPosition = new Vector2(canvasWidth * 10f, pos.y);
                    continue;
                }

                Vector4 cleanClip = cleanVP * worldPoint4;
                float cleanW = cleanClip.w != 0f ? cleanClip.w : 1f;

                // Shift by (tracked - clean) projection so any reconstruction/mapping bias cancels
                // and zero head rotation yields zero movement.
                float deltaX = (trackedClip.x / trackedClip.w - cleanClip.x / cleanW) * halfWidth;
                float deltaY = (trackedClip.y / trackedClip.w - cleanClip.y / cleanW) * halfHeight;

                rectTransform.anchoredPosition = new Vector2(pos.x + deltaX, pos.y + deltaY);
            }
        }
    }
}
