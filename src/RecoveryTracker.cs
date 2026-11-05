#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Control points that need restoring after their Tesira unit was unavailable, kept apart from
    /// whether the unit answers now: a control point leaves only once it is subscribed again, so a
    /// stray successful reply (a user command, a range poll) can't drop it before its subscriptions
    /// are restored. Each entry notes whether the control point needs its full setup (it went
    /// unavailable during startup, so its one-time writes may have failed) or subscriptions only.
    /// Thread-safe.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public sealed class RecoveryTracker
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, bool> pending = new Dictionary<string, bool>();

        /// <summary>
        /// Records that a control point needs restoring. Full setup, once asked for, stays asked for
        /// until the control point is restored.
        /// </summary>
        public void MarkNeeded(string key, bool fullSetup)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync)
                pending[key] = (pending.TryGetValue(key, out var existing) && existing) || fullSetup;
        }

        public bool IsPending(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return pending.ContainsKey(key);
        }

        /// <summary>True if the control point needs its full setup when restored.</summary>
        public bool NeedsFullSetup(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return pending.TryGetValue(key, out var full) && full;
        }

        /// <summary>The control points waiting to be restored, sorted.</summary>
        public IReadOnlyList<string> Keys
        {
            get { lock (sync) return pending.Keys.OrderBy(k => k).ToList(); }
        }

        public int Count
        {
            get { lock (sync) return pending.Count; }
        }

        /// <summary>The control point is restored (or no longer expected to subscribe). True if it was pending.</summary>
        public bool Complete(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return pending.Remove(key);
        }

        public void Clear()
        {
            lock (sync) pending.Clear();
        }
    }
}
