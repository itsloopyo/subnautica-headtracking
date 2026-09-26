using System;
using UnityEngine;
using BepInEx.Logging;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using SubnauticaHeadTracking.GameState;

namespace SubnauticaHeadTracking.Camera
{
    /// <summary>
    /// Applies head tracking rotation to the camera's view matrix using the shared library.
    /// This ONLY affects rendering - game logic (movement, aiming, reticle) remains unchanged.
    /// The reticle will appear to move on screen when you turn your head, because:
    /// - The reticle's WORLD position is fixed (determined by mouse/game logic)
    /// - The camera VIEW rotated (head tracking)
    /// - Therefore reticle appears in a different SCREEN position
    /// </summary>
    public static class CameraRotationApplicator
    {
        private static ManualLogSource Logger => HeadTrackingPlugin.ModLogger;
        private static bool _hasLoggedFirstApplication = false;

        // Processing pipeline from the shared library
        private static readonly TrackingProcessor processor = new TrackingProcessor();
        private static readonly PoseInterpolator poseInterpolator = new PoseInterpolator();

        // Position processing
        private static PositionProcessor _positionProcessor;
        private static PositionInterpolator _positionInterpolator;

        // Swim body avoidance: offset camera forward+down when swimming
        private static float _smoothedSwimBlend;
        private static readonly Vector3 SwimOffset = new Vector3(0f, -0.025f, 0.025f); // down + forward in view space
        private const float SwimBlendSpeed = 5f;

        // Cached settings to avoid per-frame config reads and struct creation
        private static bool _settingsDirty = true;
        private static SensitivitySettings _cachedSensitivity;
        private static DeadzoneSettings _cachedDeadzone;
        private static float _cachedLocalSmoothing;
        private static float _cachedRemoteSmoothing;
        private static bool _cachedIsRemoteConnection;

        /// <summary>
        /// Current processed head tracking angles (degrees). Used for reticle compensation.
        /// </summary>
        public static float CurrentYaw { get; private set; }
        public static float CurrentPitch { get; private set; }
        public static float CurrentRoll { get; private set; }

        /// <summary>
        /// Current position offset applied to the view matrix (camera-local space).
        /// Used by mask compensation to keep attached objects screen-fixed.
        /// </summary>
        public static Vector3 CurrentPositionOffset { get; private set; }

        /// <summary>
        /// The camera's original (game-computed) view matrix, captured after
        /// ResetWorldToCameraMatrix but before head tracking rotation/position is applied.
        /// Used by PlayerMaskCompensation to compute the correction transform.
        /// </summary>
        public static Matrix4x4 OriginalViewMatrix { get; private set; }

        /// <summary>Whether positional tracking is enabled (derived from <see cref="State.TrackingState.Mode"/>).</summary>
        public static bool PositionEnabled => State.TrackingState.IsPositionEnabled;

        /// <summary>
        /// Initializes position processing components.
        /// </summary>
        public static void InitializePosition()
        {
            _positionProcessor = new PositionProcessor
            {
                Settings = BuildPositionSettings()
            };
            _positionInterpolator = new PositionInterpolator();
        }

        // Every slot is named. The constructor takes ten consecutive floats, so a
        // positional call binds silently to whatever arity the signature happens to
        // have: the smoothing split already turned one such call site into a
        // limit-fed-as-smoothing bug fleet-wide. Naming makes any future shift of the
        // parameter list a compile error here instead.
        private static PositionSettings BuildPositionSettings()
        {
            return new PositionSettings(
                sensitivityX: Config.ConfigurationManager.Values.PositionSensitivityX,
                sensitivityY: Config.ConfigurationManager.Values.PositionSensitivityY,
                sensitivityZ: Config.ConfigurationManager.Values.PositionSensitivityZ,
                limitX: Config.ConfigurationManager.Values.PositionLimitX,
                limitY: Config.ConfigurationManager.Values.PositionLimitY,
                limitYDown: Config.ConfigurationManager.Values.PositionLimitYDown,
                limitZ: Config.ConfigurationManager.Values.PositionLimitZ,
                limitZBack: Config.ConfigurationManager.Values.PositionLimitZBack,
                localSmoothing: Config.ConfigurationManager.Values.LocalSmoothing,
                remoteSmoothing: Config.ConfigurationManager.Values.RemoteSmoothing,
                invertX: true, invertY: false, invertZ: false
            );
        }

