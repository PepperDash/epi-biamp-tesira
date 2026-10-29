#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>What a reply to a watchdog subscribe probe says about that subscription.</summary>
    public enum ProbeReply
    {
        /// <summary>-ERR ALREADY_SUBSCRIBED: the subscription is still there.</summary>
        Healthy,

        /// <summary>+OK: the DSP had dropped it (design push, partition restart) and has just made it again.</summary>
        Lost,

        /// <summary>The probe can never succeed: missing tag, bad attribute or index, attribute that can't be subscribed.</summary>
        ConfigError,

        /// <summary>Any other error.</summary>
        OtherError
    }

    /// <summary>
    /// Classifies replies to subscribe commands. Error texts are as a TesiraFORTÉ (firmware 5.7) sends them.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal static class ProbeReplies
    {
        private static readonly string[] ConfigErrorTexts =
        {
            "address not found",            // -ERR address not found: [0 0 0]
            "is not supported by",          // -ERR 'bogusAttr' is not supported by LevelControlInterface::Attributes
            "not supported on attribute",   // -ERR operation subscribe not supported on attribute: minLevel
            "Index out of range",           // -ERR INVALID_PARAMETER Index out of range:channelIndex min:1 max:1 received:99
        };

        public static ProbeReply Classify(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return ProbeReply.OtherError;

            var text = response.Trim();
            if (text.IndexOf("ALREADY_SUBSCRIBED", StringComparison.Ordinal) >= 0) return ProbeReply.Healthy;
            if (text.StartsWith("+OK", StringComparison.Ordinal)) return ProbeReply.Lost;
            if (ConfigErrorTexts.Any(e => text.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0)) return ProbeReply.ConfigError;
            return ProbeReply.OtherError;
        }

        /// <summary>True for a TTP subscribe command: <c>"Tag" subscribe attr ...</c>.</summary>
        public static bool IsSubscribeCommand(string command) =>
            command != null && command.IndexOf(" subscribe ", StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Chooses what each watchdog tick probes: the sentinels (one per partition, from config; by default
    /// the first candidate) plus the next few candidates of a rotating patrol. Quarantined components are
    /// never probed.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal sealed class WatchdogPatrol<T> where T : class
    {
        private readonly int patrolSize;
        private readonly HashSet<T> quarantined = new HashSet<T>();
        private int cursor;

        public WatchdogPatrol(int patrolSize)
        {
            this.patrolSize = Math.Max(0, patrolSize);
        }

        public int QuarantinedCount => quarantined.Count;

        public bool IsQuarantined(T component) => component != null && quarantined.Contains(component);

        /// <returns>true if the component was newly quarantined</returns>
        public bool Quarantine(T component) => component != null && quarantined.Add(component);

        /// <summary>Forgets quarantines, e.g. after a reconnect (the design may have been fixed).</summary>
        public void ClearQuarantine() => quarantined.Clear();

        /// <summary>
        /// The components to probe this tick, sentinels first, without duplicates.
        /// </summary>
        /// <param name="candidates">Components that can be probed (subscribed), in a stable order</param>
        /// <param name="sentinels">Configured sentinels; when none are usable, the first candidate is the sentinel</param>
        public IReadOnlyList<T> NextProbeSet(IReadOnlyList<T> candidates, IEnumerable<T> sentinels)
        {
            var probe = new List<T>();
            var usable = (candidates ?? new List<T>()).Where(c => c != null && !quarantined.Contains(c)).ToList();
            if (usable.Count == 0) return probe;

            foreach (var sentinel in sentinels ?? Enumerable.Empty<T>())
            {
                if (usable.Contains(sentinel) && !probe.Contains(sentinel)) probe.Add(sentinel);
            }
            if (probe.Count == 0) probe.Add(usable[0]);

            var patrol = usable.Where(c => !probe.Contains(c)).ToList();
            if (patrol.Count == 0) return probe;

            var take = Math.Min(patrolSize, patrol.Count);
            cursor %= patrol.Count;
            for (var i = 0; i < take; i++)
            {
                probe.Add(patrol[(cursor + i) % patrol.Count]);
            }
            cursor = (cursor + take) % patrol.Count;

            return probe;
        }
    }

    /// <summary>
    /// One tick's probes in flight. Each subscribe command a probed component sends is expected to
    /// get exactly one reply, attributed to that component (a component can send several, e.g. a fader's
    /// level and mute). The batch closes when every reply is in, or when a reply says subscriptions
    /// were lost.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested. Not thread safe; lock around it.</remarks>
    internal sealed class ProbeBatch<T> where T : class
    {
        private readonly HashSet<T> members = new HashSet<T>();
        private readonly Dictionary<T, int> outstanding = new Dictionary<T, int>();

        /// <summary>A batch has been started and not yet closed.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>While true, subscribe commands from members count as expected replies.</summary>
        public bool Capturing { get; private set; }

        public int Outstanding => outstanding.Values.Sum();

        public bool Contains(T component) => IsOpen && component != null && members.Contains(component);

        public void Begin(IEnumerable<T> components)
        {
            members.Clear();
            outstanding.Clear();
            foreach (var c in components ?? Enumerable.Empty<T>())
            {
                if (c != null) members.Add(c);
            }
            IsOpen = members.Count > 0;
            Capturing = IsOpen;
        }

        /// <summary>Counts a subscribe command a member queued while the batch was capturing.</summary>
        public void ExpectReply(T component)
        {
            if (!Capturing || component == null || !members.Contains(component)) return;
            outstanding[component] = outstanding.TryGetValue(component, out var n) ? n + 1 : 1;
        }

        /// <summary>
        /// Ends capture. If no member queued a probe (all opted out), the batch closes.
        /// </summary>
        /// <returns>true if the batch is still waiting for replies</returns>
        public bool EndCapture()
        {
            Capturing = false;
            if (Outstanding == 0) Close();
            return IsOpen;
        }

        /// <summary>
        /// Records a reply to one of a member's probe commands. A <see cref="ProbeReply.Lost"/>
        /// reply closes the batch; otherwise it closes once every expected reply is in.
        /// </summary>
        /// <returns>the reply's classification, or null when it isn't one this batch is waiting for</returns>
        public ProbeReply? Record(T component, string response)
        {
            if (!IsOpen || component == null || !outstanding.TryGetValue(component, out var n) || n == 0) return null;

            outstanding[component] = n - 1;
            var reply = ProbeReplies.Classify(response);

            if (reply == ProbeReply.Lost || (!Capturing && Outstanding == 0)) Close();
            return reply;
        }

        public void Close()
        {
            IsOpen = false;
            Capturing = false;
            members.Clear();
            outstanding.Clear();
        }
    }
}
