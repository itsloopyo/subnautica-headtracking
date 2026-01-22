using System;
using UnityEngine;
using UnityEngine.Rendering;
using SubnauticaHeadTracking.Integration;

namespace SubnauticaHeadTracking.UI
{
    /// <summary>
    /// Compensates the diving mask position and rotation during head tracking.
    /// Applied in onPreCull (before Unity caches transforms for culling/rendering),
    /// restored in onPostRender.
    ///
    /// The correction: H = V_new⁻¹ × V_orig transforms the mask so that when rendered
    /// through the head-tracked view matrix, it appears at the same screen position as
    /// if rendered through the original view matrix.
    ///
    /// </summary>
    internal static class PlayerMaskCompensation
    {
        private static readonly Bounds NeverCullBounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
        private static Transform _maskTransform;
        private static Vector3 _savedPosition;
        private static Quaternion _savedRotation;
        private static bool _modified;
        private static bool _searchFailed;
        private static bool _firstCompensationLogged;

        internal static void TryFind()
        {
            // Unity destroyed-object check: the C# ref is non-null but the
            // underlying native object is gone after scene reload / pause.
            if (_maskTransform != null && _maskTransform)
                return;

            // Stale reference — clear and re-search
            _maskTransform = null;
            _modified = false;
            _firstCompensationLogged = false;

            if (_searchFailed) return;

            try
            {
                GameTypeResolver.EnsureSearched();
                if (GameTypeResolver.PlayerMaskType == null)
                {
                    if (GameTypeResolver.PlayerType != null)
                    {
                        HeadTrackingPlugin.ModLogger?.LogWarning(
                            "PlayerMask type not found — mask compensation disabled");
                        _searchFailed = true;
                    }
                    return;
                }

                var maskInstance = UnityEngine.Object.FindObjectOfType(GameTypeResolver.PlayerMaskType) as Component;
                if (maskInstance == null) return;

                _maskTransform = maskInstance.transform;

                int rendererCount = 0;
                foreach (var renderer in _maskTransform.GetComponentsInChildren<Renderer>())
                {
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                    if (renderer is SkinnedMeshRenderer smr)
                        smr.updateWhenOffscreen = true;
                    else if (renderer is MeshRenderer mr)
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf != null && mf.mesh != null)
                            mf.mesh.bounds = NeverCullBounds;
                    }

                    rendererCount++;
                }

                HeadTrackingPlugin.ModLogger?.LogInfo(
                    $"PlayerMask found: {_maskTransform.gameObject.name} " +
                    $"(children: {_maskTransform.childCount}, renderers: {rendererCount} set to ForceNoMotion)");
            }
            catch (Exception ex)
            {
                HeadTrackingPlugin.ModLogger?.LogError(
                    $"Error finding PlayerMask: {ex.Message}");
                throw;
            }
        }

        internal static void ApplyCompensation(UnityEngine.Camera cam)
        {
            if (_maskTransform == null) return;
            if (!State.TrackingState.IsEnabled) return;

            // If the previous frame's restore was missed (camera switch, exception),
            // restore first to prevent compounding the offset.
            if (_modified)
            {
                _maskTransform.position = _savedPosition;
                _maskTransform.rotation = _savedRotation;
                _modified = false;
            }

            var origView = Camera.CameraRotationApplicator.OriginalViewMatrix;
            if (origView == Matrix4x4.identity) return;

            Matrix4x4 H = cam.cameraToWorldMatrix * origView;

            _savedPosition = _maskTransform.position;
            _savedRotation = _maskTransform.rotation;

            _maskTransform.position = H.MultiplyPoint3x4(_savedPosition);
            _maskTransform.rotation = H.rotation * _savedRotation;
            _modified = true;

            if (!_firstCompensationLogged)
            {
                _firstCompensationLogged = true;
                var delta = _maskTransform.position - _savedPosition;
                HeadTrackingPlugin.ModLogger?.LogInfo(
                    $"Mask compensation applied on {_maskTransform.gameObject.name} — " +
                    $"pos delta=({delta.x:F4}, {delta.y:F4}, {delta.z:F4}), " +
                    $"rot delta={Quaternion.Angle(_savedRotation, _maskTransform.rotation):F2}°");
            }
        }

        internal static void RestorePosition()
        {
            if (!_modified) return;
            if (_maskTransform == null) return;

            _maskTransform.position = _savedPosition;
            _maskTransform.rotation = _savedRotation;
            _modified = false;
        }
    }
}
