using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Tracking;
using SubnauticaHeadTracking.Camera;
using SubnauticaHeadTracking.Config;
using SubnauticaHeadTracking.Legacy;
using SubnauticaHeadTracking.State;
using UnityEngine;
using Xunit;

namespace SubnauticaHeadTracking.ConfigDifferential
{
    /// <summary>
    /// Comparison 2: the import against the migration. For every input the import reads the
    /// startup state and the hotkey bindings v1.4.0 ran on, and leaves to Defaults.ini exactly
    /// the rows the player never changed from v1.4.0's defaults. The owner, given only the legacy
    /// .cfg, creates a CameraUnlock.ini that holds default on each of those rows, reads back as
    /// what the import read on every other row and as Defaults.ini on those, and leaves the .cfg
    /// as it was.
    /// </summary>
    [Collection(OracleCollection.Name)]
    public class MigrationTests : IDisposable
    {
        private readonly string _scratch = Path.Combine(Path.GetTempPath(), "subnautica-config-migration-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_scratch)) Directory.Delete(_scratch, true);
        }

        // The rows a new file holds on its defaults where v1.4.0, with no file, ran on its own:
        // the defaults the conversion moved. They differ only for the no-file input.
        private static readonly string[] MovedDefaults =
        {
            "[Position] PositionLimitY", "[Position] PositionLimitYDown", "[Position] PositionLimitZBack",
            "[Hotkeys] YawModeKey",
        };

        // A Defaults.ini that differs from the built-in values on every global row the table binds.
        private const string OtherDefaults =
            "[Network]\r\nUdpPort=4250\r\n" +
            "[General]\r\nEnableOnStartup=false\r\nWorldSpaceYaw=true\r\nRotationEnabled=false\r\n" +
            "[Smoothing]\r\nLocalSmoothing=0.5\r\nRemoteSmoothing=0.6\r\n" +
            "[Position]\r\nPositionEnabled=true\r\nPositionLimitX=0.25\r\nPositionLimitY=0.25\r\nPositionLimitYDown=0.25\r\n" +
            "PositionLimitZ=0.25\r\nPositionLimitZBack=0.25\r\n" +
            "[Hotkeys]\r\nToggleKey=F5\r\nCycleTrackingModeKey=F6\r\nYawModeKey=F7\r\n";

        private enum Globals
        {
            Created,
            Other,
        }

        [Fact]
        public void EveryInputMigratesToWhatTheImportRead()
        {
            string migrated = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrated");
            if (Directory.Exists(migrated)) Directory.Delete(migrated, true);
            Directory.CreateDirectory(migrated);

            List<KeyValuePair<Input, Reading>> published = Readers.PublishedAll();
            var failures = new ConcurrentBag<string>();
            var outcomes = new ConcurrentDictionary<string, int>();
            var files = new ConcurrentDictionary<string, byte[]>();
            Parallel.For(0, published.Count, i =>
            {
                string dir = Path.Combine(_scratch, i.ToString());
                Input input = published[i].Key;
                var check = new Check(input, published[i].Value, dir, files);
                check.Run();
                foreach (string failure in check.Failures) failures.Add(input.Name + ": " + failure);
                outcomes.AddOrUpdate(check.Outcome, 1, (k, n) => n + 1);
                Directory.Delete(dir, true);
            });

            foreach (KeyValuePair<string, byte[]> file in files) File.WriteAllBytes(Path.Combine(migrated, file.Key + ".ini"), file.Value);
            string summary = string.Join(", ", outcomes.OrderBy(o => o.Key).Select(o => o.Key + " " + o.Value));
            Assert.True(failures.IsEmpty, failures.Count + " failures (" + summary + "):\n" + string.Join("\n", failures.OrderBy(f => f).Take(40)));
            Assert.True(outcomes.ContainsKey("migrated") && outcomes.ContainsKey("deferred") && outcomes.ContainsKey("created")
                        && outcomes.ContainsKey("plugin load failure"), summary);
        }

