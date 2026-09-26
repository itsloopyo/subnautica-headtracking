using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Tracking;
using SubnauticaHeadTracking.Config;
using SubnauticaHeadTracking.State;
using Xunit;

namespace SubnauticaHeadTracking.Tests
{
    /// <summary>
    /// The committed config is the table's fresh render byte for byte, the owner creates those
    /// bytes, and a save writes only the rows the mod's toggles persist.
    /// <c>pixi run render-config</c> sets CAMERAUNLOCK_RENDER_CONFIG=write to rewrite the
    /// committed file after a change to a row, a comment or a default.
    /// </summary>
    public class ConfigTests : IDisposable
    {
        private const string CommittedPath = "config/CameraUnlock.ini";

        private readonly string _scratch = Path.Combine(Path.GetTempPath(), "subnautica-config-tests-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_scratch)) Directory.Delete(_scratch, true);
        }

        private static string Committed()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "pixi.toml"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("no pixi.toml above " + AppDomain.CurrentDomain.BaseDirectory);
            return Path.Combine(dir.FullName, CommittedPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static byte[] RenderFresh()
        {
            return SubnauticaConfig.Table().RenderFresh(new RenderHeader(SubnauticaConfig.DisplayName));
        }

        private string DefaultsPath()
        {
            return Path.Combine(Path.Combine(_scratch, "global"), "Defaults.ini");
        }

        private ConfigOwner<SubnauticaConfig> Owner()
        {
            string folder = Path.Combine(_scratch, "config");
            Directory.CreateDirectory(folder);
            return new ConfigOwner<SubnauticaConfig>(SubnauticaConfigOwner.Options(folder, path => new ConfigFile(path, false),
                DefaultsFile.At(DefaultsPath())));
        }

        private string ConfigPath()
        {
            return Path.Combine(Path.Combine(_scratch, "config"), SubnauticaConfigOwner.FileName);
        }

        [Fact]
        public void CommittedFileIsTheRenderedDefaults()
        {
            byte[] rendered = RenderFresh();
            string mode = Environment.GetEnvironmentVariable("CAMERAUNLOCK_RENDER_CONFIG");
            if (mode == "write")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Committed()));
                File.WriteAllBytes(Committed(), rendered);
                return;
            }
            Assert.True(string.IsNullOrEmpty(mode), "CAMERAUNLOCK_RENDER_CONFIG is '" + mode + "'; only 'write' is read");
            Assert.True(File.Exists(Committed()), CommittedPath + " is missing; run pixi run render-config");
            Assert.True(rendered.SequenceEqual(File.ReadAllBytes(Committed())),
                CommittedPath + " differs from the table's defaults; run pixi run render-config");
        }

        [Fact]
        public void TheOwnerCreatesTheCommittedFileAndDefaultsIni()
        {
            ConfigLoadResult<SubnauticaConfig> loaded = Owner().Load();

            Assert.Equal(ConfigLoadStatus.Created, loaded.Status);
            Assert.Equal(RenderFresh(), File.ReadAllBytes(ConfigPath()));
            Assert.True(File.Exists(DefaultsPath()));
        }

        [Fact]
        public void AFreshFileRunsOnTheFleetDefaultsWithYawAroundTheCamera()
        {
            SubnauticaConfig config = Owner().Load().Config;

            Assert.Equal(4242, config.UdpPort);
            Assert.Equal("End, Ctrl+Shift+Y", config.ToggleKeyName);
            Assert.Equal("PageUp, Ctrl+Shift+G", config.CycleTrackingModeKeyName);
            Assert.Equal("PageDown, Ctrl+Shift+H", config.YawModeKeyName);
            Assert.Equal("Ctrl+Shift+J", config.CyclePortKeyName);
            StartupState startup = StartupState.From(config);
            Assert.True(startup.Enabled);
            Assert.Equal(TrackingMode.RotationAndPosition, startup.Mode);
            Assert.False(startup.WorldSpaceYaw);
            Assert.Equal(0.3f, config.Position.LimitX);
            Assert.Equal(0.2f, config.Position.LimitY);
            Assert.Equal(0.2f, config.Position.LimitYDown);
            Assert.Equal(0.4f, config.Position.LimitZ);
            Assert.Equal(0.1f, config.Position.LimitZBack);
        }

        [Fact]
        public void EveryHotkeyListParses()
        {
            SubnauticaConfig config = Owner().Load().Config;
            foreach (string list in new[] { config.ToggleKeyName, config.CycleTrackingModeKeyName, config.YawModeKeyName, config.CyclePortKeyName })
            {
                Assert.True(KeyBindings.TryParse(list, out KeyBinding[] bindings, out string error), error);
                Assert.NotEmpty(bindings);
            }
        }

        [Fact]
        public void TheModeCycleSavesBothModeRowsAndNothingElse()
        {
            ConfigOwner<SubnauticaConfig> owner = Owner();
            owner.Load();
            string before = File.ReadAllText(ConfigPath());
            byte[] defaults = File.ReadAllBytes(DefaultsPath());

            ConfigSaveResult saved = owner.Save(c =>
            {
                c.RotationEnabled = true;
                c.PositionEnabled = false;
            });

            Assert.Equal(ConfigSaveStatus.Saved, saved.Status);
            string after = File.ReadAllText(ConfigPath());
            Assert.Equal(before.Replace("RotationEnabled=default", "RotationEnabled=true")
                .Replace("PositionEnabled=default", "PositionEnabled=false"), after);
            Assert.Contains(saved.Log, line => line.Contains("PositionEnabled=false is now set for this game"));
            Assert.Equal(defaults, File.ReadAllBytes(DefaultsPath()));
            Assert.Equal(TrackingMode.RotationOnly, StartupState.From(Owner().Load().Config).Mode);
        }

        [Fact]
        public void TheYawToggleSavesWorldSpaceYawAndNothingElse()
        {
            ConfigOwner<SubnauticaConfig> owner = Owner();
            owner.Load();
            string before = File.ReadAllText(ConfigPath());

            Assert.Equal(ConfigSaveStatus.Saved, owner.Save(c => c.WorldSpaceYaw = true).Status);

            Assert.Equal(before.Replace("WorldSpaceYaw=false", "WorldSpaceYaw=true"), File.ReadAllText(ConfigPath()));
            Assert.True(StartupState.From(Owner().Load().Config).WorldSpaceYaw);
        }

        [Fact]
        public void EnableOnStartupIsNeverSaved()
        {
            ConfigOwner<SubnauticaConfig> owner = Owner();
            owner.Load();
            byte[] before = File.ReadAllBytes(ConfigPath());

            Assert.Throws<InvalidOperationException>(() => owner.Save(c => c.EnableOnStartup = false));
            Assert.Equal(before, File.ReadAllBytes(ConfigPath()));
        }

        [Fact]
        public void DefaultRowsFollowDefaultsIniAndWorldSpaceYawDoesNot()
        {
            Owner().Load();
            string defaults = File.ReadAllText(DefaultsPath());
            Assert.Contains("WorldSpaceYaw=true", defaults);
            defaults = defaults.Replace("ToggleKey=End, Ctrl+Shift+Y", "ToggleKey=F8").Replace("UdpPort=4242", "UdpPort=4250");
            File.WriteAllText(DefaultsPath(), defaults, new UTF8Encoding(false));

            SubnauticaConfig config = Owner().Load().Config;

            Assert.Equal("F8", config.ToggleKeyName);
            Assert.Equal(4250, config.UdpPort);
            Assert.False(config.WorldSpaceYaw);
        }
    }
}
