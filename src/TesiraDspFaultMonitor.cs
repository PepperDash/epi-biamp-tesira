using System;
using Crestron.SimplSharpPro.DeviceSupport;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Opt-in fault monitoring: polls 'DEVICE get activeFaultList' (the Tesira can't subscribe to
    /// it) and reports the active faults as feedbacks and bridge joins. Off unless
    /// faultPollIntervalSecs is set, the FaultPollingEnable join goes high, or SetFaultPolling is called.
    /// </summary>
    public class TesiraDspFaultMonitor : TesiraDspControlPoint
    {
        public const string AttributeCode = "activeFaultList";

        /// <summary>Shortest allowed poll interval; 1-9 is clamped up to this.</summary>
        public const int MinimumIntervalSecs = FaultPollInterval.MinimumSecs;

        /// <summary>Longest allowed poll interval (1 day); longer is clamped down to this.</summary>
        public const int MaximumIntervalSecs = FaultPollInterval.MaximumSecs;

        /// <summary>Interval used when the bridge join turns polling on and the config has none.</summary>
        public const int DefaultIntervalSecs = 60;

        private const string keyFormatter = "{0}--{1}";

        private readonly object timerLock = new object();
        private System.Timers.Timer pollTimer;
        private TesiraFaultList current = new TesiraFaultList();
        private string lastSignature;
        private bool errorLogged;

        // Guards current, lastSignature, IntervalSecs and replyGate. Never held while feedbacks or
        // FaultsChanged are published: a subscriber may send a DSP command, which takes the queue
        // lock, while a reply holds the queue lock and waits here.
        private readonly object stateLock = new object();

        // Serializes turning polling on and off (SetPolling, Start, Stop), so the interval and the
        // timer always change together: two callers at once (both bridges' enable joins, or a join
        // and devjson) can't leave the timer stopped while polling reports enabled. Held only around
        // the interval and timer change, never while sending or publishing, so it can't form a
        // cycle with the command queue lock.
        private readonly object controlLock = new object();

        // Tags each poll with a generation, so a reply to a poll sent before polling was turned off
        // is dropped (see FaultReplyGate).
        private readonly FaultReplyGate replyGate = new FaultReplyGate(AttributeCode);

        private const string PollCommand = "\"DEVICE\" get " + AttributeCode;

        /// <summary>The interval from config (0 = off), used when the bridge join turns polling on.</summary>
        public int ConfiguredIntervalSecs { get; }

        /// <summary>The interval polling runs at now; 0 = off.</summary>
        public int IntervalSecs { get; private set; }

        public bool PollingEnabled => IntervalSecs > 0;

        /// <summary>The last parsed fault list. Empty until the first poll answers.</summary>
        public TesiraFaultList Current => current;

        public BoolFeedback PollingEnabledFeedback { get; }
        public BoolFeedback HasFaultFeedback { get; }
        public IntFeedback ActiveFaultCountFeedback { get; }
        public IntFeedback FaultSeverityFeedback { get; }
        public StringFeedback FaultIdFeedback { get; }
        public StringFeedback FaultNameFeedback { get; }
        public StringFeedback FaultListFeedback { get; }

        /// <summary>Raised after each parsed poll whose result differs from the previous one.</summary>
        public event EventHandler FaultsChanged;

        public TesiraDspFaultMonitor(TesiraDsp parent, int configuredIntervalSecs)
            : base("DEVICE", "DEVICE", 0, 0, parent, string.Format(keyFormatter, parent.Key, "FaultMonitor"), "FaultMonitor", 0)
        {
            ConfiguredIntervalSecs = Normalize(configuredIntervalSecs);
            IntervalSecs = ConfiguredIntervalSecs;

            PollingEnabledFeedback = new BoolFeedback(Key + "-PollingEnabled", () => PollingEnabled);
            HasFaultFeedback = new BoolFeedback(Key + "-HasFault", () => current.HasFault);
            ActiveFaultCountFeedback = new IntFeedback(Key + "-ActiveFaultCount", () => current.ActiveFaultCount);
            FaultSeverityFeedback = new IntFeedback(Key + "-FaultSeverity", () => current.Severity);
            FaultIdFeedback = new StringFeedback(Key + "-FaultId", () => current.FaultId);
            FaultNameFeedback = new StringFeedback(Key + "-FaultName", () => current.FaultName);
            FaultListFeedback = new StringFeedback(Key + "-FaultList", () => current.FaultListText);

            Feedbacks.Add(PollingEnabledFeedback);
            Feedbacks.Add(HasFaultFeedback);
            Feedbacks.Add(ActiveFaultCountFeedback);
            Feedbacks.Add(FaultSeverityFeedback);
            Feedbacks.Add(FaultIdFeedback);
            Feedbacks.Add(FaultNameFeedback);
            Feedbacks.Add(FaultListFeedback);

            if (PollingEnabled)
                this.LogInformation("Fault monitoring on: polling every {interval}s", IntervalSecs);
        }

        /// <summary>0 or less = off; otherwise clamped to 10 s - 1 day (see <see cref="FaultPollInterval"/>).</summary>
        public static int Normalize(int secs) => FaultPollInterval.Normalize(secs);

        /// <summary>
        /// Called when the TTP session is ready (subscriptions done): starts the timer if polling is
        /// on and polls once so the joins have a value right away.
        /// </summary>
        public void Start()
        {
            lock (controlLock)
            {
                if (!PollingEnabled) return;
                StartTimer();
            }
            PollPeriodic(); // outside controlLock: sending takes the queue lock
        }

        /// <summary>Stops the timer (program stopping). The interval is kept.</summary>
        public void Stop()
        {
            lock (controlLock) StopTimer();
        }

        /// <summary>
        /// Changes the poll interval at runtime. 0 turns monitoring off and clears the fault values.
        /// </summary>
        public void SetPolling(int intervalSecs)
        {
            var secs = Normalize(intervalSecs);
            if (intervalSecs > 0 && secs != intervalSecs)
                this.LogInformation("Fault poll interval {requested}s is outside {min}-{max}s; using {secs}s",
                    intervalSecs, MinimumIntervalSecs, MaximumIntervalSecs, secs);

            bool wasEnabled;
            var cleared = false;
            lock (controlLock)
            {
                lock (stateLock)
                {
                    wasEnabled = PollingEnabled;
                    IntervalSecs = secs;
                    if (secs == 0)
                    {
                        // One step: drop replies to polls queued before this, and clear the values. A
                        // PollNow() sent after this point gets the new generation, so its reply is
                        // applied after the clear and can't be erased by it.
                        replyGate.PollingTurnedOff();
                        cleared = Store(new TesiraFaultList(), logChange: false);
                    }
                }
                if (secs > 0) StartTimer();
                else StopTimer();
            }

            // Outside controlLock: sending takes the queue lock, and Publish runs subscribers.
            if (secs > 0)
            {
                this.LogInformation("Fault monitoring on: polling every {interval}s", secs);
                if (!wasEnabled) PollPeriodic(); // sends nothing if polling was turned off meanwhile
            }
            else
            {
                if (wasEnabled) this.LogInformation("Fault monitoring off");
                Publish(cleared);
            }

            PollingEnabledFeedback.FireUpdate();
        }

        /// <summary>
        /// Polls once now (d13 / PollFaults), whether or not periodic polling is on. Its reply is
        /// applied unless polling is turned off before it arrives.
        /// </summary>
        public void PollNow()
        {
            string tag;
            lock (stateLock) tag = replyGate.Tag;
            Send(tag);
        }

        // Timer ticks and the first poll after turning on. A tick that lands after polling was
        // turned off sends nothing.
        private void PollPeriodic()
        {
            string tag;
            lock (stateLock)
            {
                if (!PollingEnabled) return;
                tag = replyGate.Tag;
            }
            Send(tag);
        }

        /// <summary>
        /// Queues one 'DEVICE get activeFaultList' in the background lane, behind user commands,
        /// tagged so its reply can be matched to this request. Skipped while the DSP is offline
        /// (the queue drops commands then anyway).
        /// </summary>
        private void Send(string tag)
        {
            if (Parent.CommunicationMonitor == null || !Parent.CommunicationMonitor.IsOnline)
            {
                this.LogVerbose("Fault poll skipped: DSP offline");
                return;
            }

            using (Parent.CommandQueue.BackgroundScope())
            {
                Parent.CommandQueue.EnqueueCommand(new QueuedCommand(PollCommand, tag, this, priority: (int)CommandPriority.Critical));
            }
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            if (!replyGate.IsFaultReply(attributeCode) || string.IsNullOrEmpty(message)) return;

            if (message.StartsWith("-ERR", StringComparison.Ordinal))
            {
                // Log once per session: firmware without activeFaultList would otherwise log every poll.
                if (!errorLogged)
                {
                    errorLogged = true;
                    this.LogWarning("Fault poll rejected by the DSP: {message}", message);
                }
                return;
            }

            var parsed = TesiraFaultList.Parse(message);
            if (parsed == null) return; // the echo, or a reply that isn't a value list

            errorLogged = false;

            bool changed;
            lock (stateLock)
            {
                // Drop the reply to any poll sent before polling was last turned off.
                if (!replyGate.ShouldApply(attributeCode))
                {
                    this.LogVerbose("Fault poll reply discarded: monitoring was turned off after it was sent");
                    return;
                }
                changed = Store(parsed, logChange: true);
            }
            Publish(changed);
        }

        /// <summary>Stores the new list and logs a change. Caller holds stateLock. True if it changed.</summary>
        private bool Store(TesiraFaultList parsed, bool logChange)
        {
            var previous = current;
            current = parsed;

            var signature = parsed.Signature;
            var changed = signature != lastSignature;
            lastSignature = signature;

            if (changed && logChange)
            {
                if (parsed.HasFault)
                    this.LogWarning("DSP faults: {faultName} ({faultId}), {count} active: {faults}",
                        parsed.FaultName, parsed.FaultId, parsed.ActiveFaultCount, parsed.FaultListText);
                else if (previous.HasFault)
                    this.LogInformation("DSP faults cleared: {faultName}", parsed.FaultName);
            }

            return changed;
        }

        /// <summary>
        /// Fires the value feedbacks and, if the list changed, FaultsChanged. Called outside stateLock.
        /// It runs on the reply path (inside the command queue), so a subscriber that throws is logged
        /// and can't stop the queue from finishing the response.
        /// </summary>
        private void Publish(bool changed)
        {
            try
            {
                HasFaultFeedback.FireUpdate();
                ActiveFaultCountFeedback.FireUpdate();
                FaultSeverityFeedback.FireUpdate();
                FaultIdFeedback.FireUpdate();
                FaultNameFeedback.FireUpdate();
                FaultListFeedback.FireUpdate();
            }
            catch (Exception e)
            {
                this.LogError(e, "Error in a fault feedback subscriber");
            }

            if (!changed) return;
            var handlers = FaultsChanged;
            if (handlers == null) return;

            // Invoke each handler on its own, so one subscriber's failure can't stop another's
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((EventHandler)handler)(this, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    this.LogError(e, "Error in a FaultsChanged handler");
                }
            }
        }

        private void StartTimer()
        {
            lock (timerLock)
            {
                pollTimer?.Stop();
                pollTimer?.Dispose();
                pollTimer = new System.Timers.Timer(IntervalSecs * 1000.0) { AutoReset = true };
                pollTimer.Elapsed += (s, e) => PollPeriodic();
                pollTimer.Start();
            }
        }

        private void StopTimer()
        {
            lock (timerLock)
            {
                if (pollTimer == null) return;
                pollTimer.Stop();
                pollTimer.Dispose();
                pollTimer = null;
            }
        }

        /// <summary>
        /// Links the fault joins. The enable join acts on transitions only, so a SIMPL program that
        /// leaves it low (including the low states sent when the EISC connects) never turns off
        /// monitoring that the config turned on. It changes runtime state only, never the config.
        /// </summary>
        public void LinkFaultJoins(BasicTriList trilist, JoinDataComplete enable, JoinDataComplete hasFault,
            JoinDataComplete pollNow, JoinDataComplete count, JoinDataComplete severity,
            JoinDataComplete faultId, JoinDataComplete faultName, JoinDataComplete faultList)
        {
            var lastEnable = false;
            trilist.SetBoolSigAction(enable.JoinNumber, high =>
            {
                if (high == lastEnable) return;
                lastEnable = high;
                SetPolling(high ? (ConfiguredIntervalSecs > 0 ? ConfiguredIntervalSecs : DefaultIntervalSecs) : 0);
            });
            trilist.SetSigTrueAction(pollNow.JoinNumber, PollNow);

            PollingEnabledFeedback.LinkInputSig(trilist.BooleanInput[enable.JoinNumber]);
            HasFaultFeedback.LinkInputSig(trilist.BooleanInput[hasFault.JoinNumber]);
            ActiveFaultCountFeedback.LinkInputSig(trilist.UShortInput[count.JoinNumber]);
            FaultSeverityFeedback.LinkInputSig(trilist.UShortInput[severity.JoinNumber]);
            FaultIdFeedback.LinkInputSig(trilist.StringInput[faultId.JoinNumber]);
            FaultNameFeedback.LinkInputSig(trilist.StringInput[faultName.JoinNumber]);
            FaultListFeedback.LinkInputSig(trilist.StringInput[faultList.JoinNumber]);

            // Re-send current values when SIMPL reconnects. The standalone DeviceInfo object only
            // re-fires its own feedbacks, so this is needed for both bridge modes to stay in sync.
            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;
                foreach (var feedback in Feedbacks) feedback.FireUpdate();
            };

            foreach (var feedback in Feedbacks) feedback.FireUpdate();
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            // Bridged through the DSP's device join map (full bridge) or TesiraDspDeviceInfo (standalone).
        }
    }
}
