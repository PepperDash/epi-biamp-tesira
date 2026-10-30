using System;
using System.Text.RegularExpressions;
using System.Timers;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;


namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDspRoomCombiner : TesiraDspControlPoint, IBasicVolumeWithFeedback, IVolumeComponent
    {
        private bool outIsMuted;
        protected bool OutIsMuted
        {
            get
            {
                return outIsMuted;
            }
            set
            {
                outIsMuted = value;
                MuteFeedback.FireUpdate();
            }
        }

        private double rawOutVolumeLevelDb;
        // False until the DSP has reported a levelOut value; min/max replies don't rescale before then.
        private bool hasOutLevel;

        // A levelOutMin/levelOutMax pair queued after an out-of-range publish and not yet answered (UTC ticks; 0 = none).
        // It expires, so a read dropped by a full queue or lost to a timeout can't block retries.
        private long rangePollStartedTicks;
        private static readonly long rangePollExpiryTicks = TimeSpan.FromSeconds(10).Ticks;

        private bool TryStartRangePoll()
        {
            var now = DateTime.UtcNow.Ticks;
            var started = System.Threading.Interlocked.Read(ref rangePollStartedTicks);
            if (started != 0 && now - started < rangePollExpiryTicks) return false;
            return System.Threading.Interlocked.CompareExchange(ref rangePollStartedTicks, now, started) == started;
        }

        private void EndRangePoll() => System.Threading.Interlocked.Exchange(ref rangePollStartedTicks, 0);
        private int outVolumeLevel;
        protected int OutVolumeLevel
        {
            get
            {
                return outVolumeLevel;
            }
            set
            {
                outVolumeLevel = value;
                VolumeLevelFeedback.FireUpdate();
            }
        }

        private int roomGroup;
        protected int RoomGroup
        {
            get
            {
                return roomGroup;
            }
            set
            {
                roomGroup = value;
                RoomGroupFeedback.FireUpdate();
            }
        }

        private const string keyFormatter = "{0}--{1}";
        /// <summary>
        /// Component Permissions
        /// </summary>
        public int Permissions { get; set; }

        /// <summary>
        /// Boolean Feedback for Mute State
        /// </summary>
        public BoolFeedback MuteFeedback { get; private set; }

        /// <summary>
        /// Boolean Feedback for Visbility State
        /// </summary>
        public BoolFeedback VisibleFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for Volume Level
        /// </summary>
        public IntFeedback VolumeLevelFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for Control Type
        /// </summary>
        public IntFeedback TypeFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for Permissions
        /// </summary>
        public IntFeedback PermissionsFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for RoomGroup
        /// </summary>
        public IntFeedback RoomGroupFeedback { get; private set; }

        private EPdtLevelTypes type;
        private string IncrementAmount { get; set; }
        private bool UseAbsoluteValue { get; set; }
        private int VolumeRepeatRateMs { get; set; }
        private int VolumeHoldTimeoutMs { get; set; }
        private string LevelControlPointTag { get { return InstanceTag1; } }

        private VolumeRamp volumeRamp;

        System.Timers.Timer pollTimer;

        /// <summary>
        /// Subscription identifier for Room Combiner
        /// </summary>
        public string LevelCustomName { get; private set; }

        /// <summary>
        /// Minimum fader level
        /// </summary>
        public double MinLevel { get; private set; }

        /// <summary>
        /// Maximum fader level
        /// </summary>
        public double MaxLevel { get; private set; }

        /// <summary>
        /// Checks if a valid subscription string has been recieved for all subscriptions
        /// </summary>
        public override bool IsSubscribed
        {
            get
            {
                return levelIsSubscribed;
            }
            protected set { }
        }

        private bool AutomaticUnmuteOnVolumeUp { get; set; }

        private bool levelIsSubscribed;

        /// <summary>
        /// Constructor for Tesira Dsp Room Combiner Component
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="config">configuration object</param>
        /// <param name="parent">Parent Object</param>
		public TesiraDspRoomCombiner(string key, TesiraRoomCombinerBlockConfig config, TesiraDsp parent)
            : base(config.RoomCombinerInstanceTag, "", config.RoomIndex, 0, parent, string.Format(keyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Initialize(config);
        }

        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="config">Configuration Object</param>
		private void Initialize(TesiraRoomCombinerBlockConfig config)
        {

            this.LogVerbose("Adding RoomCombiner {key}", Key);

            IsSubscribed = false;

            type = config.IsMic ? EPdtLevelTypes.Microphone : EPdtLevelTypes.Speaker;

            UseAbsoluteValue = config.UseAbsoluteValue;
            Enabled = config.Enabled;
            Permissions = config.Permissions;
            IncrementAmount = config.IncrementAmount;
            AutomaticUnmuteOnVolumeUp = config.UnmuteOnVolChange;
            VolumeRepeatRateMs = config.VolumeRepeatRateMs;
            VolumeHoldTimeoutMs = config.VolumeHoldTimeoutMs ?? 10000;
            volumeRamp = new VolumeRamp(
                isUp => SendFullCommand(isUp ? "increment" : "decrement", "levelOut", IncrementAmount, 1),
                () => VolumeRepeatRateMs,
                () => VolumeHoldTimeoutMs,
                onUpPress: () => { if (AutomaticUnmuteOnVolumeUp && outIsMuted) MuteOff(); },
                logWarning: message => this.LogWarning("{LevelControlPointTag}: {message}", LevelControlPointTag, message));

            pollTimer = new System.Timers.Timer();
            pollTimer.Elapsed += (sender, e) => DoPoll();
            pollTimer.AutoReset = false;
            pollTimer.Enabled = false;

            InitializeFeedbacks();
        }

        /// <summary>
        /// Helper method to restart a timer with a new interval
        /// </summary>
        private void ResetTimer(System.Timers.Timer timer, double intervalMs)
        {
            timer.Stop();
            timer.Interval = intervalMs;
            timer.Start();
        }

        private void InitializeFeedbacks()
        {
            MuteFeedback = new BoolFeedback(Key + "-MuteFeedback", () => OutIsMuted);
            VisibleFeedback = new BoolFeedback(Key + "-VisibleFeedback", () => Enabled);

            RoomGroupFeedback = new IntFeedback(Key + "-RoomGroupFeedback", () => RoomGroup);
            VolumeLevelFeedback = new IntFeedback(Key + "-LevelFeedback", () => OutVolumeLevel);
            TypeFeedback = new IntFeedback(Key + "-TypeFeedback", () => (ushort)type);
            PermissionsFeedback = new IntFeedback(Key + "-PermissionsFeedback", () => Permissions);

            Feedbacks.Add(MuteFeedback);
            Feedbacks.Add(VolumeLevelFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(VisibleFeedback);
            Feedbacks.Add(TypeFeedback);
            Feedbacks.Add(PermissionsFeedback);

            Parent.Feedbacks.AddRange(Feedbacks);
        }


        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe()
        {
            LevelCustomName = string.Format("{0}__roomCombiner{1}", InstanceTag1, Index1);
            AddCustomName(LevelCustomName);
            // A range poll lost with a previous session's queue must not block the next one.
            EndRangePoll();
            // Limits first: the queue is FIFO, so they arrive before the subscription's first publish
            SendFullCommand("get", "levelOutMin", null, 1);
            SendFullCommand("get", "levelOutMax", null, 1);
            // The levelOut subscription is what marks this component subscribed (ParseSubscriptionMessage),
            // which is also what the watchdog checks
            SendSubscriptionCommand(LevelCustomName, "levelOut", 250, 1);
            SendFullCommand("get", "group", null, 1);
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {

            levelIsSubscribed = false;
            LevelCustomName = string.Format("{0}__roomCombiner{1}", InstanceTag1, Index1);
            SendUnSubscriptionCommand(LevelCustomName, "levelOut", 1);
        }

        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="data"></param>
        public override void ParseSubscriptionMessage(string customName, string data)
        {
            if (customName != LevelCustomName) return;

            ApplyOutLevel(double.Parse(data));

            // No poll-timer reset here: frequent level publishes would otherwise keep postponing the
            // poll that refreshes mute and group. The GET reply handlers restart it.
            levelIsSubscribed = true;
        }

        /// <summary>
        /// Updates level feedback from a levelOut value in dB, from a subscription or a GET reply.
        /// </summary>
        private void ApplyOutLevel(double value)
        {
            rawOutVolumeLevelDb = value;
            hasOutLevel = true;

            if (!UseAbsoluteValue && (value < MinLevel || value > MaxLevel))
            {
                // Value is outside our known range — the DSP range likely changed externally.
                // Clamp to boundary so the ushort cast doesn't wrap, then re-poll to get the new range.
                this.LogInformation(
                    "LevelOut {val} dB outside known range [{min},{max}] — clamping feedback and re-polling min/max.",
                    value, MinLevel, MaxLevel);
                var clamped = Math.Max(MinLevel, Math.Min(MaxLevel, value));
                OutVolumeLevel = (ushort)clamped.Scale(MinLevel, MaxLevel, 0, 65535, this);
                // One min/max pair in flight: publishes keep coming every 250 ms while the level moves.
                if (TryStartRangePoll())
                {
                    GetMinLevel();
                    GetMaxLevel();
                }
            }
            else
            {
                OutVolumeLevel = UseAbsoluteValue ? (ushort)value : (ushort)value.Scale(MinLevel, MaxLevel, 0, 65535, this);
            }
        }

        const string parsePattern = "[^ ]* (.*)";
        private readonly static Regex parseRegex = new Regex(parsePattern);

        // Matches: -ERR INVALID_PARAMETER Value out of range: attr:levelOut min:-45 max:-5 val:-3
        private readonly static Regex outOfRangeRegex = new Regex(
            @"-ERR INVALID_PARAMETER Value out of range: attr:\S+ min:(?<min>[\d.\-]+) max:(?<max>[\d.\-]+) val:(?<val>[\d.\-]+)",
            RegexOptions.Compiled);

        private void HandleOutOfRangeError(string message)
        {
            var m = outOfRangeRegex.Match(message);
            if (!m.Success) return;

            var reportedMin = double.Parse(m.Groups["min"].Value);
            var reportedMax = double.Parse(m.Groups["max"].Value);

            var rangeChanged = false;

            if (Math.Abs(reportedMin - MinLevel) > 0.001)
            {
                this.LogInformation("MinLevel updated by DSP error response: {old} -> {new}", MinLevel, reportedMin);
                MinLevel = reportedMin;
                rangeChanged = true;
            }

            if (Math.Abs(reportedMax - MaxLevel) > 0.001)
            {
                this.LogInformation("MaxLevel updated by DSP error response: {old} -> {new}", MaxLevel, reportedMax);
                MaxLevel = reportedMax;
                rangeChanged = true;
            }

            if (rangeChanged)
            {
                // val is the attempted (rejected) level, not the DSP's: rescale the last known actual
                // level against the new range, and read the actual level back.
                if (hasOutLevel)
                {
                    var clampedRaw = Math.Max(MinLevel, Math.Min(MaxLevel, rawOutVolumeLevelDb));
                    OutVolumeLevel = UseAbsoluteValue
                        ? (ushort)clampedRaw
                        : (ushort)clampedRaw.Scale(MinLevel, MaxLevel, 0, 65535, this);
                }
                GetVolume();
            }

            // Stop repeating in both directions; already-queued repeat callbacks see the cleared
            // held state and do nothing. Hold-timeout timers stay armed.
            this.LogDebug("Out-of-range error on {LevelControlPointTag} — stopping ramp timers and suppressing held state.", LevelControlPointTag);
            volumeRamp.Cancel();

            // Clamp to the boundary that was exceeded so that a step size larger than the
            // remaining range doesn't strand the fader short of the limit.
            var val = double.Parse(m.Groups["val"].Value);
            // Commands run in order, so a correction queued now would land after any newer level
            // change (a set, or a step either way) and undo it. If that change is rejected too, its
            // own error runs this recovery with nothing queued behind it.
            if (Parent.CommandQueue.HasPendingLevelChange(this))
            {
                this.LogDebug("Skipping boundary correction for {LevelControlPointTag}: a newer level change is queued.", LevelControlPointTag);
                return;
            }
            if (val > MaxLevel)
            {
                this.LogDebug("Clamping levelOut to MaxLevel {MaxLevel} after out-of-range increment.", MaxLevel);
                SendFullCommand("set", "levelOut", string.Format("{0:0.000000}", MaxLevel), 1);
            }
            else if (val < MinLevel)
            {
                this.LogDebug("Clamping levelOut to MinLevel {MinLevel} after out-of-range decrement.", MinLevel);
                SendFullCommand("set", "levelOut", string.Format("{0:0.000000}", MinLevel), 1);
            }
        }


        /// <summary>
        /// Parses a non subscription response
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command</param>
        /// <param name="message">The message to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                this.LogVerbose("Parsing Message: {message} attributeCode: {attributeCode}", message, attributeCode);

                // The max read is queued after the min one: its reply, +OK or -ERR, ends the range poll.
                if (attributeCode == "levelOutMax") EndRangePoll();

                // Only level steps feed volume recovery; a rejected group (or other) value must not
                // overwrite the level limits.
                if (attributeCode == "levelOut" &&
                    message.IndexOf("-ERR INVALID_PARAMETER", StringComparison.Ordinal) >= 0)
                {
                    var isOutOfRangeError = Regex.IsMatch(message, @"\bout of range\b", RegexOptions.IgnoreCase);
                    if (isOutOfRangeError)
                    {
                        HandleOutOfRangeError(message);
                        return;
                    }
                }

                // Parse an "+OK" message

                var match = parseRegex.Match(message);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                this.LogDebug("Response: {attributeCode} Value: {value}", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;
                switch (attributeCode)
                {
                    case "levelOutMin":
                        {
                            MinLevel = double.Parse(value);
                            this.LogDebug("MinLevel: {MinLevel}", MinLevel);
                            if (!UseAbsoluteValue && hasOutLevel && MinLevel < MaxLevel)
                            {
                                var clampedRawLevel = Math.Max(MinLevel, Math.Min(MaxLevel, rawOutVolumeLevelDb));
                                OutVolumeLevel = (ushort)clampedRawLevel.Scale(MinLevel, MaxLevel, 0, 65535, this);
                            }
                            break;
                        }
                    case "levelOutMax":
                        {
                            MaxLevel = double.Parse(value);
                            this.LogDebug("MaxLevel: {MaxLevel}", MaxLevel);
                            if (!UseAbsoluteValue && hasOutLevel && MinLevel < MaxLevel)
                            {
                                var clampedRawLevel = Math.Max(MinLevel, Math.Min(MaxLevel, rawOutVolumeLevelDb));
                                OutVolumeLevel = (ushort)clampedRawLevel.Scale(MinLevel, MaxLevel, 0, 65535, this);
                            }
                            break;
                        }
                    case "levelOut":
                        {
                            ApplyOutLevel(double.Parse(value));
                            ResetTimer(pollTimer, 30000);
                            break;
                        }
                    case "muteOut":
                        {
                            OutIsMuted = bool.Parse(value);
                            this.LogDebug("MuteState is {value}", value);
                            pollTimer.Stop();
                            pollTimer.Interval = 30000;
                            pollTimer.Start();
                            break;
                        }

                    case "group":
                        {
                            RoomGroup = int.Parse(value);
                            this.LogDebug("Room Group: {RoomGroup}", RoomGroup);
                            // Arm the poll only when it is idle: Subscribe() reads group too, and
                            // watchdog probes call Subscribe() as often as every 10 s, which would
                            // otherwise keep pushing the poll (and the muteOut read) back forever.
                            if (!pollTimer.Enabled)
                            {
                                pollTimer.Interval = 30000;
                                pollTimer.Start();
                            }
                            break;
                        }
                    default:
                        {
                            this.LogDebug("Response does not match expected attribute codes: '{message}'", message);
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                this.LogError("Unable to parse {message}: {exception}", message, e.Message);
                this.LogDebug(e, "Stack Trace: ");
            }

        }

        /// <summary>
        /// Turns the mute off
        /// </summary>
        public void MuteOff()
        {
            SendFullCommand("set", "muteOut", "false", 1);
        }

        /// <summary>
        /// Turns the mute on
        /// </summary>
        public void MuteOn()
        {
            SendFullCommand("set", "muteOut", "true", 1);
        }

        /// <summary>
        /// Sets the volume to a specified level
        /// </summary>
        /// <param name="level"></param>
        public void SetVolume(ushort level)
        {
            this.LogDebug("volume: {level}", level);
            // Unmute volume if new level is higher than existing
            if (level > outVolumeLevel && AutomaticUnmuteOnVolumeUp)
                if (outIsMuted)
                    MuteOff();
            var newLevel = (double)level;

            var volumeLevel = UseAbsoluteValue ? level : newLevel.Scale(0, 65535, MinLevel, MaxLevel, this);

            SendFullCommand("set", "levelOut", string.Format("{0:0.000000}", volumeLevel), 1);
        }

        /// <summary>
        /// Set the room group to the specified value
        /// </summary>
        /// <param name="group"></param>
        public void SetRoomGroup(ushort group)
        {
            this.LogDebug("group: {group}", group);
            SendFullCommand("set", "group", Convert.ToString(group), 1);
        }

        /// <summary>
        /// Polls all data for this control
        /// </summary>
        public override void DoPoll()
        {
            GetVolume();
            GetMute();
            GetRoomGroup();
        }

        /// <summary>
        /// Polls the current volume level
        /// </summary>
        public void GetVolume()
        {
            SendFullCommand("get", "levelOut", string.Empty, 1);
        }

        /// <summary>
        /// Polls minimum level of fader component
        /// </summary>
        public void GetMinLevel()
        {
            SendFullCommand("get", "levelOutMin", null, 1);
        }

        /// <summary>
        /// poll maximum level of fader component
        /// </summary>
        public void GetMaxLevel()
        {
            SendFullCommand("get", "levelOutMax", null, 1);
        }


        /// <summary>
        /// Polls the current mute state
        /// </summary>
        public void GetMute()
        {
            SendFullCommand("get", "muteOut", string.Empty, 1);
        }

        /// <summary>
        /// Polls the current room group
        /// </summary>
        public void GetRoomGroup()
        {
            SendFullCommand("get", "group", string.Empty, 1);
        }

        /// <summary>
        /// Toggles mute status
        /// </summary>
        public void MuteToggle()
        {
            SendFullCommand("toggle", "muteOut", string.Empty, 1);
        }

        /// <summary>
        /// Decrements volume level
        /// </summary>
        /// <param name="press"></param>
        public void VolumeDown(bool press)
        {
            this.LogDebug("VolumeDown {press} for {LevelControlPointTag}", press ? "press" : "release", LevelControlPointTag);
            volumeRamp.Set(false, press);
        }

        /// <summary>
        /// Increments volume level
        /// </summary>
        /// <param name="press"></param>
        public void VolumeUp(bool press)
        {
            this.LogDebug("VolumeUp {press} for {LevelControlPointTag}", press ? "press" : "release", LevelControlPointTag);
            volumeRamp.Set(true, press);
        }




        public enum EPdtLevelTypes
        {
            Speaker = 0,
            Microphone = 1
        }


        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraRoomCombinerJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraRoomCombinerJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            this.LogVerbose("Tesira Room Combiner {0} connect", Key);

            var genericChannel = this as IBasicVolumeWithFeedback;
            if (!Enabled) return;

            this.LogDebug("TesiraChannel {0} Is Enabled", Key);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);
            VisibleFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Visible.JoinNumber]);
            TypeFeedback.LinkInputSig(trilist.UShortInput[joinMap.Type.JoinNumber]);
            PermissionsFeedback.LinkInputSig(trilist.UShortInput[joinMap.Permissions.JoinNumber]);
            RoomGroupFeedback.LinkInputSig(trilist.UShortInput[joinMap.Group.JoinNumber]);

            genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.MuteToggle.JoinNumber]);
            genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.MuteOn.JoinNumber]);
            genericChannel.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.MuteOff.JoinNumber]);
            genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.Volume.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.MuteToggle.JoinNumber, genericChannel.MuteToggle);
            trilist.SetSigTrueAction(joinMap.MuteOn.JoinNumber, genericChannel.MuteOn);
            trilist.SetSigTrueAction(joinMap.MuteOff.JoinNumber, genericChannel.MuteOff);

            trilist.SetBoolSigAction(joinMap.VolumeUp.JoinNumber, genericChannel.VolumeUp);
            trilist.SetBoolSigAction(joinMap.VolumeDown.JoinNumber, genericChannel.VolumeDown);

            trilist.SetUShortSigAction(joinMap.Volume.JoinNumber, u => { if (u > 0) { genericChannel.SetVolume(u); } });

            trilist.SetUShortSigAction(joinMap.Group.JoinNumber, u => { if (u > 0) { SetRoomGroup(u); } });

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine)
                {
                    // Panel dropped — force release of any held volume button to prevent runaway
                    VolumeUp(false);
                    VolumeDown(false);
                    return;
                }

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }
            };

        }

    }
}
