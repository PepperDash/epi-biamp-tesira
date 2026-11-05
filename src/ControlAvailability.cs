#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Which control points the DSP has reported unavailable (<c>-CANNOT_DELIVER</c>: their Tesira
    /// unit is offline, rebooting or not running the design), tracked per target block (instance
    /// tag): one control point can use blocks on different units, such as a fader's separate level
    /// and mute blocks. A control point stays unavailable until every unavailable target of it
    /// answers again. Each change is reported once, so a block that keeps failing logs once rather
    /// than per command. Thread-safe.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public sealed class ControlAvailability
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, HashSet<string>> unavailable = new Dictionary<string, HashSet<string>>();

        /// <summary>
        /// Records an unavailable reply for one target of a control point. True if that target has
        /// just become unavailable.
        /// </summary>
        public bool MarkUnavailable(string key, string target)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync)
            {
                if (!unavailable.TryGetValue(key, out var targets))
                    unavailable[key] = targets = new HashSet<string>();
                return targets.Add(target ?? string.Empty);
            }
        }

        /// <summary>
        /// Records a successful reply for one target of a control point.
        /// </summary>
        /// <param name="targetRecovered">true if that target was unavailable until now</param>
        /// <returns>true if the control point has no unavailable targets left, having had some</returns>
        public bool MarkAvailable(string key, string target, out bool targetRecovered)
        {
            targetRecovered = false;
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync)
            {
                if (!unavailable.TryGetValue(key, out var targets)) return false;
                targetRecovered = targets.Remove(target ?? string.Empty);
                if (targets.Count > 0) return false;
                unavailable.Remove(key);
                return true;
            }
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return unavailable.ContainsKey(key);
        }

        /// <summary>Unavailable control points.</summary>
        public int Count
        {
            get { lock (sync) return unavailable.Count; }
        }

        /// <summary>The unavailable control points' keys, sorted.</summary>
        public IReadOnlyList<string> Keys
        {
            get { lock (sync) return unavailable.Keys.OrderBy(k => k).ToList(); }
        }

        /// <summary>The unavailable targets of one control point, sorted.</summary>
        public IReadOnlyList<string> TargetsOf(string key)
        {
            lock (sync)
                return key != null && unavailable.TryGetValue(key, out var targets)
                    ? targets.OrderBy(t => t).ToList()
                    : new List<string>();
        }

        public void Clear()
        {
            lock (sync) unavailable.Clear();
        }
    }
}
