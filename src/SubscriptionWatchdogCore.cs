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
        OtherError,

        /// <summary>-CANNOT_DELIVER: the block's Tesira unit is offline, rebooting or not running the design.</summary>
        Unavailable
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
            if (TtpReply.IsUnavailable(text)) return ProbeReply.Unavailable;
            if (ConfigErrorTexts.Any(e => text.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0)) return ProbeReply.ConfigError;
            return ProbeReply.OtherError;
        }

        /// <summary>True for a TTP subscribe command: <c>"Tag" subscribe attr ...</c>.</summary>
        public static bool IsSubscribeCommand(string command) =>
            command != null && command.IndexOf(" subscribe ", StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Chooses what each watchdog tick probes: the sentinels (one per partition, from config; by default
    /// the first candidate) plus the next few candidates of a rotating patrol. Quarantined components
    /// leave the patrol, but one of them (rotating) is probed each tick: a whole design going missing
    /// quarantines working components too, and without a probe among them the watchdog couldn't see
    /// the design come back once everything is quarantined.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal sealed class WatchdogPatrol<T> where T : class
    {
        private readonly int patrolSize;
        private readonly HashSet<T> quarantined = new HashSet<T>();
        private int cursor;
        private int quarantineCursor;

        public WatchdogPatrol(int patrolSize)
        {
            this.patrolSize = Math.Max(0, patrolSize);
        }

        public int QuarantinedCount => quarantined.Count;

        public bool IsQuarantined(T component) => component != null && quarantined.Contains(component);

        /// <returns>true if the component was newly quarantined</returns>
        public bool Quarantine(T component) => component != null && quarantined.Add(component);

        /// <summary>Releases one component, e.g. when its probe shows it is subscribed after all.</summary>
        /// <returns>true if the component was quarantined</returns>
        public bool Release(T component) => component != null && quarantined.Remove(component);

        /// <summary>Snapshot of the quarantined components, in no particular order.</summary>
        public IReadOnlyList<T> Quarantined => quarantined.ToList();

        /// <summary>
        /// Forgets quarantines: after a reconnect, or when the DSP turns out to have dropped its
        /// subscriptions (the design may have changed). Components still missing from the design are
        /// quarantined again by later probes.
        /// </summary>
        public void ClearQuarantine() => quarantined.Clear();

        /// <summary>
        /// The components to probe this tick: the sentinels and patrol, then one quarantined candidate
        /// (rotating), without duplicates.
        /// </summary>
        /// <param name="candidates">Components that can be probed (subscribed), in a stable order</param>
        /// <param name="sentinels">Configured sentinels; when none are usable, the first candidate is the sentinel</param>
        public IReadOnlyList<T> NextProbeSet(IReadOnlyList<T> candidates, IEnumerable<T> sentinels)
        {
            var candidateList = (candidates ?? new List<T>()).Where(c => c != null).ToList();
            var probe = SentinelsAndPatrol(candidateList.Where(c => !quarantined.Contains(c)).ToList(), sentinels);

            var quarantinedCandidates = candidateList.Where(quarantined.Contains).ToList();
            if (quarantinedCandidates.Count > 0)
            {
                quarantineCursor %= quarantinedCandidates.Count;
                probe.Add(quarantinedCandidates[quarantineCursor]);
                quarantineCursor = (quarantineCursor + 1) % quarantinedCandidates.Count;
            }

            return probe;
        }

        /// <summary>Sentinels first, then the next patrol components in rotation.</summary>
        private List<T> SentinelsAndPatrol(List<T> usable, IEnumerable<T> sentinels)
        {
            var probe = new List<T>();
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
    /// get exactly one reply, matched to that command (a component can send several, e.g. a fader's
    /// level and mute). The batch closes when every reply is in, or when a reply says subscriptions
    /// were lost.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested. Not thread safe; lock around it.</remarks>
    internal sealed class ProbeBatch<TComponent, TCommand>
        where TComponent : class
        where TCommand : class
    {
        private readonly HashSet<TComponent> members = new HashSet<TComponent>();

        // Members that queued at least one probe command while capturing.
        private readonly HashSet<TComponent> membersWithReplies = new HashSet<TComponent>();

        // The exact probe commands queued while capturing. Only their replies count, so a subscribe
        // a member had queued before the probe can't take a probe's place.
        private readonly HashSet<TCommand> outstanding = new HashSet<TCommand>();

        // Which member sent each probe command, and members with any reply that wasn't healthy: a
        // component's probe can be several subscribes (a fader's level and mute), judged together.
        private readonly Dictionary<TCommand, TComponent> senders = new Dictionary<TCommand, TComponent>();
        private readonly HashSet<TComponent> notHealthy = new HashSet<TComponent>();
        private readonly HashSet<TComponent> replied = new HashSet<TComponent>();

        /// <summary>A batch has been started and not yet closed.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>While true, subscribe commands from members count as expected replies.</summary>
        public bool Capturing { get; private set; }

        public int Outstanding => outstanding.Count;

        public bool Contains(TComponent component) => IsOpen && component != null && members.Contains(component);

        public void Begin(IEnumerable<TComponent> components)
        {
            Reset();
            foreach (var c in components ?? Enumerable.Empty<TComponent>())
            {
                if (c != null) members.Add(c);
            }
            IsOpen = members.Count > 0;
            Capturing = IsOpen;
        }

        /// <summary>Expects a reply to a subscribe command a member queued while the batch was capturing.</summary>
        public void ExpectReply(TComponent component, TCommand command)
        {
            if (!Capturing || component == null || command == null || !members.Contains(component)) return;
            outstanding.Add(command);
            senders[command] = component;
            membersWithReplies.Add(component);
        }

        /// <summary>
        /// Records that a member tried to queue a probe command that the queue rejected (full). It is not
        /// expected to reply, but it isn't a component that sends nothing either.
        /// </summary>
        public void MarkAttempted(TComponent component)
        {
            if (!Capturing || component == null || !members.Contains(component)) return;
            membersWithReplies.Add(component);
            // A subscribe that never went out wasn't checked, so this probe can't show the component healthy.
            notHealthy.Add(component);
        }

        /// <summary>
        /// Members that tried to queue no probe command at all. Call before <see cref="EndCapture"/>,
        /// which may close the batch.
        /// </summary>
        public IReadOnlyList<TComponent> MembersWithoutExpectedReplies() =>
            members.Where(m => !membersWithReplies.Contains(m)).ToList();

        /// <summary>
        /// Ends capture. If no member queued a probe (all opted out), the batch closes.
        /// </summary>
        /// <returns>true if the batch is still waiting for replies</returns>
        public bool EndCapture() => EndCapture(out _);

        /// <param name="healthySenders">
        /// Members whose probe replies all arrived during capture and were all healthy. A reply during
        /// capture can't settle its sender, which may still be queueing more subscribes.
        /// </param>
        public bool EndCapture(out IReadOnlyList<TComponent> healthySenders)
        {
            Capturing = false;
            healthySenders = replied.Where(IsSettledHealthy).ToList();
            if (Outstanding == 0) Close();
            return IsOpen;
        }

        private bool IsSettledHealthy(TComponent sender) => !notHealthy.Contains(sender) && !senders.ContainsValue(sender);

        /// <summary>
        /// Records the reply to one of this batch's probe commands. A <see cref="ProbeReply.Lost"/>
        /// reply closes the batch; otherwise it closes once every expected reply is in.
        /// </summary>
        /// <returns>the reply's classification, or null when the command isn't one this batch is waiting for</returns>
        public ProbeReply? Record(TCommand command, string response) => Record(command, response, out _);

        /// <param name="senderHealthy">
        /// true when capture has ended, this was the sender's last outstanding probe reply and all of
        /// its replies were healthy (ALREADY_SUBSCRIBED). Senders settled during capture are reported
        /// by <see cref="EndCapture(out IReadOnlyList{TComponent})"/> instead.
        /// </param>
        public ProbeReply? Record(TCommand command, string response, out bool senderHealthy)
        {
            senderHealthy = false;
            if (!IsOpen || command == null || !outstanding.Remove(command)) return null;

            var reply = ProbeReplies.Classify(response);

            if (senders.TryGetValue(command, out var sender))
            {
                senders.Remove(command);
                replied.Add(sender);
                if (reply != ProbeReply.Healthy) notHealthy.Add(sender);
                senderHealthy = !Capturing && IsSettledHealthy(sender);
            }

            if (reply == ProbeReply.Lost || (!Capturing && Outstanding == 0)) Close();
            return reply;
        }

        public void Close()
        {
            IsOpen = false;
            Capturing = false;
            Reset();
        }

        private void Reset()
        {
            members.Clear();
            membersWithReplies.Clear();
            outstanding.Clear();
            senders.Clear();
            notHealthy.Clear();
            replied.Clear();
        }
    }
}
