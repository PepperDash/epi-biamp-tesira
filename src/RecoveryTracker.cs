#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Control points that need restoring after their Tesira unit was unavailable, kept apart from
    /// whether the unit answers now. A control point leaves only after a restore was sent while its
    /// unit was answering and no unavailable reply came back since. A stray successful reply (a user
    /// command, a range poll) can't drop it early, and neither can a subscribed flag left over from
    /// before the outage (a unit that restarts drops its subscriptions). Each entry also notes
    /// whether the control point needs its full setup (it went unavailable during startup, so its
    /// one-time writes may have failed) or subscriptions only. Thread-safe.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public sealed class RecoveryTracker
    {
        private sealed class Entry
        {
            public bool FullSetup;
            public bool RestoreSent;
        }

        private readonly object sync = new object();
        private readonly Dictionary<string, Entry> pending = new Dictionary<string, Entry>();

        /// <summary>
        /// Records an unavailable reply for a control point: it needs restoring, and any restore sent
        /// before this reply no longer counts. Full setup, once asked for, stays asked for until the
        /// control point is restored.
        /// </summary>
        public void MarkNeeded(string key, bool fullSetup)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync)
            {
                if (!pending.TryGetValue(key, out var entry)) pending[key] = entry = new Entry();
                entry.FullSetup |= fullSetup;
                entry.RestoreSent = false;
            }
        }

        /// <summary>Records that a restore was sent while the control point's unit was answering.</summary>
        public void MarkRestoreSent(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync)
                if (pending.TryGetValue(key, out var entry)) entry.RestoreSent = true;
        }

        /// <summary>True once a restore was sent while answering, with no unavailable reply since.</summary>
        public bool IsRestored(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return pending.TryGetValue(key, out var entry) && entry.RestoreSent;
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
            lock (sync) return pending.TryGetValue(key, out var entry) && entry.FullSetup;
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
