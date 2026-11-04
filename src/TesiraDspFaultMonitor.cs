using System;
using Crestron.SimplSharpPro.DeviceSupport;
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
        public const int MinimumIntervalSecs = 10;

        /// <summary>Interval used when the bridge join turns polling on and the config has none.</summary>
        public const int DefaultIntervalSecs = 60;

        private const string keyFormatter = "{0}--{1}";

        private readonly object timerLock = new object();
        private System.Timers.Timer pollTimer;
        private TesiraFaultList current = new TesiraFaultList();
        private string lastSignature;
        private bool errorLogged;

        // While polling is off, only the reply to an explicit PollNow() is applied: a reply to a poll
        // queued before polling was turned off must not restore the values that turning it off cleared.
        private readonly object stateLock = new object();
        private bool oneShotPending;

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

        /// <summary>0 or less = off; 1-9 is clamped to the minimum.</summary>
        public static int Normalize(int secs)
        {
            if (secs <= 0) return 0;
            return Math.Max(secs, MinimumIntervalSecs);
        }

        /// <summary>
        /// Called when the TTP session is ready (subscriptions done): starts the timer if polling is
        /// on and polls once so the joins have a value right away.
        /// </summary>
        public void Start()
        {
            if (!PollingEnabled) return;
            StartTimer();
            PollNow();
        }

        /// <summary>Stops the timer (program stopping). The interval is kept.</summary>
        public void Stop()
        {
            StopTimer();
        }

        /// <summary>
        /// Changes the poll interval at runtime. 0 turns monitoring off and clears the fault values.
        /// </summary>
        public void SetPolling(int intervalSecs)
        {
            var secs = Normalize(intervalSecs);
            if (intervalSecs > 0 && secs != intervalSecs)
                this.LogInformation("Fault poll interval {secs}s is below the minimum; using {min}s", intervalSecs, secs);

            bool wasEnabled;
            lock (stateLock)
            {
                wasEnabled = PollingEnabled;
                IntervalSecs = secs;
                if (secs == 0) oneShotPending = false; // drop replies to polls queued before this
            }

            if (secs > 0)
            {
                this.LogInformation("Fault monitoring on: polling every {interval}s", secs);
                StartTimer();
                if (!wasEnabled) PollNow();
            }
            else
            {
                StopTimer();
                if (wasEnabled) this.LogInformation("Fault monitoring off");
                lock (stateLock) Apply(new TesiraFaultList(), logChange: false);
            }

            PollingEnabledFeedback.FireUpdate();
        }

        /// <summary>
        /// Queues one 'DEVICE get activeFaultList' in the background lane, behind user commands.
        /// Skipped while the DSP is offline (the queue drops commands then anyway).
        /// </summary>
        public void PollNow()
        {
            if (Parent.CommunicationMonitor == null || !Parent.CommunicationMonitor.IsOnline)
            {
                this.LogVerbose("Fault poll skipped: DSP offline");
                return;
            }

            lock (stateLock)
            {
                if (!PollingEnabled) oneShotPending = true;
            }

            using (Parent.CommandQueue.BackgroundScope())
            {
                SendFullCommand("get", AttributeCode, null, 999);
            }
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            if (attributeCode != AttributeCode || string.IsNullOrEmpty(message)) return;

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

            lock (stateLock)
            {
                if (!PollingEnabled)
                {
                    // Off: apply only the answer to an explicit one-shot poll, never a stale periodic one.
                    if (!oneShotPending)
                    {
                        this.LogVerbose("Fault poll reply discarded: monitoring was turned off");
                        return;
                    }
                    oneShotPending = false;
                }
                Apply(parsed, logChange: true);
            }
        }

        private void Apply(TesiraFaultList parsed, bool logChange)
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

            HasFaultFeedback.FireUpdate();
            ActiveFaultCountFeedback.FireUpdate();
            FaultSeverityFeedback.FireUpdate();
            FaultIdFeedback.FireUpdate();
            FaultNameFeedback.FireUpdate();
            FaultListFeedback.FireUpdate();

            if (changed) FaultsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StartTimer()
        {
            lock (timerLock)
            {
                pollTimer?.Stop();
                pollTimer?.Dispose();
                pollTimer = new System.Timers.Timer(IntervalSecs * 1000.0) { AutoReset = true };
                pollTimer.Elapsed += (s, e) => PollNow();
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
