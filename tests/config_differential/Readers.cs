extern alias oracle;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using CameraUnlock.Core.Input;
using SubnauticaHeadTracking.Legacy;
using UnityEngine;
using Xunit;
using PublishedReader = oracle::SubnauticaHeadTracking.Config.ConfigurationManager;
using PublishedInfo = oracle::SubnauticaHeadTracking.PluginInfo;

namespace SubnauticaHeadTracking.ConfigDifferential
{
    /// <summary>What one reader made of one input.</summary>
    internal sealed class Reading
    {
        /// <summary>
        /// The exception BepInEx's ConfigFile threw on the file, as type and message, or null.
        /// BaseUnityPlugin builds that ConfigFile before the plugin runs, so the plugin does
        /// not load at all.
        /// </summary>
        public string PluginLoadFailure;

        public bool FileFound;
        public LegacyConfig Values;
    }

    /// <summary>
    /// v1.4.0 keeps its settings in static properties, so no two tests run it at once.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class OracleCollection
    {
        public const string Name = "v1.4.0 oracle";
    }

    internal static class Readers
    {
        private static readonly Lazy<List<KeyValuePair<Input, Reading>>> PublishedInputs =
            new Lazy<List<KeyValuePair<Input, Reading>>>(ReadAllPublished);

        /// <summary>
        /// Every input <see cref="Inputs.All"/> lists, with what v1.4.0 made of it, read once for
        /// the test run: it is the one reader that cannot run two inputs at once.
        /// </summary>
        public static List<KeyValuePair<Input, Reading>> PublishedAll()
        {
            return PublishedInputs.Value;
        }

        private static List<KeyValuePair<Input, Reading>> ReadAllPublished()
        {
            string scratch = Path.Combine(Path.GetTempPath(), "subnautica-config-published-" + Guid.NewGuid().ToString("N"));
            var read = new List<KeyValuePair<Input, Reading>>();
            try
            {
                foreach (Input input in Inputs.All())
                {
                    string dir = Path.Combine(scratch, read.Count.ToString());
                    read.Add(new KeyValuePair<Input, Reading>(input, Published(dir, input)));
                    Directory.Delete(dir, true);
                }
            }
            finally
            {
                if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
            }
            return read;
        }

        /// <summary>
        /// v1.4.0 on <paramref name="input"/>, written as the legacy file into
        /// <paramref name="dir"/>: BaseUnityPlugin's ConfigFile, then the published Initialize,
        /// which saves the file after each Bind as that build did.
        /// </summary>
        public static Reading Published(string dir, Input input)
        {
            string path = Place(dir, input);
            var reading = new Reading { FileFound = input.Bytes != null };
            ConfigFile config;
            try
            {
                config = new ConfigFile(path, false, new BepInPlugin(PublishedInfo.PLUGIN_GUID,
                    PublishedInfo.PLUGIN_NAME, PublishedInfo.PLUGIN_VERSION));
            }
            catch (ArgumentException e)
            {
                reading.PluginLoadFailure = e.GetType().Name + ": " + e.Message;
                return reading;
            }
            PublishedReader.Initialize(config);
            reading.Values = FromPublished();
            return reading;
        }

        /// <summary>
        /// The frozen reader on <paramref name="input"/>, on a ConfigFile built as
        /// BaseUnityPlugin builds the plugin's. Fails when the reader changed the file.
        /// </summary>
        public static Reading Frozen(string dir, Input input)
        {
            string path = Place(dir, input);
            FileState before = FileState.Of(path);
            var reading = new Reading();
            try
            {
                ConfigFile config = new ConfigFile(path, false);
                reading.Values = LegacyConfigReader.Read(config, out reading.FileFound);
            }
            catch (ArgumentException e)
            {
                reading.PluginLoadFailure = e.GetType().Name + ": " + e.Message;
            }
            FileState after = FileState.Of(path);
            if (!before.Equals(after)) throw new InvalidOperationException("the frozen reader changed " + path);
            return reading;
        }

        public static string Place(string dir, Input input)
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, Inputs.LegacyFileName);
            if (input.Bytes != null) File.WriteAllBytes(path, input.Bytes);
            return path;
        }

        // v1.4.0 keeps its settings in static ConfigEntry properties named as LegacyConfig's
        // fields are.
        private static LegacyConfig FromPublished()
        {
            var values = new LegacyConfig();
            foreach (FieldInfo field in typeof(LegacyConfig).GetFields())
            {
                PropertyInfo entry = typeof(PublishedReader).GetProperty(field.Name, BindingFlags.Public | BindingFlags.Static);
                if (entry == null) throw new InvalidOperationException("v1.4.0 has no setting " + field.Name);
                field.SetValue(values, ((ConfigEntryBase)entry.GetValue(null, null)).BoxedValue);
            }
            return values;
        }

        /// <summary>
        /// Every field that differs, floats by their bits, as "Field: a != b".
        /// </summary>
        public static List<string> Differences(LegacyConfig a, LegacyConfig b)
        {
            var differences = new List<string>();
            foreach (FieldInfo field in typeof(LegacyConfig).GetFields())
            {
                object x = field.GetValue(a);
                object y = field.GetValue(b);
                bool same = x is float
                    ? BitConverter.ToInt32(BitConverter.GetBytes((float)x), 0) == BitConverter.ToInt32(BitConverter.GetBytes((float)y), 0)
                    : Equals(x, y);
                if (!same) differences.Add(field.Name + ": " + x + " != " + y);
            }
            return differences;
        }

        /// <summary>
        /// The bindings v1.4.0's HotkeyHandler polled for one action: the configured key, which
        /// never fires as KeyCode.None, and the Ctrl+Shift chord written in code.
        /// </summary>
        public static KeyBinding[] PublishedBindings(KeyCode key, KeyCode chordLetter)
        {
            var bindings = new List<KeyBinding>();
            if (key != KeyCode.None) bindings.Add(new KeyBinding(KeyModifiers.None, (int)key));
            bindings.Add(new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter));
            return bindings.ToArray();
        }
    }

    /// <summary>A file's bytes, last write time and attributes, or its absence.</summary>
    internal sealed class FileState : IEquatable<FileState>
    {
        private byte[] _bytes;
        private DateTime _written;
        private FileAttributes _attributes;

        public static FileState Of(string path)
        {
            if (!File.Exists(path)) return new FileState();
            return new FileState
            {
                _bytes = File.ReadAllBytes(path),
                _written = File.GetLastWriteTimeUtc(path),
                _attributes = File.GetAttributes(path),
            };
        }

        public bool Equals(FileState other)
        {
            if (other == null) return false;
            if (_bytes == null || other._bytes == null) return _bytes == other._bytes;
            return _bytes.SequenceEqual(other._bytes) && _written == other._written && _attributes == other._attributes;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FileState);
        }

        public override int GetHashCode()
        {
            return _bytes == null ? 0 : _bytes.Length;
        }
    }
}
