using System;
using System.Threading;

namespace SubnauticaHeadTracking.Config
{
    /// <summary>
    /// A named Windows mutex held around every load and save of the config file. Two
    /// copies of the game started from one folder share one BepInEx\config (the port
    /// hotkey exists for that setup), and the config owner's own lock covers one process
    /// only. The mutex is shared between processes, so a toggle saved in one never
    /// interleaves with a load or save in another.
    /// </summary>
    internal sealed class ConfigFileLock
    {
        private readonly Mutex _mutex;
        private readonly int _timeoutMs;

        public ConfigFileLock(string name, int timeoutMs)
        {
            _mutex = new Mutex(false, name);
            _timeoutMs = timeoutMs;
        }

        /// <summary>
        /// Runs <paramref name="action"/> while holding the mutex. False, with the action
        /// not run, when another holder keeps it past the timeout.
        /// </summary>
        public bool TryRun(Action action)
        {
            bool acquired;
            try
            {
                acquired = _mutex.WaitOne(_timeoutMs);
            }
            catch (AbandonedMutexException)
            {
                // A process ended while holding it, which leaves the mutex owned by
                // this thread. The owner writes the whole file or none of it, so what
                // that process left on disk is still a file the owner can read.
                acquired = true;
            }

            if (!acquired) return false;
            try
            {
                action();
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
            return true;
        }
    }
}
