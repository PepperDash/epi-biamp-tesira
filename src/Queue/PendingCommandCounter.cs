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
        private static readonly Regex ChangeVerb = new Regex(@"^""[^""]*""\s+(set|increment|decrement)\s", RegexOptions.Compiled);

        /// <summary>True when a TTP command line changes a value: set, increment or decrement.</summary>
        public static bool IsChangeCommand(string command) => command != null && ChangeVerb.IsMatch(command);

        // A raw line (pass-through) that changes a level: "Tag" set level 1 -10, Tag increment levelOut 2 1.
        private static readonly Regex RawLevelChange = new Regex(
            @"^\s*(?:""(?<tag>[^""]*)""|(?<tag>\S+))\s+(set|increment|decrement)\s+(level|levelOut)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// The instance tag a raw command line changes the level of, for lines no component sent
        /// (command pass-through). False for anything else.
        /// </summary>
        public static bool TryGetRawLevelTarget(string command, out string instanceTag)
        {
            instanceTag = null;
            if (command == null) return false;
            var m = RawLevelChange.Match(command);
            if (!m.Success || m.Groups["tag"].Value.Length == 0) return false;
            instanceTag = m.Groups["tag"].Value;
            return true;
        }

        /// <summary>The counter key for raw level changes to an instance tag (case-insensitive).</summary>
        public static string RawKey(string instanceTag) => "raw:" + (instanceTag ?? string.Empty).ToUpperInvariant();
    }
}