        /// <summary>
        /// Applies head tracking rotation to the camera's worldToCameraMatrix.
        /// Called from Camera.onPreCull, which runs after all game logic but before rendering.
        /// This ensures head tracking ONLY affects the rendered view, not game logic.
        /// </summary>
        /// <param name="cam">The Unity camera to modify</param>
        /// <param name="receiver">Core OpenTrack receiver providing rotation data</param>
        public static void ApplyViewMatrixRotation(UnityEngine.Camera cam, OpenTrackReceiver receiver)
        {
            if (cam == null)
            {
                throw new System.ArgumentNullException(nameof(cam), "Camera cannot be null");
            }
            if (receiver == null)
            {
                throw new System.ArgumentNullException(nameof(receiver), "Receiver cannot be null");
            }

            // Cache Time.deltaTime - single native interop read instead of five
            float dt = Time.deltaTime;

            // Get raw pose with timestamp (needed for PoseInterpolator new-sample detection)
            TrackingPose rawPose = receiver.GetRawPose();

            // Update processor settings from config (in case they changed) and pick up
            // a connection locality change so the right smoothing parameter applies.
            UpdateProcessorSettings(receiver);

            // Interpolate between tracking samples (30Hz → display rate via velocity extrapolation)
            TrackingPose interpolatedPose = poseInterpolator.Update(rawPose, dt);

            // Always feed interpolated data - the interpolator fills frames with velocity-
            // predicted values, which is what keeps motion smooth at LocalSmoothing = 0.
            TrackingPose processedPose = processor.Process(interpolatedPose, dt);

            // When the user has disabled rotation (PositionOnly mode), zero the angles
            // so the rotation matrix collapses to identity and reticle/HUD compensators
            // see no rotation. The processor still ran above so it stays warm for resume.
            bool rotationEnabled = State.TrackingState.IsRotationEnabled;
            CurrentYaw = rotationEnabled ? processedPose.Yaw : 0f;
            CurrentPitch = rotationEnabled ? processedPose.Pitch : 0f;
            CurrentRoll = rotationEnabled ? processedPose.Roll : 0f;

            // Capture the original (game-computed) view matrix before we modify it.
            cam.ResetWorldToCameraMatrix();
            OriginalViewMatrix = cam.worldToCameraMatrix;

            // Build head rotation with correct composition order: yaw → pitch → roll
            // in view-space (right-to-left multiplication). This prevents pitch from
            // appearing as roll at extreme yaw angles.
            //
            // World-space yaw rotates about world up expressed in this view's space
            // (OriginalViewMatrix maps world up into the view frame the head rotation
            // is applied in). Keeping it as the innermost yaw axis reproduces the
            // decomposed convention: at the horizon world up maps to view up and the
            // two modes coincide; looking straight down it becomes a view-axis spin.
            Vector3 yawAxis = State.TrackingState.WorldSpaceYaw
                ? OriginalViewMatrix.MultiplyVector(Vector3.up)
                : Vector3.up;
            Quaternion yawQ = Quaternion.AngleAxis(CurrentYaw, yawAxis);
            Quaternion pitchQ = Quaternion.AngleAxis(CurrentPitch, Vector3.right);
            Quaternion rollQ = Quaternion.AngleAxis(-CurrentRoll, Vector3.forward);
            Quaternion headRotUnity = rollQ * pitchQ * yawQ;

            // Compute head rotation matrix directly - avoids ViewMatrixModifier which
            // would redundantly ResetWorldToCameraMatrix and re-read the matrix we
            // already captured above.
            Matrix4x4 headRotMatrix = Matrix4x4.Rotate(headRotUnity);

            // Accumulate view-space offset from position tracking and swim avoidance.
            // Both are applied in original view space so offsets follow body orientation.
            // Combined into a single matrix composition to eliminate redundant
            // OriginalViewMatrix.inverse computations and intermediate matrix writes.
            Vector3 totalViewOffset = Vector3.zero;

            // Position processing: compute offset (rendering-only, like rotation)
            if (State.TrackingState.IsPositionEnabled && _positionProcessor != null && _positionInterpolator != null)
            {
                var rawPos = receiver.GetLatestPosition();
                var interpolatedPos = _positionInterpolator.Update(rawPos, dt);
                var headRotQ = new Quat4(headRotUnity.x, headRotUnity.y, headRotUnity.z, headRotUnity.w);
                Vec3 posOffset = _positionProcessor.Process(interpolatedPos, headRotQ, dt);

                // Already box-clamped by the processor against the configured asymmetric
                // limits ([-LimitYDown, +LimitY], [-LimitZ, +LimitZBack]).
                Vector3 offset = new Vector3(posOffset.X, posOffset.Y, posOffset.Z);

                CurrentPositionOffset = offset;
                totalViewOffset = -offset;
            }

            // Swim body avoidance: nudge camera forward+down when swimming
            float targetBlend = SwimDetector.IsPlayerSwimming() ? 1f : 0f;
            _smoothedSwimBlend = Mathf.Lerp(_smoothedSwimBlend, targetBlend, SwimBlendSpeed * dt);
            if (_smoothedSwimBlend > 0.001f)
            {
                totalViewOffset += SwimOffset * _smoothedSwimBlend;
            }

            // Apply final view matrix in a single composition:
            //   cam.worldToCameraMatrix = headRot * Translate(offset) * originalView
            // Position uses negative offset (lean right → camera moves left in view space).
            // Swim uses positive offset (SwimOffset is already in the desired direction).
            if (totalViewOffset.sqrMagnitude > 0.000001f)
                cam.worldToCameraMatrix = headRotMatrix * Matrix4x4.Translate(totalViewOffset) * OriginalViewMatrix;
            else
                cam.worldToCameraMatrix = headRotMatrix * OriginalViewMatrix;

            if (!_hasLoggedFirstApplication)
            {
                Logger.LogInfo($"First view matrix rotation applied: Yaw={CurrentYaw:F2}°, Pitch={CurrentPitch:F2}°, Roll={CurrentRoll:F2}°");
                Logger.LogInfo($"Smoothing: local={_cachedLocalSmoothing}, remote={_cachedRemoteSmoothing}, effective={SmoothingUtils.GetEffectiveSmoothing(_cachedLocalSmoothing, _cachedRemoteSmoothing, _cachedIsRemoteConnection)}, extrapolation fraction={poseInterpolator.MaxExtrapolationFraction}");
                Logger.LogInfo("Head tracking uses camera-local rotation (no horizon lock - swimming-safe)");
                _hasLoggedFirstApplication = true;
            }
        }

