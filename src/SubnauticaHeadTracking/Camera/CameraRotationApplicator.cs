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
        private static readonly PoseInterpolator poseInterpolator = new PoseInterpolator
        {
            // Default 0.1s decays velocity to 56% within a 30Hz sample period (33ms),
            // causing a 44% snap at every sample boundary. At 0.5s the decay is only 12%,
            // keeping predictions accurate between samples so the processor just absorbs
            // the small residual error instead of chasing large steps.
            MaxExtrapolationTime = 0.5f
        };

        // Position processing
        private static PositionProcessor _positionProcessor;
        private static PositionInterpolator _positionInterpolator;
        private static bool _positionEnabled = true;

        // Swim body avoidance: offset camera forward+down when swimming
        private static float _smoothedSwimBlend;
        private static readonly Vector3 SwimOffset = new Vector3(0f, -0.025f, 0.025f); // down + forward in view space
        private const float SwimBlendSpeed = 5f;

        // Cached settings to avoid per-frame config reads and struct creation
        private static bool _settingsDirty = true;
        private static SensitivitySettings _cachedSensitivity;
        private static DeadzoneSettings _cachedDeadzone;
        private static float _cachedSmoothingFactor;
        private static float _cachedPositionLimitY;
        private static float _cachedPositionLimitYDown;
        private static float _cachedPositionLimitZBack;

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

        /// <summary>Whether positional tracking is enabled.</summary>
        public static bool PositionEnabled
        {
            get => _positionEnabled;
            set => _positionEnabled = value;
        }

        /// <summary>
        /// Initializes position processing components.
        /// </summary>
        public static void InitializePosition()
        {
            _positionProcessor = new PositionProcessor
            {
                Settings = new PositionSettings(
                    Config.ConfigurationManager.PositionSensitivityX.Value,
                    Config.ConfigurationManager.PositionSensitivityY.Value,
                    Config.ConfigurationManager.PositionSensitivityZ.Value,
                    Config.ConfigurationManager.PositionLimitX.Value,
                    Config.ConfigurationManager.PositionLimitY.Value,
                    // Tracker reports negative Z for forward lean, so the generous forward
                    // limit (PositionLimitZ) must be on the negative side of the clamp.
                    // Clamp(z, -limitZBack, limitZ) → swap config values so forward gets 0.40m.
                    Config.ConfigurationManager.PositionLimitZBack.Value,
                    Config.ConfigurationManager.PositionLimitZ.Value,
                    Config.ConfigurationManager.PositionSmoothing.Value,
                    invertX: true, invertY: false, invertZ: false
                )
            };
            _positionInterpolator = new PositionInterpolator();
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

            // Cache Time.deltaTime — single native interop read instead of five
            float dt = Time.deltaTime;

            // Get raw pose with timestamp (needed for PoseInterpolator new-sample detection)
            TrackingPose rawPose = receiver.GetRawPose();

            // Update processor settings from config (in case they changed)
            UpdateProcessorSettings();

            // Interpolate between tracking samples (30Hz → display rate via velocity extrapolation)
            TrackingPose interpolatedPose = poseInterpolator.Update(rawPose, dt);

            // Always feed interpolated data — the interpolator fills frames with velocity-
            // predicted values, and the processor's baseline smoothing absorbs the small
            // residual error at sample boundaries.
            TrackingPose processedPose = processor.Process(interpolatedPose, dt);

            // Store processed values for reticle compensation
            CurrentYaw = processedPose.Yaw;
            CurrentPitch = processedPose.Pitch;
            CurrentRoll = processedPose.Roll;

            // Capture the original (game-computed) view matrix before we modify it.
            cam.ResetWorldToCameraMatrix();
            OriginalViewMatrix = cam.worldToCameraMatrix;

            // Build head rotation with correct composition order: yaw → pitch → roll
            // in view-space (right-to-left multiplication). This prevents pitch from
            // appearing as roll at extreme yaw angles.
            Quaternion yawQ = Quaternion.AngleAxis(CurrentYaw, Vector3.up);
            Quaternion pitchQ = Quaternion.AngleAxis(CurrentPitch, Vector3.right);
            Quaternion rollQ = Quaternion.AngleAxis(-CurrentRoll, Vector3.forward);
            Quaternion headRotUnity = rollQ * pitchQ * yawQ;

            // Compute head rotation matrix directly — avoids ViewMatrixModifier which
            // would redundantly ResetWorldToCameraMatrix and re-read the matrix we
            // already captured above.
            Matrix4x4 headRotMatrix = Matrix4x4.Rotate(headRotUnity);

            // Accumulate view-space offset from position tracking and swim avoidance.
            // Both are applied in original view space so offsets follow body orientation.
            // Combined into a single matrix composition to eliminate redundant
            // OriginalViewMatrix.inverse computations and intermediate matrix writes.
            Vector3 totalViewOffset = Vector3.zero;

            // Position processing: compute offset (rendering-only, like rotation)
            if (_positionEnabled && _positionProcessor != null && _positionInterpolator != null)
            {
                var rawPos = receiver.GetLatestPosition();
                var interpolatedPos = _positionInterpolator.Update(rawPos, dt);
                var headRotQ = new Quat4(headRotUnity.x, headRotUnity.y, headRotUnity.z, headRotUnity.w);
                Vec3 posOffset = _positionProcessor.Process(interpolatedPos, headRotQ, dt);

                Vector3 offset = new Vector3(posOffset.X, posOffset.Y, posOffset.Z);

                // Clamp: Z symmetric (ZBack both dirs), Y uses separate up/down limits.
                offset.z = Mathf.Clamp(offset.z, -_cachedPositionLimitZBack, _cachedPositionLimitZBack);
                offset.y = Mathf.Clamp(offset.y, -_cachedPositionLimitYDown, _cachedPositionLimitY);

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
                Logger.LogInfo($"Baseline smoothing={SmoothingUtils.BaselineSmoothing}, extrapolation window={poseInterpolator.MaxExtrapolationTime}s");
                Logger.LogInfo("Head tracking uses camera-local rotation (no horizon lock — swimming-safe)");
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
        /// Only rebuilds settings when they are marked dirty (config changed).
        /// </summary>
        private static void UpdateProcessorSettings()
        {
            if (!_settingsDirty) return;

            _settingsDirty = false;

            // Cache and apply sensitivity settings
            _cachedSensitivity = new SensitivitySettings(
                Config.ConfigurationManager.YawSensitivity.Value,
                Config.ConfigurationManager.PitchSensitivity.Value,
                Config.ConfigurationManager.RollSensitivity.Value,
                Config.ConfigurationManager.YawInvert.Value,
                Config.ConfigurationManager.PitchInvert.Value,
                Config.ConfigurationManager.RollInvert.Value
            );
            processor.Sensitivity = _cachedSensitivity;

            // Cache and apply deadzone settings
            _cachedDeadzone = new DeadzoneSettings(
                Config.ConfigurationManager.YawDeadzone.Value,
                Config.ConfigurationManager.PitchDeadzone.Value,
                Config.ConfigurationManager.RollDeadzone.Value
            );
            processor.Deadzone = _cachedDeadzone;

            // The library's GetEffectiveSmoothing applies BaselineSmoothing as a floor
            // unconditionally, so just pass the user's value through.
            _cachedSmoothingFactor = Config.ConfigurationManager.SmoothingFactor.Value;
            processor.SmoothingFactor = _cachedSmoothingFactor;

            // Cache position limits to avoid per-frame ConfigEntry.Value reads
            _cachedPositionLimitY = Config.ConfigurationManager.PositionLimitY.Value;
            _cachedPositionLimitYDown = Config.ConfigurationManager.PositionLimitYDown.Value;
            _cachedPositionLimitZBack = Config.ConfigurationManager.PositionLimitZBack.Value;

            Logger.LogInfo("Processor settings updated from configuration");
        }

        /// <summary>
        /// Recenters tracking by capturing current head position as neutral.
        /// </summary>
        /// <param name="yaw">Current raw yaw from receiver</param>
        /// <param name="pitch">Current raw pitch from receiver</param>
        /// <param name="roll">Current raw roll from receiver</param>
        public static void Recenter(float yaw, float pitch, float roll)
        {
            var currentPose = new TrackingPose(yaw, pitch, roll);
            processor.RecenterTo(currentPose);
            poseInterpolator.Reset();
            var receiver = HeadTrackingPlugin.Receiver;
            if (receiver != null)
            {
                _positionProcessor?.SetCenter(receiver.GetLatestPosition());
            }
            _positionInterpolator?.Reset();
            Logger.LogInfo($"Head tracking recentered to: Yaw={yaw:F2}°, Pitch={pitch:F2}°, Roll={roll:F2}°");
        }

        /// <summary>
        /// Resets all tracking state. Called when exiting gameplay to ensure
        /// the main menu camera is not affected by stale head tracking data.
        /// </summary>
        public static void ResetState()
        {
            processor.Reset();
            poseInterpolator.Reset();
            CurrentYaw = 0f;
            CurrentPitch = 0f;
            CurrentRoll = 0f;
            CurrentPositionOffset = Vector3.zero;
            OriginalViewMatrix = Matrix4x4.identity;
            _hasLoggedFirstApplication = false;
            _settingsDirty = true;
            _smoothedSwimBlend = 0f;
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
        }
    }
}
