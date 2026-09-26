using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Xunit;

namespace SubnauticaHeadTracking.ConfigDifferential
{
    /// <summary>
    /// The published build's reader (the oracle, v1.4.0) against the frozen reader in
    /// src/SubnauticaHeadTracking/Legacy, on every input <see cref="Inputs.All"/> lists.
    /// </summary>
    [Collection(OracleCollection.Name)]
    public class DifferentialTests : IDisposable
    {
        private readonly string _scratch = Path.Combine(Path.GetTempPath(), "subnautica-config-differential-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_scratch)) Directory.Delete(_scratch, true);
        }

        /// <summary>
        /// Comparison 1: what players see change that the conversion did not cause. The frozen
        /// reader's source is v1.4.0's Bind calls unchanged, and nothing else between the two
        /// reads the file, so every input reads the same and the list of differences is empty.
        /// </summary>
        [Fact]
        public void TheFrozenReaderReadsEveryInputAsThePublishedBuildDid()
        {
            List<KeyValuePair<Input, Reading>> published = Readers.PublishedAll();
            var failures = new ConcurrentBag<string>();
            Parallel.For(0, published.Count, i =>
            {
                string dir = Path.Combine(_scratch, i.ToString());
                Input input = published[i].Key;
                Reading frozen = Readers.Frozen(dir, input);
                foreach (string difference in Compare(published[i].Value, frozen)) failures.Add(input.Name + ": " + difference);
                Directory.Delete(dir, true);
            });
            Assert.True(failures.IsEmpty, failures.Count + " differences:\n" + string.Join("\n", failures.OrderBy(f => f).Take(40)));
        }

        /// <summary>
        /// Every build's first-run file after v1.4.0 has started on it once: what a player who
        /// updated to v1.4.0 holds, with the older build's keys left in it.
        /// </summary>
        [Fact]
        public void TheFrozenReaderReadsEachFileV140RewroteAsV140Did()
        {
            var failures = new List<string>();
            foreach (string build in Inputs.Builds)
            {
                string dir = Path.Combine(_scratch, build);
                Reading published = Readers.Published(Path.Combine(dir, "published"), new Input(build, Inputs.FirstRun(build)));
                byte[] rewritten = File.ReadAllBytes(Path.Combine(Path.Combine(dir, "published"), Inputs.LegacyFileName));
                var input = new Input(build + " after v1.4.0", rewritten);
                Reading again = Readers.Published(Path.Combine(dir, "published again"), input);
                Reading frozen = Readers.Frozen(Path.Combine(dir, "frozen"), input);
                foreach (string difference in Compare(published, again)) failures.Add(input.Name + ", v1.4.0 twice: " + difference);
                foreach (string difference in Compare(again, frozen)) failures.Add(input.Name + ": " + difference);
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        /// <summary>
        /// What the test compiles is pinned by hash, line endings aside, in provenance.tsv: the
        /// oracle is v1.4.0's source, the frozen reader is never edited, and the corpus and the
        /// oracle run on the BepInEx the installer ships.
        /// </summary>
        [Fact]
        public void EveryPinnedFileHoldsItsPinnedBytes()
        {
            string root = Inputs.RepoRoot();
            var failures = new List<string>();
            int rows = 0;
            foreach (string line in File.ReadAllLines(Path.Combine(Inputs.TestDir(), "provenance.tsv")))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split('\t');
                if (fields.Length != 4) throw new InvalidOperationException("provenance.tsv: '" + line + "' does not have 4 fields");
                rows++;
                string path = Path.Combine(root, fields[1].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    failures.Add(fields[1] + " is missing");
                    continue;
                }
                string actual = Sha256(path, fields[0] != "bepinex");
                if (actual != fields[2]) failures.Add(fields[1] + " hashes " + actual + ", pinned " + fields[2] + " (" + fields[3] + ")");
            }
            Assert.True(rows > 0, "provenance.tsv pins nothing");
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        private static IEnumerable<string> Compare(Reading a, Reading b)
        {
            if (a.PluginLoadFailure != b.PluginLoadFailure)
            {
                yield return "plugin load failure '" + a.PluginLoadFailure + "' != '" + b.PluginLoadFailure + "'";
                yield break;
            }
            if (a.PluginLoadFailure != null) yield break;
            if (a.FileFound != b.FileFound) yield return "file found " + a.FileFound + " != " + b.FileFound;
            foreach (string difference in Readers.Differences(a.Values, b.Values)) yield return difference;
        }

        private static string Sha256(string path, bool text)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (text)
            {
                var lf = new List<byte>(bytes.Length);
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n') continue;
                    lf.Add(bytes[i]);
                }
                bytes = lf.ToArray();
            }
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }
    }
}
