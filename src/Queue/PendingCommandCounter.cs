#nullable disable
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
    /// <summary>
    /// Counts queued, not-yet-sent commands per key. Not thread-safe: the owning queue updates it
    /// under its own lock, in the same critical sections that enqueue, dequeue and clear.
    /// </summary>
    internal sealed class PendingCommandCounter
    {
        private readonly Dictionary<object, int> counts = new Dictionary<object, int>();

        public void Add(object key)
        {
            counts.TryGetValue(key, out var count);
            counts[key] = count + 1;
        }

        public void Remove(object key)
        {
            if (!counts.TryGetValue(key, out var count)) return;
            if (count <= 1) counts.Remove(key);
            else counts[key] = count - 1;
        }

        public bool Has(object key) => counts.ContainsKey(key);

        public void Clear() => counts.Clear();

        // TTP commands read: "Instance Tag" verb attribute [index] [value]. Match the verb after the
        // quoted tag, since a tag can itself contain words like "set".
        private static readonly Regex SetVerb = new Regex(@"^""[^""]*""\s+set\s", RegexOptions.Compiled);

        /// <summary>True when a TTP command line uses the <c>set</c> verb.</summary>
        public static bool IsSetCommand(string command) => command != null && SetVerb.IsMatch(command);
    }
}
