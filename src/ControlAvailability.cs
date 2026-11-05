#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Which control points the DSP has reported unavailable (<c>-CANNOT_DELIVER</c>: their Tesira
    /// unit is offline, rebooting or not running the design). Each change is reported once, so a
    /// block that keeps failing logs once rather than per command. Thread-safe.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public sealed class ControlAvailability
    {
        private readonly object sync = new object();
        private readonly HashSet<string> unavailable = new HashSet<string>();

        /// <summary>Records an unavailable reply. True if the control point has just become unavailable.</summary>
        public bool MarkUnavailable(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return unavailable.Add(key);
        }

        /// <summary>Records a successful reply. True if the control point was unavailable until now.</summary>
        public bool MarkAvailable(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return unavailable.Remove(key);
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (sync) return unavailable.Contains(key);
        }

        public int Count
        {
            get { lock (sync) return unavailable.Count; }
        }

        /// <summary>The unavailable control points' keys, sorted.</summary>
        public IReadOnlyList<string> Keys
        {
            get { lock (sync) return unavailable.OrderBy(k => k).ToList(); }
        }

        public void Clear()
        {
            lock (sync) unavailable.Clear();
        }
    }
}