        /// <summary>
        /// Marks settings as dirty, forcing them to be reloaded on the next frame.
        /// Call this when configuration changes.
        /// </summary>
        public static void MarkSettingsDirty()
        {
            _settingsDirty = true;
        }

        /// <summary>
        /// Updates processor settings from the configuration manager.
        /// Only rebuilds settings when they are marked dirty (config changed) or when
        /// the connection switched between a local and a remote tracker.
        /// </summary>
        /// <param name="receiver">Core OpenTrack receiver providing the connection locality</param>
        private static void UpdateProcessorSettings(OpenTrackReceiver receiver)
        {
            bool isRemoteConnection = receiver.IsRemoteConnection;
            if (isRemoteConnection != _cachedIsRemoteConnection)
            {
                _cachedIsRemoteConnection = isRemoteConnection;
                _settingsDirty = true;
            }

            if (!_settingsDirty) return;

            _settingsDirty = false;

            // Cache and apply sensitivity settings
            _cachedSensitivity = new SensitivitySettings(
                Config.ConfigurationManager.Values.YawSensitivity,
                Config.ConfigurationManager.Values.PitchSensitivity,
                Config.ConfigurationManager.Values.RollSensitivity,
                Config.ConfigurationManager.Values.YawInvert,
                Config.ConfigurationManager.Values.PitchInvert,
                Config.ConfigurationManager.Values.RollInvert
            );
            processor.Sensitivity = _cachedSensitivity;

            // Cache and apply deadzone settings
            _cachedDeadzone = new DeadzoneSettings(
                Config.ConfigurationManager.Values.YawDeadzone,
                Config.ConfigurationManager.Values.PitchDeadzone,
                Config.ConfigurationManager.Values.RollDeadzone
            );
            processor.Deadzone = _cachedDeadzone;

            // Both smoothing values go to the processors as-is; the library selects
            // between them from the connection flag. No floor is applied.
            _cachedLocalSmoothing = Config.ConfigurationManager.Values.LocalSmoothing;
            _cachedRemoteSmoothing = Config.ConfigurationManager.Values.RemoteSmoothing;
            processor.LocalSmoothing = _cachedLocalSmoothing;
            processor.RemoteSmoothing = _cachedRemoteSmoothing;
            processor.IsRemoteConnection = _cachedIsRemoteConnection;

            // Position carries the smoothing pair and the limits inside PositionSettings,
            // so the whole struct is rebuilt here; only the connection flag is runtime
            // state on the processor.
            if (_positionProcessor != null)
            {
                _positionProcessor.Settings = BuildPositionSettings();
                _positionProcessor.IsRemoteConnection = _cachedIsRemoteConnection;
            }

            Logger.LogInfo("Processor settings updated from configuration");
        }

    }
}
