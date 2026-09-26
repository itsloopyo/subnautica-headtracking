using System;
using System.Collections.Generic;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Config.Testing;
using SubnauticaHeadTracking.Legacy;

namespace SubnauticaHeadTracking.ConfigDifferential
{
    /// <summary>One legacy file the readers are run on. Null bytes: no file.</summary>
    internal sealed class Input
    {
        public Input(string name, byte[] bytes)
        {
            Name = name;
            Bytes = bytes;
        }

        public string Name { get; }
        public byte[] Bytes { get; }
    }

    internal static class Inputs
    {
        public const string LegacyFileName = "com.cameraunlock.subnautica.headtracking.cfg";

        // Each published build that read the .cfg differently from the one before, and the
        // tags that shipped it. data/<folder>/first-run.cfg is what that build wrote at its
        // first start with no .cfg (provenance.tsv).
        public static readonly string[] Builds =
        {
            "v1.0.0", "v1.1.0-v1.1.3", "v1.2.0-v1.3.0", "v1.3.1", "v1.4.0",
        };

        public static string RepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "pixi.toml"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("no pixi.toml above " + AppDomain.CurrentDomain.BaseDirectory);
            return dir.FullName;
        }

        public static string TestDir()
        {
            return Path.Combine(RepoRoot(), "tests", "config_differential");
        }

        public static byte[] FirstRun(string build)
        {
            return File.ReadAllBytes(Path.Combine(Path.Combine(Path.Combine(TestDir(), "data"), build), "first-run.cfg"));
        }

        /// <summary>
        /// Every input: each build's first-run file, no file, an empty file, the hand-written
        /// hotkey cases, and the corpus core's generator makes from v1.4.0's first-run file.
        /// </summary>
        public static List<Input> All()
        {
            var inputs = new List<Input>();
            foreach (string build in Builds) inputs.Add(new Input(build + " first run", FirstRun(build)));
            inputs.Add(new Input("no file", null));
            inputs.Add(new Input("empty file", new byte[0]));

            byte[] shipped = FirstRun("v1.4.0");
            foreach (KeyValuePair<string, string> hotkey in HotkeyCases)
            {
                inputs.Add(new Input("[Hotkeys] CyclePort=" + hotkey.Key + ": " + hotkey.Value,
                    SetValue(shipped, "CyclePort = PageDown", "CyclePort = " + hotkey.Key)));
            }

            IList<LegacyKey> reads = LegacyConfigReader.Keys;
            foreach (IniMutation mutation in IniMutations.Generate(shipped, reads, Descriptors))
            {
                inputs.Add(new Input(mutation.Name, mutation.Bytes));
            }

            var names = new HashSet<string>();
            foreach (Input input in inputs)
            {
                if (!names.Add(input.Name)) throw new InvalidOperationException("two inputs are named " + input.Name);
            }
            return inputs;
        }

        // Values BepInEx reads into a KeyCode that the corpus does not reach: Enum.Parse takes
        // any letter case, a number, and a list whose values it ORs together.
        private static readonly KeyValuePair<string, string>[] HotkeyCases =
        {
            new KeyValuePair<string, string>("None", "unbound"),
            new KeyValuePair<string, string>("pagedown", "lower case"),
            new KeyValuePair<string, string>("281", "PageDown by number"),
            new KeyValuePair<string, string>("330", "JoystickButton0 by number"),
            new KeyValuePair<string, string>("Home, End", "two names, ORed"),
            new KeyValuePair<string, string>("LeftControl", "a modifier key"),
            new KeyValuePair<string, string>("99999", "a number no key has"),
        };

        private static byte[] SetValue(byte[] file, string line, string replacement)
        {
            string text = System.Text.Encoding.UTF8.GetString(file);
            if (!text.Contains(line + "\r\n")) throw new InvalidOperationException("the file has no line " + line);
            return System.Text.Encoding.UTF8.GetBytes(text.Replace(line + "\r\n", replacement + "\r\n"));
        }

        private static MutationKey Key(string section, string key, string alternate, params string[] outOfRange)
        {
            return new MutationKey(section, key, alternate, outOfRange, false, new ChordSwitch[0]);
        }

        private static MutationKey Hotkey(string key, string alternate)
        {
            return new MutationKey("Hotkeys", key, alternate, new string[0], true, new ChordSwitch[0]);
        }

        /// <summary>
        /// One descriptor per key <see cref="LegacyConfigReader.Keys"/> lists, in its order. The
        /// out-of-range values sit either side of each AcceptableValueRange, which BepInEx clamps
        /// to. A KeyCode, a bool and a string have no range. The Ctrl+Shift chords were written
        /// in code, not in the file, so no key switches one.
        /// </summary>
        public static readonly MutationKey[] Descriptors =
        {
            Key("Network", "UdpPort", "4243", "1023", "65536"),
            Key("Network", "BindAddress", "127.0.0.1"),
            Key("Sensitivity", "Yaw", "1.5", "0.05", "3.5"),
            Key("Sensitivity", "Pitch", "1.5", "0.05", "3.5"),
            Key("Sensitivity", "Roll", "1.5", "0.05", "3.5"),
            Key("Deadzone", "Yaw", "0.5", "-0.5", "10.5"),
            Key("Deadzone", "Pitch", "0.5", "-0.5", "10.5"),
            Key("Deadzone", "Roll", "0.5", "-0.5", "10.5"),
            Key("Inversion", "YawInvert", "true"),
            Key("Inversion", "PitchInvert", "false"),
            Key("Inversion", "RollInvert", "true"),
            Hotkey("Toggle", "F9"),
            Hotkey("CycleTrackingMode", "F10"),
            Hotkey("ToggleYawMode", "F11"),
            Hotkey("CyclePort", "F12"),
            Key("Advanced", "LocalSmoothing", "0.3", "-0.1", "1.5"),
            Key("Advanced", "RemoteSmoothing", "0.5", "-0.1", "1.5"),
            Key("Position", "PositionEnabled", "false"),
            Key("Position", "PositionSensitivityX", "1", "-1", "3.5"),
            Key("Position", "PositionSensitivityY", "1", "-1", "3.5"),
            Key("Position", "PositionSensitivityZ", "1", "-1", "3.5"),
            Key("Position", "PositionLimitX", "0.2", "0.005", "0.6"),
            Key("Position", "PositionLimitY", "0.3", "-0.1", "0.6"),
            Key("Position", "PositionLimitYDown", "0.1", "-0.1", "0.6"),
            Key("Position", "PositionLimitZ", "0.2", "0.005", "0.6"),
            Key("Position", "PositionLimitZBack", "0.05", "0.005", "0.6"),
        };
    }
}
