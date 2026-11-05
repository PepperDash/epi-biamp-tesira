#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Control points that need restoring after their Tesira unit was unavailable, kept apart from
    /// whether the unit answers now. A control point is restored only when a restore was sent while
    /// its unit was answering AND a restore command (a subscribe or a read) was answered
    /// successfully since, with no error in between. Neither a stray successful user command, nor a
    /// subscribed flag left over from before the outage, nor a restore the queue rejected or
    /// discarded (no reply ever comes) can end recovery early. Each entry also notes whether the
    /// control point needs its full setup (it went unavailable during startup, so its one-time
    /// writes may have failed) or subscriptions only. Thread-safe.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public sealed class RecoveryTracker
    {
        private sealed class Entry
        {
            public bool FullSetup;
            public bool RestoreSent;
            public bool Acknowledged;
            public bool ExpectsSubscribe;
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
                entry.Acknowledged = false;
                entry.ExpectsSubscribe = false;
            }
        }

        /// <summary>Records that a restore is being sent while the control point's unit is answering.</summary>
        public void MarkRestoreSent(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync)
            {
                if (!pending.TryGetValue(key, out var entry)) return;
                entry.RestoreSent = true;
                entry.Acknowledged = false;
                entry.ExpectsSubscribe = false;
            }
        }

        /// <summary>
        /// The restore queued a subscribe for the control point: from now on only a successful
        /// subscribe reply acknowledges it (a read answered earlier doesn't count).
        /// </summary>
        public void ExpectSubscribe(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync)
            {
                if (!pending.TryGetValue(key, out var entry) || !entry.RestoreSent) return;
                entry.ExpectsSubscribe = true;
                entry.Acknowledged = false;
            }
        }

        /// <summary>
        /// A restore command for the control point was answered successfully. A read counts only for
        /// a control point whose restore queued no subscribe (a polled one).
        /// </summary>
        public void Acknowledge(string key, bool isSubscribe)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync)
            {
                if (!pending.TryGetValue(key, out var entry) || !entry.RestoreSent) return;
                if (entry.ExpectsSubscribe && !isSubscribe) return;
                entry.Acknowledged = true;
            }
        }

        /// <summary>A restore command for the control point failed: the restore doesn't count, retry it.</summary>
        public void Fail(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync)
            {
                if (!pending.TryGetValue(key, out var entry)) return;
                entry.RestoreSent = false;
                entry.Acknowledged = false;
                entry.ExpectsSubscribe = false;
            }
        }

        /// <summary>True once a restore was sent while answering and acknowledged, with no failure since.</summary>
        public bool IsRestored(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return pending.TryGetValue(key, out var entry) && entry.RestoreSent && entry.Acknowledged;
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
