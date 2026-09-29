using BepInEx;
using BepInEx.Logging;
using System;
using CameraUnlock.Core.Protocol;
using SubnauticaHeadTracking.Config;
using UnityEngine;

namespace SubnauticaHeadTracking
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class HeadTrackingPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource ModLogger { get; private set; }

        // Static references to keep everything alive regardless of GameObject destruction
        private static bool initialized;
        private static OpenTrackReceiver staticReceiver;
        private static UnityEngine.Camera.CameraCallback staticPreCullCallback;
        private static UnityEngine.Camera.CameraCallback staticPreRenderCallback;
        private static UnityEngine.Camera.CameraCallback staticPostRenderCallback;

        // Tracks whether the camera is in "manual matrix" mode from a previous frame.
        // When head tracking stops (toggled off, left gameplay, signal lost), we must
        // call ResetWorldToCameraMatrix to give control back to the transform.
        private static bool _viewMatrixOverridden;

        // Latched: the log must be able to answer "did any tracker packet ever reach
        // the mod", separately from whether tracking was applied. Without it a wrong
        // port, a firewall block and a gameplay gate all look identical in the log.
        private static bool _hasLoggedFirstPacket;

        // Per-frame caches - Camera.main does FindGameObjectWithTag internally,
        // and IsInActiveGameplay does 3+ reflection calls. Both are called from
        // multiple callbacks per frame but their results can't change within a frame.
        private static int _mainCameraFrame = -1;
        private static UnityEngine.Camera _mainCameraCache;
        private static int _gameplayFrame = -1;
        private static bool _gameplayCache;
        private static int _preCullFrame = -1;

        private static UnityEngine.Camera GetMainCamera()
        {
            int frame = Time.frameCount;
            if (_mainCameraFrame != frame)
            {
                _mainCameraCache = UnityEngine.Camera.main;
                _mainCameraFrame = frame;
            }
            return _mainCameraCache;
        }

        private static bool CheckGameplay()
        {
            int frame = Time.frameCount;
            if (_gameplayFrame != frame)
            {
                _gameplayCache = GameState.GameplayDetector.IsInActiveGameplay();
                _gameplayFrame = frame;
            }
            return _gameplayCache;
        }

        void Awake()
        {
            // Prevent duplicate initialization (can happen if BepInEx reloads)
            if (initialized)
            {
                Logger.LogWarning("Plugin already initialized, skipping duplicate Awake");
                return;
            }

            ModLogger = Logger;
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} initializing...");

            try
            {
                InitializeConfiguration();
                InitializeUdpListener();
                Camera.CameraRotationApplicator.Initialize(Settings.Current);
                InitializeCameraCallback();
                LogStartupInformation();
                initialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize {PluginInfo.PLUGIN_NAME}: {ex.Message}");
                Logger.LogError(ex.StackTrace);
                throw;
            }
        }

        private void InitializeConfiguration()
        {
            Settings.Load(Config, Logger);
            State.TrackingState.Initialize(Settings.Current);
            Input.HotkeyHandler.Initialize(Settings.Current);
        }

        private const int PortRangeBase = 4242;
        private const int PortRangeSize = 4;
        internal static int CurrentPort { get; private set; }

        private void InitializeUdpListener()
        {
            Logger.LogInfo("Starting UDP receiver...");
            staticReceiver = new OpenTrackReceiver();
            staticReceiver.Log = msg => Logger.LogInfo(msg);
            CurrentPort = Settings.Current.UdpPort;
            staticReceiver.Start(CurrentPort);
            Logger.LogInfo($"UDP receiver started on port {CurrentPort}");
        }

        internal static void CyclePort()
        {
            // A configured port outside the range steps into it; C#'s % keeps the sign of a
            // port below the range, so the remainder is folded back to non-negative.
            int step = ((CurrentPort - PortRangeBase + 1) % PortRangeSize + PortRangeSize) % PortRangeSize;
            int nextPort = PortRangeBase + step;

            staticReceiver.Stop();
            bool listening = staticReceiver.Start(nextPort);
            CurrentPort = nextPort;
            ModLogger.LogInfo(listening
                ? $"UDP port cycled to {CurrentPort} - configure OpenTrack to send to this port"
                : $"UDP port cycled to {CurrentPort}, which could not be bound yet - the receiver keeps retrying it");
        }

        private void InitializeCameraCallback()
        {
            Logger.LogInfo("Registering camera callbacks for view matrix modification...");

            staticPreCullCallback = OnCameraPreCullStatic;
            staticPreRenderCallback = OnCameraPreRenderStatic;
            staticPostRenderCallback = OnCameraPostRenderStatic;

            UnityEngine.Camera.onPreCull += staticPreCullCallback;
            UnityEngine.Camera.onPreRender += staticPreRenderCallback;
            UnityEngine.Camera.onPostRender += staticPostRenderCallback;

            Canvas.willRenderCanvases += OnWillRenderCanvasesStatic;

            Logger.LogInfo("Camera callbacks registered - head tracking will modify view matrix only (not game logic)");
        }

        private static void OnCameraPreCullStatic(UnityEngine.Camera cam)
        {
            if (!initialized) return;
            if (cam != GetMainCamera()) return;

            // Once per frame: a second render of the main camera in the same frame would
            // otherwise read each hotkey press twice and advance the pose pipeline twice.
            // The view matrix set below persists for any later render this frame.
            int frame = Time.frameCount;
            if (frame == _preCullFrame) return;
            _preCullFrame = frame;

            // Hotkeys must be checked before the IsEnabled guard so the toggle
            // hotkey can re-enable tracking.
            Input.HotkeyHandler.CheckHotkeys();

            bool receiverActive = staticReceiver.IsReceiving;
            if (receiverActive && !_hasLoggedFirstPacket)
            {
                _hasLoggedFirstPacket = true;
                ModLogger.LogInfo($"First tracker packet received on port {CurrentPort} (remote sender: {staticReceiver.IsRemoteConnection})");
            }

            if (!receiverActive || !State.TrackingState.IsEnabled || !CheckGameplay())
            {
                StopDrawingTrackedView(cam);
                return;
            }

            // Always process tracking data to keep processor/interpolator warm.
            // This ensures seamless resume when PDA closes - no stale data, no jump.
            Camera.CameraRotationApplicator.ApplyViewMatrixRotation(cam, staticReceiver);

            UI.PDACompensation.UpdateState();
            if (UI.PDACompensation.IsPDAOpen)
            {
                // ApplyViewMatrixRotation has just written the matrix, so the stop path must reset it.
                _viewMatrixOverridden = true;
                StopDrawingTrackedView(cam);
                return;
            }

            _viewMatrixOverridden = true;

            UI.PlayerHeadHider.Hide();
            UI.ReticleCompensation.UpdatePosition(cam);
            UI.PingCompensation.TryFindCanvas();
        }

        /// <summary>
        /// Hands the view back to the camera's transform and undoes everything drawn for the
        /// tracked view, so mouse look drives an unmodified view again.
        /// </summary>
        private static void StopDrawingTrackedView(UnityEngine.Camera cam)
        {
            if (!_viewMatrixOverridden) return;
            _viewMatrixOverridden = false;
            cam.ResetWorldToCameraMatrix();
            UI.PlayerHeadHider.Show();
            UI.ReticleCompensation.Restore();
        }

        private static void OnCameraPreRenderStatic(UnityEngine.Camera cam)
        {
            if (!initialized) return;
            if (cam != GetMainCamera()) return;
            if (!_viewMatrixOverridden) return;

            // Mask compensation runs in onPreRender (after the game's
            // OnWillRenderObject has finished positioning the mask) so our
            // H-matrix correction isn't overwritten by game code.
            UI.PlayerMaskCompensation.TryFind();
            UI.PlayerMaskCompensation.ApplyCompensation(cam);
        }

        private static void OnCameraPostRenderStatic(UnityEngine.Camera cam)
        {
            if (!initialized) return;
            if (cam != GetMainCamera()) return;

            UI.PlayerMaskCompensation.RestorePosition();
        }

        private static void OnWillRenderCanvasesStatic()
        {
            if (!initialized) return;
            if (!State.TrackingState.IsEnabled) return;
            if (!CheckGameplay()) return;
            // Only compensate when the tracked view matrix is actually applied this frame,
            // so PingCompensation reads a current, consistent OriginalViewMatrix.
            if (!_viewMatrixOverridden) return;

            var cam = GetMainCamera();
            if (cam == null) return;

            UI.PingCompensation.Reposition(cam);
        }

        private void LogStartupInformation()
        {
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded successfully");
            Logger.LogInfo($"Press {Settings.Current.ToggleKeyName} to toggle tracking");
            Logger.LogInfo($"Tracking is {(State.TrackingState.IsEnabled ? "ENABLED" : "DISABLED")} at startup (EnableOnStartup)");
        }

        void OnDestroy()
        {
            // NEVER clean up on destroy - we use static references that survive
            // Only clean up on application quit
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} component destroyed - static resources preserved");
        }

        void OnApplicationQuit()
        {
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} application quitting - cleaning up...");

            CleanupCameraCallback();
            CleanupUdpListener();

            initialized = false;

            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} cleanup complete");
        }

        private void CleanupCameraCallback()
        {
            if (staticPreCullCallback != null)
            {
                UnityEngine.Camera.onPreCull -= staticPreCullCallback;
                ModLogger?.LogInfo("Camera pre-cull callback unregistered");
                staticPreCullCallback = null;
            }

            if (staticPreRenderCallback != null)
            {
                UnityEngine.Camera.onPreRender -= staticPreRenderCallback;
                ModLogger?.LogInfo("Camera pre-render callback unregistered");
                staticPreRenderCallback = null;
            }

            if (staticPostRenderCallback != null)
            {
                UnityEngine.Camera.onPostRender -= staticPostRenderCallback;
                ModLogger?.LogInfo("Camera post-render callback unregistered");
                staticPostRenderCallback = null;
            }

            Canvas.willRenderCanvases -= OnWillRenderCanvasesStatic;
            ModLogger?.LogInfo("Canvas willRenderCanvases callback unregistered");
        }

        private void CleanupUdpListener()
        {
            if (staticReceiver == null)
            {
                return;
            }

            staticReceiver.Dispose();
            ModLogger?.LogInfo("UDP receiver stopped");
            staticReceiver = null;
        }
    }
}
