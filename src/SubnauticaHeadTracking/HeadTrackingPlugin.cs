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

        /// <summary>
        /// Gets the core receiver instance for checking connection state.
        /// </summary>
        public static OpenTrackReceiver Receiver { get; private set; }

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

        // Auto-recenter: detect false→true transitions of IsReceiving and recenter
        // after a few stabilization frames so the first tracking data doesn't cause a jump.
        private static bool _wasReceiving;
        private static bool _wasTracking;
        private static int _stabilizationFramesRemaining;
        private const int StabilizationFrameCount = 5;

        // Per-frame caches — Camera.main does FindGameObjectWithTag internally,
        // and IsInActiveGameplay does 3+ reflection calls. Both are called from
        // multiple callbacks per frame but their results can't change within a frame.
        private static int _mainCameraFrame = -1;
        private static UnityEngine.Camera _mainCameraCache;
        private static int _gameplayFrame = -1;
        private static bool _gameplayCache;

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
                Camera.CameraRotationApplicator.InitializePosition();
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
            Config.SettingChanged += OnConfigSettingChanged;
            Config.ConfigReloaded += (sender, args) =>
            {
                Logger.LogInfo("Configuration reloaded from file");
            };

            Logger.LogInfo("Initializing configuration...");
            ConfigurationManager.Initialize(Config);
            Logger.LogInfo("Configuration initialized successfully");
        }

        private const int PortRangeBase = 4242;
        private const int PortRangeSize = 4;
        internal static int CurrentPort { get; private set; }

        private void InitializeUdpListener()
        {
            Logger.LogInfo("Starting UDP receiver...");
            staticReceiver = new OpenTrackReceiver();
            staticReceiver.Log = msg => Logger.LogInfo(msg);
            CurrentPort = ConfigurationManager.UdpPort.Value;
            staticReceiver.Start(CurrentPort);
            Receiver = staticReceiver;
            Logger.LogInfo($"UDP receiver started on port {CurrentPort}");
        }

        internal static void CyclePort()
        {
            int nextPort = PortRangeBase + ((CurrentPort - PortRangeBase + 1) % PortRangeSize);

            staticReceiver.Stop();
            staticReceiver.Start(nextPort);
            CurrentPort = nextPort;
            ModLogger.LogInfo($"UDP port cycled to {CurrentPort} - configure OpenTrack to send to this port");
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

            // Hotkeys must be checked before the IsEnabled guard so the toggle
            // hotkey can re-enable tracking. Runs once per frame (main camera only).
            if (staticReceiver != null)
                Input.HotkeyHandler.CheckHotkeys();

            // Detect PDA open/close for view matrix suppression
            UI.PDACompensation.TryFind();
            UI.PDACompensation.UpdateState();

            // Determine if head tracking should be active this frame
            bool shouldTrack = State.TrackingState.IsEnabled && CheckGameplay();

            if (shouldTrack && staticReceiver == null)
            {
                ModLogger?.LogError("CRITICAL: staticReceiver is null after initialization - disabling head tracking");
                initialized = false;
                shouldTrack = false;
            }

            // Track actual receiver connection state independently of gameplay
            // state so that pause/unpause doesn't trigger a false reconnection.
            bool receiverActive = staticReceiver != null && staticReceiver.IsReceiving;
            if (!receiverActive)
                shouldTrack = false;

            // Auto-recenter only on genuine tracker (re)connection
            if (receiverActive && !_wasReceiving)
            {
                _stabilizationFramesRemaining = StabilizationFrameCount;
                ModLogger?.LogInfo("Tracker connected — stabilizing before auto-recenter");
            }
            if (receiverActive && _stabilizationFramesRemaining > 0)
            {
                _stabilizationFramesRemaining--;
                if (_stabilizationFramesRemaining == 0)
                {
                    staticReceiver.GetRawRotation(out float yaw, out float pitch, out float roll);
                    Camera.CameraRotationApplicator.Recenter(yaw, pitch, roll);
                    ModLogger?.LogInfo($"Auto-recentered: Yaw={yaw:F2}, Pitch={pitch:F2}, Roll={roll:F2}");
                }
            }
            _wasReceiving = receiverActive;

            // Recenter when tracking resumes (spawn, scene load, unpause)
            if (shouldTrack && !_wasTracking && receiverActive)
            {
                staticReceiver.GetRawRotation(out float ry, out float rp, out float rr);
                Camera.CameraRotationApplicator.Recenter(ry, rp, rr);
                ModLogger?.LogInfo("Auto-recentered on gameplay start");
            }
            _wasTracking = shouldTrack;

            if (!shouldTrack)
            {
                // If the camera was in manual matrix mode, reset it so the transform
                // drives the view again (mouse look works, no frozen view).
                if (_viewMatrixOverridden)
                {
                    cam.ResetWorldToCameraMatrix();
                    _viewMatrixOverridden = false;
                    UI.PlayerHeadHider.Show();
                }
                return;
            }

            // Always process tracking data to keep processor/interpolator warm.
            // This ensures seamless resume when PDA closes — no stale data, no jump.
            Camera.CameraRotationApplicator.ApplyViewMatrixRotation(cam, staticReceiver);

            if (UI.PDACompensation.IsPDAOpen)
            {
                // PDA open: undo view matrix but processor stays current
                cam.ResetWorldToCameraMatrix();
                _viewMatrixOverridden = false;
                return;
            }

            _viewMatrixOverridden = true;

            UI.PlayerHeadHider.TryFind();
            UI.PlayerHeadHider.Hide();
            UI.ReticleCompensation.UpdatePosition(cam);
            UI.PingCompensation.TryFindCanvas();
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

            var cam = GetMainCamera();
            if (cam == null) return;

            float pitch = Camera.CameraRotationApplicator.CurrentPitch;
            float yaw = Camera.CameraRotationApplicator.CurrentYaw;
            float roll = Camera.CameraRotationApplicator.CurrentRoll;

            if (Mathf.Abs(yaw) < 0.01f && Mathf.Abs(pitch) < 0.01f && Mathf.Abs(roll) < 0.01f)
                return;

            UI.PingCompensation.Reposition(cam, yaw, pitch, roll);
        }

        private void LogStartupInformation()
        {
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded successfully");
            Logger.LogInfo($"Press {ConfigurationManager.ToggleHotkey.Value} to toggle tracking");
            Logger.LogInfo($"Press {ConfigurationManager.RecenterHotkey.Value} to recenter tracking");
            Logger.LogInfo($"Tracking is {(State.TrackingState.IsEnabled ? "ENABLED" : "DISABLED")} by default");
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
            Receiver = null;
        }

        private void OnConfigSettingChanged(object sender, EventArgs e)
        {
            Camera.CameraRotationApplicator.MarkSettingsDirty();
            Input.HotkeyHandler.InvalidateCache();

            if (sender is BepInEx.Configuration.ConfigEntry<int> intEntry)
            {
                if (intEntry.Definition.Key == "UdpPort")
                {
                    Logger.LogWarning("UDP port changed - restart game for changes to take effect");
                }
            }
            else if (sender is BepInEx.Configuration.ConfigEntry<string> stringEntry)
            {
                if (stringEntry.Definition.Key == "BindAddress")
                {
                    Logger.LogWarning("Bind address changed - restart game for changes to take effect");
                }
            }
        }
    }
}