        /// <summary>
        /// v1.4.0's first-run file is what every player who never changed a setting holds: its
        /// pose shaping is the mod's axis code now, so nothing is dropped, and the migrated file
        /// differs from a new one only in the defaults the conversion moved.
        /// </summary>
        [Fact]
        public void TheShippedFileFoldsEveryPoseShapingValue()
        {
            string dir = Path.Combine(_scratch, "shipped");
            string path = Readers.Place(dir, new Input("v1.4.0 first run", Inputs.FirstRun("v1.4.0")));
            SubnauticaConfig config = TableDefaults();
            ImportResult result = SubnauticaConfigImport.Create(p => new ConfigFile(p, false)).Run(new LegacyImportInput(path), config);

            Assert.Equal(ImportStatus.Imported, result.Status);
            Assert.Empty(result.Dropped);
            Assert.Equal(12, result.PoseShaping.Count);
            Assert.All(result.PoseShaping, p => Assert.True(p.Folded, "[" + p.Section + "] " + p.Key + "=" + p.Value));
            Assert.Equal("2.0", result.PoseShaping.Single(p => p.Key == "PositionSensitivityX").Shipped);
            Assert.Equal("true", result.PoseShaping.Single(p => p.Key == "PitchInvert").Shipped);

            string[] differs = RowDifferences(config, Owner(Path.Combine(dir, "fresh"), DefaultsFile.At(Path.Combine(dir, "Defaults.ini"))).Load().Config);
            Assert.Equal(MovedDefaults, differs);
            Assert.Equal(FollowingRows, result.FollowsDefaultsIni.Select(c => "[" + c.Section + "] " + c.Key).ToArray());
        }

        /// <summary>
        /// A player who never changed a setting keeps none of v1.4.0's defaults: under a
        /// Defaults.ini that differs on every row, the migrated file holds default on each global
        /// row the table does not keep for the game, the port key takes this version's, and the
        /// session runs as a new file does.
        /// </summary>
        [Fact]
        public void AnUntouchedFileFollowsDefaultsIni()
        {
            string dir = Path.Combine(_scratch, "untouched");
            string folder = Path.Combine(dir, "config");
            string defaultsPath = Path.Combine(dir, "Defaults.ini");
            Directory.CreateDirectory(dir);
            File.WriteAllText(defaultsPath, OtherDefaults, new UTF8Encoding(false));
            Readers.Place(folder, new Input("v1.4.0 first run", Inputs.FirstRun("v1.4.0")));

            ConfigLoadResult<SubnauticaConfig> loaded = Owner(folder, DefaultsFile.At(defaultsPath)).Load();
            Assert.Equal(ConfigLoadStatus.Migrated, loaded.Status);
            Dictionary<string, string> written = FileRows(Path.Combine(folder, SubnauticaConfigOwner.FileName));
            foreach (string row in FollowingRows) Assert.Equal("default", written[row]);
            Assert.Equal("false", written["[General] WorldSpaceYaw"]);

            SubnauticaConfig fresh = Owner(Path.Combine(dir, "fresh"), DefaultsFile.At(defaultsPath)).Load().Config;
            Assert.Empty(RowDifferences(fresh, loaded.Config));
        }

        // The global rows the table does not keep for the game, which v1.4.0's first-run file
        // holds at its defaults, in the order the import gives them.
        private static readonly string[] FollowingRows =
        {
            "[Network] UdpPort", "[General] EnableOnStartup", "[General] RotationEnabled", "[Position] PositionEnabled",
            "[Smoothing] LocalSmoothing", "[Smoothing] RemoteSmoothing",
            "[Position] PositionLimitX", "[Position] PositionLimitY", "[Position] PositionLimitYDown",
            "[Position] PositionLimitZ", "[Position] PositionLimitZBack",
            "[Hotkeys] ToggleKey", "[Hotkeys] CycleTrackingModeKey", "[Hotkeys] YawModeKey",
        };

        /// <summary>Each row of a canonical file as "[Section] Key" to its value text.</summary>
        internal static Dictionary<string, string> FileRows(string path)
        {
            var rows = new Dictionary<string, string>();
            string section = null;
            foreach (string line in File.ReadAllText(path).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(";")) continue;
                if (line.StartsWith("[")) section = line;
                else rows.Add(section + " " + line.Substring(0, line.IndexOf('=')), line.Substring(line.IndexOf('=') + 1));
            }
            return rows;
        }

        /// <summary>
        /// Every row as <paramref name="imported"/> holds it, except the rows
        /// <paramref name="follows"/> names, which hold <paramref name="defaults"/>' value.
        /// </summary>
        private static string[] Expected(SubnauticaConfig imported, SubnauticaConfig defaults, ICollection<ConceptDescriptor> follows)
        {
            var names = new HashSet<string>(follows.Select(c => "[" + c.Section + "]\n" + c.Key + "="));
            string[] x = Rows(imported);
            string[] y = Rows(defaults);
            var rows = new string[x.Length];
            for (int i = 0; i < x.Length; i++) rows[i] = names.Any(n => x[i].StartsWith(n, StringComparison.Ordinal)) ? y[i] : x[i];
            return rows;
        }

