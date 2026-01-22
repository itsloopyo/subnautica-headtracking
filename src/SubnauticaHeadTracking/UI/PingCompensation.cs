using System;
using CameraUnlock.Core.Unity.Extensions;
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
        /// Uses a fixed 30m reference distance for projection — at typical ping distances
        /// (10-500m) the parallax from a 0.4m lean is under 1 degree, which is sufficient.
        /// </summary>
        internal static void Reposition(UnityEngine.Camera cam, float yaw, float pitch, float roll)
        {
            if (_pingCanvasTransform == null) return;

            // Compute center offset using WorldToScreenPoint projection at a fixed reference distance
            Vector2 screenOffset = CanvasCompensation.CalculateAimScreenOffset(cam, cam.transform.forward, PingReferenceDistance, 1f);

            // Convert from screen pixels to canvas units
            float canvasWidth = _pingCanvasTransform.rect.width;
            float canvasHeight = _pingCanvasTransform.rect.height;
            float halfWidth = canvasWidth * 0.5f;
            float halfHeight = canvasHeight * 0.5f;
            float offsetX = screenOffset.x * (canvasWidth / Screen.width);
            float offsetY = screenOffset.y * (canvasHeight / Screen.height);

            // Roll: pre-calculate rotation values (negate to match view matrix convention)
            float rollRad = -roll * Mathf.Deg2Rad;
            float cosRoll = Mathf.Cos(rollRad);
            float sinRoll = Mathf.Sin(rollRad);

            int childCount = _pingCanvasTransform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = _pingCanvasTransform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;

                RectTransform rectTransform = child as RectTransform;
                if (rectTransform == null) continue;

                // Get position relative to canvas center
                Vector2 pos = rectTransform.anchoredPosition;
                float relX = pos.x - halfWidth;
                float relY = pos.y - halfHeight;

                // Apply roll rotation around canvas center
                float rotatedRelX = relX * cosRoll - relY * sinRoll;
                float rotatedRelY = relX * sinRoll + relY * cosRoll;

                // Apply center offset and convert back to canvas coordinates
                rectTransform.anchoredPosition = new Vector2(
                    rotatedRelX + offsetX + halfWidth,
                    rotatedRelY + offsetY + halfHeight);
            }
        }
    }
}