        internal static SubnauticaConfig TableDefaults()
        {
            var config = new SubnauticaConfig();
            SubnauticaConfig.Table().Apply(CanonicalIni.Parse(new byte[0]), config);
            return config;
        }

        internal static ConfigOwner<SubnauticaConfig> Owner(string folder, DefaultsFile defaults)
        {
            Directory.CreateDirectory(folder);
            return new ConfigOwner<SubnauticaConfig>(SubnauticaConfigOwner.Options(folder, p => new ConfigFile(p, false), defaults));
        }

        /// <summary>Every row whose value differs, as "[Section] Key", in table order.</summary>
        internal static string[] RowDifferences(SubnauticaConfig a, SubnauticaConfig b)
        {
            string[] x = Rows(a);
            string[] y = Rows(b);
            var differs = new List<string>();
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i]) differs.Add(x[i].Substring(0, x[i].IndexOf('=')).Replace("\n", " "));
            }
            return differs.ToArray();
        }

        // Each row as "[Section]\nKey=value", from the table's render, which writes every value
        // as its codec does: floats in the shortest text that reads back to the same bits.
        private static string[] Rows(SubnauticaConfig config)
        {
            string text = Encoding.ASCII.GetString(SubnauticaConfig.Table().Render(config, new RenderHeader(SubnauticaConfig.DisplayName)));
            var rows = new List<string>();
            string section = null;
            foreach (string line in text.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(";")) continue;
                if (line.StartsWith("[")) section = line;
                else rows.Add(section + "\n" + line);
            }
            return rows.ToArray();
        }

        private sealed class Check
        {
            private readonly Input _input;
            private readonly Reading _published;
            private readonly string _dir;
            private readonly ConcurrentDictionary<string, byte[]> _files;

            public Check(Input input, Reading published, string dir, ConcurrentDictionary<string, byte[]> files)
            {
                _input = input;
                _published = published;
                _dir = dir;
                _files = files;
            }

            public List<string> Failures { get; } = new List<string>();
            public string Outcome { get; private set; }

            public void Run()
            {
                SubnauticaConfig imported = TableDefaults();
                ImportResult import = null;
                string importFailure = null;
                string path = Readers.Place(Path.Combine(_dir, "import"), _input);
                try
                {
                    import = SubnauticaConfigImport.Create(p => new ConfigFile(p, false)).Run(new LegacyImportInput(path), imported);
                }
                catch (ArgumentException e)
                {
                    importFailure = e.GetType().Name + ": " + e.Message;
                }
                if (importFailure != _published.PluginLoadFailure)
                {
                    Failures.Add("the import's load failure '" + importFailure + "' is not v1.4.0's '" + _published.PluginLoadFailure + "'");
                    return;
                }

                if (import != null)
                {
                    CheckPoseShaping(import);
                    if (import.Status != ImportStatus.Undecodable) CheckFollows(import);
                    CheckStartup("the import", imported);
                    CheckHotkeys("the import", imported, import);
                }

                Migrate("writable", Globals.Created, false, import, imported);
                if (Failures.Count > 0) return;
                Migrate("read-only", Globals.Created, true, import, imported);
                Migrate("other Defaults.ini", Globals.Other, false, import, imported);
            }

            // Each pose-shaping value v1.4.0 ran on is listed with that value, folded exactly
            // when it is the shipped one, and dropped exactly when it is not.
            private void CheckPoseShaping(ImportResult import)
            {
                if (import.Status == ImportStatus.Undecodable) return;
                var expected = new Dictionary<string, string>
                {
                    { "[Sensitivity] Yaw", Text(_published.Values.YawSensitivity) },
                    { "[Sensitivity] Pitch", Text(_published.Values.PitchSensitivity) },
                    { "[Sensitivity] Roll", Text(_published.Values.RollSensitivity) },
                    { "[Deadzone] Yaw", Text(_published.Values.YawDeadzone) },
                    { "[Deadzone] Pitch", Text(_published.Values.PitchDeadzone) },
                    { "[Deadzone] Roll", Text(_published.Values.RollDeadzone) },
                    { "[Inversion] YawInvert", Text(_published.Values.YawInvert) },
                    { "[Inversion] PitchInvert", Text(_published.Values.PitchInvert) },
                    { "[Inversion] RollInvert", Text(_published.Values.RollInvert) },
                    { "[Position] PositionSensitivityX", Text(_published.Values.PositionSensitivityX) },
                    { "[Position] PositionSensitivityY", Text(_published.Values.PositionSensitivityY) },
                    { "[Position] PositionSensitivityZ", Text(_published.Values.PositionSensitivityZ) },
                };
                var dropped = new HashSet<string>();
                var modifiers = new HashSet<string>();
                foreach (DroppedValue d in import.Dropped)
                {
                    if (d.Rule == DropRule.ModifierKey) modifiers.Add(d.Key + "=" + d.Value);
                    else if (d.Rule != DropRule.PoseShaping) Failures.Add("dropped " + d.Describe() + ", which no approved change covers");
                    else dropped.Add("[" + d.Section + "] " + d.Key + "=" + d.Value);
                }
                var expectedModifiers = new HashSet<string>();
                AddModifier(expectedModifiers, "Toggle", _published.Values.ToggleHotkey);
                AddModifier(expectedModifiers, "CycleTrackingMode", _published.Values.CycleTrackingModeHotkey);
                AddModifier(expectedModifiers, "ToggleYawMode", _published.Values.ToggleYawModeHotkey);
                AddModifier(expectedModifiers, "CyclePort", _published.Values.CyclePortHotkey);
                if (!modifiers.SetEquals(expectedModifiers))
                {
                    Failures.Add("modifier keys dropped: " + string.Join(", ", modifiers) + "; expected " + string.Join(", ", expectedModifiers));
                }
                if (import.PoseShaping.Count != expected.Count) Failures.Add(import.PoseShaping.Count + " pose-shaping values, not " + expected.Count);
                foreach (PoseShapingValue p in import.PoseShaping)
                {
                    string name = "[" + p.Section + "] " + p.Key;
                    if (!expected.TryGetValue(name, out string value) || value != p.Value)
                    {
                        Failures.Add(name + "=" + p.Value + " is not what v1.4.0 ran on");
                        continue;
                    }
                    if (p.Folded != (p.Value == p.Shipped)) Failures.Add(name + " is folded " + p.Folded + " at " + p.Value + ", shipped " + p.Shipped);
                    if (p.Folded == dropped.Contains(name + "=" + p.Value)) Failures.Add(name + "=" + p.Value + " is folded " + p.Folded + " and dropped " + !p.Folded);
                }
                if (dropped.Count != import.PoseShaping.Count(p => !p.Folded)) Failures.Add("dropped values that are not pose shaping");
            }

            private static void AddModifier(HashSet<string> modifiers, string key, KeyCode code)
            {
                if (IsModifierKey(code)) modifiers.Add(key + "=" + code);
            }

            // The rows the import leaves to Defaults.ini: the startup state v1.4.0 fixed in code,
            // and each setting v1.4.0 read that holds v1.4.0's default.
            private void CheckFollows(ImportResult import)
            {
                LegacyConfig v = _published.Values;
                LegacyConfig s = Shipped.Value;
                var expected = new List<string> { "[General] EnableOnStartup", "[General] RotationEnabled", "[Position] PositionEnabled" };
                if (v.UdpPort == s.UdpPort) expected.Add("[Network] UdpPort");
                if (v.LocalSmoothing.Equals(s.LocalSmoothing)) expected.Add("[Smoothing] LocalSmoothing");
                if (v.RemoteSmoothing.Equals(s.RemoteSmoothing)) expected.Add("[Smoothing] RemoteSmoothing");
                if (v.PositionLimitX.Equals(s.PositionLimitX)) expected.Add("[Position] PositionLimitX");
                if (v.PositionLimitY.Equals(s.PositionLimitY)) expected.Add("[Position] PositionLimitY");
                if (v.PositionLimitYDown.Equals(s.PositionLimitYDown)) expected.Add("[Position] PositionLimitYDown");
                if (v.PositionLimitZ.Equals(s.PositionLimitZ)) expected.Add("[Position] PositionLimitZ");
                if (v.PositionLimitZBack.Equals(s.PositionLimitZBack)) expected.Add("[Position] PositionLimitZBack");
                if (v.ToggleHotkey == s.ToggleHotkey) expected.Add("[Hotkeys] ToggleKey");
                if (v.CycleTrackingModeHotkey == s.CycleTrackingModeHotkey) expected.Add("[Hotkeys] CycleTrackingModeKey");
                if (v.ToggleYawModeHotkey == s.ToggleYawModeHotkey && v.CyclePortHotkey == s.CyclePortHotkey) expected.Add("[Hotkeys] YawModeKey");
                var actual = import.FollowsDefaultsIni.Select(c => "[" + c.Section + "] " + c.Key).ToList();
                if (!new HashSet<string>(actual).SetEquals(expected))
                {
                    Failures.Add("follows Defaults.ini: " + string.Join(", ", actual) + "; expected " + string.Join(", ", expected));
                }
            }

            private void Migrate(string name, Globals globals, bool readOnly, ImportResult import, SubnauticaConfig imported)
            {
                string root = Path.Combine(_dir, name);
                string folder = Path.Combine(root, "config");
                string defaultsPath = Path.Combine(Path.Combine(root, "global"), "Defaults.ini");
                if (globals == Globals.Other)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(defaultsPath));
                    File.WriteAllText(defaultsPath, OtherDefaults, new UTF8Encoding(false));
                }
                string legacy = Readers.Place(folder, _input);
                if (readOnly && _input.Bytes != null) File.SetAttributes(legacy, FileAttributes.ReadOnly);
                string config = Path.Combine(folder, SubnauticaConfigOwner.FileName);
                FileState before = FileState.Of(legacy);

                ConfigLoadResult<SubnauticaConfig> loaded;
                try
                {
                    loaded = Owner(folder, DefaultsFile.At(defaultsPath)).Load();
                }
                catch (ArgumentException e)
                {
                    string failure = e.GetType().Name + ": " + e.Message;
                    if (failure != _published.PluginLoadFailure) Failures.Add(name + ": the owner threw " + failure);
                    Outcome = "plugin load failure";
                    Unlock(legacy, readOnly);
                    return;
                }
                if (_published.PluginLoadFailure != null)
                {
                    Failures.Add(name + ": v1.4.0 failed to load, and the owner loaded " + loaded.Status);
                    return;
                }
                if (!before.Equals(FileState.Of(legacy))) Failures.Add(name + ": the legacy file changed");

                ConfigLoadStatus expected = _input.Bytes == null ? ConfigLoadStatus.Created
                    : import.Status == ImportStatus.Undecodable ? ConfigLoadStatus.Deferred
                    : ConfigLoadStatus.Migrated;
                if (loaded.Status != expected)
                {
                    Failures.Add(name + ": " + loaded.Status + ", not " + expected + ": " + string.Join(" | ", loaded.Log));
                    Unlock(legacy, readOnly);
                    return;
                }
                Outcome = expected == ConfigLoadStatus.Created ? "created" : expected == ConfigLoadStatus.Deferred ? "deferred" : "migrated";
                if (loaded.Diagnostics.Count > 0) Failures.Add(name + ": the new file draws " + loaded.Diagnostics[0].Describe());

                var names = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                var expectedNames = new List<string>();
                if (_input.Bytes != null) expectedNames.Add(SubnauticaConfigOwner.LegacyFileName);
                if (expected != ConfigLoadStatus.Deferred) expectedNames.Add(SubnauticaConfigOwner.FileName);
                expectedNames.Sort(StringComparer.Ordinal);
                if (!names.SequenceEqual(expectedNames)) Failures.Add(name + ": the folder holds " + string.Join(", ", names));

                if (expected == ConfigLoadStatus.Created)
                {
                    if (globals == Globals.Created)
                    {
                        string[] differs = RowDifferences(imported, loaded.Config);
                        if (!differs.SequenceEqual(MovedDefaults)) Failures.Add(name + ": a new file differs from v1.4.0's defaults in " + string.Join(", ", differs));
                    }
                }
                else
                {
                    SubnauticaConfig defaults = Owner(Path.Combine(root, "fresh"), DefaultsFile.At(defaultsPath)).Load().Config;
                    ICollection<ConceptDescriptor> follows = expected == ConfigLoadStatus.Deferred
                        ? new ConceptDescriptor[0]
                        : (ICollection<ConceptDescriptor>)import.FollowsDefaultsIni;
                    string[] want = Expected(imported, defaults, follows);
                    string[] got = Rows(loaded.Config);
                    for (int i = 0; i < want.Length; i++)
                    {
                        if (want[i] != got[i]) Failures.Add(name + ": the session holds " + got[i].Replace("\n", " ") + ", not " + want[i].Replace("\n", " "));
                    }
                }

                if (expected == ConfigLoadStatus.Migrated)
                {
                    Dictionary<string, string> written = FileRows(config);
                    foreach (ConceptDescriptor c in import.FollowsDefaultsIni)
                    {
                        string row = "[" + c.Section + "] " + c.Key;
                        if (written[row] != "default") Failures.Add(name + ": " + row + "=" + written[row] + " does not follow Defaults.ini");
                    }
                    _files.TryAdd(Sha256(File.ReadAllBytes(config)), File.ReadAllBytes(config));
                }

                FileState configBefore = FileState.Of(config);
                ConfigLoadResult<SubnauticaConfig> again = Owner(folder, DefaultsFile.At(defaultsPath)).Load();
                ConfigLoadStatus expectedAgain = expected == ConfigLoadStatus.Deferred ? ConfigLoadStatus.Deferred : ConfigLoadStatus.Canonical;
                if (again.Status != expectedAgain) Failures.Add(name + ": the next start is " + again.Status + ", not " + expectedAgain);
                if (RowDifferences(loaded.Config, again.Config).Length > 0) Failures.Add(name + ": the next start runs on other settings");
                if (!configBefore.Equals(FileState.Of(config))) Failures.Add(name + ": the next start changed " + SubnauticaConfigOwner.FileName);
                if (!before.Equals(FileState.Of(legacy))) Failures.Add(name + ": the next start changed the legacy file");
                Unlock(legacy, readOnly);
            }

            private void CheckStartup(string name, SubnauticaConfig config)
            {
                StartupState startup = StartupState.From(config);
                if (!startup.Enabled || startup.Mode != TrackingMode.RotationAndPosition || startup.WorldSpaceYaw)
                {
                    Failures.Add(name + ": starts " + startup.Enabled + ", " + startup.Mode + ", world yaw " + startup.WorldSpaceYaw
                                 + "; v1.4.0 started on, rotation and position, camera-local yaw");
                }
            }

            // Where the import could not name a key, the session keeps that action's chord alone. A
            // port key left at v1.4.0's default takes this version's.
            private void CheckHotkeys(string name, SubnauticaConfig config, ImportResult import)
            {
                bool undecodable = import.Status == ImportStatus.Undecodable;
                Compare(name, "ToggleKey", config.ToggleKeyName, _published.Values.ToggleHotkey, KeyCode.Y, undecodable);
                Compare(name, "CycleTrackingModeKey", config.CycleTrackingModeKeyName, _published.Values.CycleTrackingModeHotkey, KeyCode.G, undecodable);
                Compare(name, "YawModeKey", config.YawModeKeyName, _published.Values.ToggleYawModeHotkey, KeyCode.U, undecodable);
                if (_published.Values.CyclePortHotkey == Shipped.Value.CyclePortHotkey)
                {
                    if (config.CyclePortKeyName != "Ctrl+Shift+J") Failures.Add(name + ": CyclePortKey=" + config.CyclePortKeyName + ", not this version's Ctrl+Shift+J");
                }
                else
                {
                    Compare(name, "CyclePortKey", config.CyclePortKeyName, _published.Values.CyclePortHotkey, KeyCode.H, undecodable);
                }
            }

            private void Compare(string name, string key, string list, KeyCode legacy, KeyCode chord, bool undecodable)
            {
                if (!KeyBindings.TryParse(list, out KeyBinding[] bindings, out string error))
                {
                    Failures.Add(name + ": " + key + "=" + list + " does not parse: " + error);
                    return;
                }
                KeyBinding[] expected = Readers.PublishedBindings(IsModifierKey(legacy) ? KeyCode.None : legacy, chord);
                if (bindings.SequenceEqual(expected)) return;
                if (undecodable && bindings.SequenceEqual(Readers.PublishedBindings(KeyCode.None, chord))) return;
                Failures.Add(name + ": " + key + "=" + list + ", and v1.4.0 bound " + legacy + " and Ctrl+Shift+" + chord);
            }

            private static void Unlock(string legacy, bool readOnly)
            {
                if (readOnly && File.Exists(legacy)) File.SetAttributes(legacy, FileAttributes.Normal);
            }
        }

        // v1.4.0 with no file: every setting at its default.
        private static readonly Lazy<LegacyConfig> Shipped = new Lazy<LegacyConfig>(
            () => Readers.PublishedAll().Single(p => p.Key.Bytes == null).Value.Values);

        // Ctrl, Shift and Alt alone, RightShift to LeftAlt, which the import unbinds (N3).
        private static bool IsModifierKey(KeyCode code)
        {
            return code >= KeyCode.RightShift && code <= KeyCode.LeftAlt;
        }

        private static string Text(float value)
        {
            return Encoding.ASCII.GetString(new FloatCodec().Render(value));
        }

        private static string Text(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }
    }
}
