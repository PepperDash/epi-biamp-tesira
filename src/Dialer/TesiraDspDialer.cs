using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Messengers;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Common.AudioCodec;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Dialer
{
    /// <summary>
    /// Represents a single Tesira dialer LINE. A line is the "codec/phone": it owns one phonebook
    /// (speed-dial list) and one or more call appearances (<see cref="TesiraCallAppearance"/>). Each
    /// appearance tracks its own call. Line-level state (do-not-disturb, auto-answer, display name,
    /// last dialed) is shared by all appearances on the line.
    /// </summary>
    public partial class TesiraDspDialer : TesiraDspDialerControlPoint, IAudioCodecPhonebook, IJoinCalls
    {
        /// <summary>
        /// Maximum number of speed-dial entries supported per line by the Tesira dialer block.
        /// </summary>
        public const int MaxSpeedDialEntries = 16;

        /// <summary>
        /// Collection of all feedbacks owned by this line (line-level plus every appearance's feedbacks).
        /// </summary>
        public FeedbackCollection<Feedback> Feedbacks;

        /// <summary>
        /// True when this line is a VoIP line; false for a POTS line.
        /// </summary>
        public bool IsVoip { get; private set; }

        /// <summary>
        /// Line number on the device (1-based).
        /// </summary>
        public int LineNumber { get; private set; }

        /// <summary>
        /// When true, the dial string is cleared after a call ends.
        /// </summary>
        public bool ClearOnHangup { get; private set; }

        /// <summary>
        /// When true, DTMF digits sent during an active call are appended to the dial string.
        /// </summary>
        public bool AppendDtmf { get; private set; }

        /// <summary>
        /// Call appearances belonging to this line, ordered by appearance number.
        /// </summary>
        public List<TesiraCallAppearance> Appearances { get; private set; }

        /// <summary>
        /// Last dialed number for the line.
        /// </summary>
        public string LastDialed { get; protected set; }

        /// <summary>
        /// Number to be displayed on remote caller ID for the line.
        /// </summary>
        public string DisplayNumber { get; protected set; }

        /// <summary>
        /// Current Do Not Disturb state for the line.
        /// </summary>
        public bool DoNotDisturbState { get; protected set; }

        /// <summary>
        /// Current Auto Answer state for the line.
        /// </summary>
        public bool AutoAnswerState { get; protected set; }

        /// <summary>True when the VoIP line is registered and ready (VoIP only).</summary>
        public bool LineReady { get; protected set; }

        /// <summary>True when a dial tone is detected on the line (POTS only).</summary>
        public bool DialToneDetected { get; protected set; }

        /// <summary>True when a busy tone is detected on the line (POTS only).</summary>
        public bool BusyToneDetected { get; protected set; }

        /// <summary>True when a ring-back tone is detected on the line (POTS only).</summary>
        public bool RingBackToneDetected { get; protected set; }

        /// <summary>True when an inbound ring is detected on the line (POTS only).</summary>
        public bool RingDetected { get; protected set; }

        /// <summary>True when the line is dialing (POTS only).</summary>
        public bool Dialing { get; protected set; }

        #region Subscription identifiers

        /// <summary>Dialer subscription identifier.</summary>
        public string DialerCustomName { get; protected set; }
        /// <summary>ControlStatus subscription identifier.</summary>
        public string ControlStatusCustomName { get; protected set; }
        /// <summary>AutoAnswer subscription identifier.</summary>
        public string AutoAnswerCustomName { get; protected set; }
        /// <summary>HookState subscription identifier.</summary>
        public string HookStateCustomName { get; protected set; }
        /// <summary>POTS dialer subscription identifier.</summary>
        public string PotsDialerCustomName { get; protected set; }
        /// <summary>Last-dialed subscription identifier.</summary>
        public string LastDialedCustomName { get; protected set; }
        /// <summary>Line-ready subscription identifier (VoIP).</summary>
        public string LineReadyCustomName { get; protected set; }
        /// <summary>Dial-tone-detected subscription identifier (POTS).</summary>
        public string DialToneCustomName { get; protected set; }
        /// <summary>Busy-tone-detected subscription identifier (POTS).</summary>
        public string BusyToneCustomName { get; protected set; }
        /// <summary>Ring-back-tone-detected subscription identifier (POTS).</summary>
        public string RingBackToneCustomName { get; protected set; }
        /// <summary>Ring-detected subscription identifier (POTS).</summary>
        public string RingDetectedCustomName { get; protected set; }
        /// <summary>Dialing subscription identifier (POTS).</summary>
        public string DialingCustomName { get; protected set; }

        #endregion

        #region Line-level feedbacks

        /// <summary>Boolean feedback for Auto Answer status.</summary>
        public BoolFeedback AutoAnswerFeedback;
        /// <summary>Boolean feedback for Do Not Disturb status.</summary>
        public BoolFeedback DoNotDisturbFeedback;
        /// <summary>String feedback for the last dialed number.</summary>
        public StringFeedback LastDialedFeedback;
        /// <summary>String feedback for the line friendly name.</summary>
        public StringFeedback NameFeedback;
        /// <summary>String feedback for the displayed number.</summary>
        public StringFeedback DisplayNumberFeedback;
        /// <summary>Boolean feedback indicating at least one appearance is in a local conference.</summary>
        public BoolFeedback ConferenceActiveFeedback;
        /// <summary>Int feedback reporting the number of appearances currently in a local conference.</summary>
        public IntFeedback ConferenceCountFeedback;
        /// <summary>Boolean feedback indicating the VoIP line is registered and ready (VoIP only).</summary>
        public BoolFeedback LineReadyFeedback;
        /// <summary>Boolean feedback indicating a dial tone is detected on the line (POTS only).</summary>
        public BoolFeedback DialToneDetectedFeedback;
        /// <summary>Boolean feedback indicating a busy tone is detected on the line (POTS only).</summary>
        public BoolFeedback BusyToneDetectedFeedback;
        /// <summary>Boolean feedback indicating a ring-back tone is detected on the line (POTS only).</summary>
        public BoolFeedback RingBackToneDetectedFeedback;
        /// <summary>Boolean feedback indicating an inbound ring is detected on the line (POTS only).</summary>
        public BoolFeedback RingDetectedFeedback;
        /// <summary>Boolean feedback indicating the line is dialing (POTS only).</summary>
        public BoolFeedback DialingFeedback;

        #endregion

        #region Subscription state

        /// <summary>
        /// Subscription state of the line.
        /// </summary>
        public override bool IsSubscribed
        {
            get
            {
                if (IsVoip)
                    return VoipIsSubscribed && AutoAnswerIsSubscribed;
                return PotsIsSubscribed;
            }
            protected set { }
        }

        private bool VoipIsSubscribed { get; set; }
        private bool AutoAnswerIsSubscribed { get; set; }
        private bool PotsIsSubscribed { get; set; }

        #endregion

        #region Phonebook

        private bool _phonebookEnabled;
        private int _phonebookEntryCount;
        private List<CodecPhonebookEntry> _phonebookBuffer;
        private int _pendingPhonebookResponses;
        private List<CodecPhonebookEntry> _phonebookEntries = new List<CodecPhonebookEntry>();

        // Optional device attributes pushed on subscribe (see TesiraDialerControlBlockConfig).
        private bool? _redialEnable;
        private string _autoAnswerRingCount;

        /// <summary>
        /// Raised when the list of phonebook (speed-dial) entries changes.
        /// </summary>
        public event EventHandler<PhonebookListChangedEventArgs> ListChanged;

        /// <summary>
        /// The current list of phonebook (speed-dial) entries for the line.
        /// </summary>
        public List<CodecPhonebookEntry> PhonebookEntries
        {
            get { return _phonebookEntries; }
        }

        #endregion

        /// <summary>
        /// Constructor for the Tesira DSP dialer line.
        /// </summary>
        /// <param name="key">Unique key.</param>
        /// <param name="config">Line configuration.</param>
        /// <param name="parent">Parent device.</param>
        public TesiraDspDialer(string key, TesiraDialerControlBlockConfig config, TesiraDsp parent)
            : base(key, config.DialerInstanceTag, config.ControlStatusInstanceTag, config.Index, 1, parent, config.BridgeIndex)
        {
            Key = string.Format("{0}--{1}", parent.Key, key);

            Feedbacks = new FeedbackCollection<Feedback>();

            AutoAnswerFeedback = new BoolFeedback(Key + "-AutoAnswerFeedback", () => AutoAnswerState);
            DoNotDisturbFeedback = new BoolFeedback(Key + "-DoNotDisturbFeedback", () => DoNotDisturbState);
            LastDialedFeedback = new StringFeedback(Key + "-LastDialedFeedback", () => LastDialed);
            NameFeedback = new StringFeedback(Key + "-NameFeedback", () => Name);
            DisplayNumberFeedback = new StringFeedback(Key + "-DisplayNumberFeedback", () => DisplayNumber);
            ConferenceActiveFeedback = new BoolFeedback(Key + "-ConferenceActiveFeedback", () => ConferenceActive);
            ConferenceCountFeedback = new IntFeedback(Key + "-ConferenceCountFeedback", () => ConferenceCount);
            LineReadyFeedback = new BoolFeedback(Key + "-LineReadyFeedback", () => LineReady);
            DialToneDetectedFeedback = new BoolFeedback(Key + "-DialToneDetectedFeedback", () => DialToneDetected);
            BusyToneDetectedFeedback = new BoolFeedback(Key + "-BusyToneDetectedFeedback", () => BusyToneDetected);
            RingBackToneDetectedFeedback = new BoolFeedback(Key + "-RingBackToneDetectedFeedback", () => RingBackToneDetected);
            RingDetectedFeedback = new BoolFeedback(Key + "-RingDetectedFeedback", () => RingDetected);
            DialingFeedback = new BoolFeedback(Key + "-DialingFeedback", () => Dialing);

            Feedbacks.Add(AutoAnswerFeedback);
            Feedbacks.Add(DoNotDisturbFeedback);
            Feedbacks.Add(LastDialedFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(DisplayNumberFeedback);
            Feedbacks.Add(ConferenceActiveFeedback);
            Feedbacks.Add(ConferenceCountFeedback);
            Feedbacks.Add(LineReadyFeedback);
            Feedbacks.Add(DialToneDetectedFeedback);
            Feedbacks.Add(BusyToneDetectedFeedback);
            Feedbacks.Add(RingBackToneDetectedFeedback);
            Feedbacks.Add(RingDetectedFeedback);
            Feedbacks.Add(DialingFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);
        }

        private void Initialize(TesiraDialerControlBlockConfig config)
        {
            if (config.Enabled)
            {
                DeviceManager.AddDevice(this);
            }

            this.LogVerbose("Adding Dialer Line {key}", Key);

            IsSubscribed = false;
            PotsIsSubscribed = false;
            VoipIsSubscribed = false;
            AutoAnswerIsSubscribed = false;

            Label = config.Label;
            IsVoip = config.IsVoip;
            LineNumber = config.Index;
            AppendDtmf = config.AppendDtmf;
            ClearOnHangup = config.ClearOnHangup;
            Enabled = config.Enabled;
            DisplayNumber = config.DisplayNumber;

            _phonebookEnabled = config.Phonebook != null && config.Phonebook.Enabled;
            _phonebookEntryCount = config.Phonebook != null && config.Phonebook.EntryCount.HasValue
                ? Math.Min(Math.Max(config.Phonebook.EntryCount.Value, 0), MaxSpeedDialEntries)
                : MaxSpeedDialEntries;

            _redialEnable = config.RedialEnable;
            _autoAnswerRingCount = config.AutoAnswerRingCount;

            // Build the call appearances for this line.
            Appearances = new List<TesiraCallAppearance>();
            ActiveCalls = new List<CodecActiveCallItem>();

            if (config.CallAppearances != null && config.CallAppearances.Count > 0)
            {
                foreach (var kvp in config.CallAppearances.OrderBy(x => x.Key))
                {
                    var appearanceConfig = kvp.Value ?? new TesiraDialerCallAppearanceConfig();
                    var appearance = new TesiraCallAppearance(this, kvp.Key, appearanceConfig.Label, appearanceConfig.BridgeIndex);
                    Appearances.Add(appearance);
                }
            }
            else
            {
                // Default to a single appearance on this line.
                Appearances.Add(new TesiraCallAppearance(this, 1, Label, BridgeIndex));
            }

            foreach (var appearance in Appearances)
            {
                ActiveCalls.Add(appearance.ActiveCall);
                Feedbacks.AddRange(appearance.Feedbacks);
                Parent.Feedbacks.AddRange(appearance.Feedbacks);
            }
        }

        /// <summary>
        /// Called by a call appearance whenever its call status changes so the line can bubble the
        /// change up to <see cref="AudioCodecBase"/> consumers.
        /// </summary>
        /// <param name="appearance">The appearance whose status changed.</param>
        public void OnAppearanceCallStatusChanged(TesiraCallAppearance appearance)
        {
            if (appearance == null) return;
            ConferenceActiveFeedback.FireUpdate();
            ConferenceCountFeedback.FireUpdate();
            
            // Guard against messenger system not being ready during program startup
            try
            {
                OnCallStatusChange(appearance.ActiveCall);
            }
            catch (NullReferenceException ex)
            {
                this.LogDebug(ex, "Messenger not ready for call status change - {id}", appearance.Key);
            }
            catch (Exception ex)
            {
                this.LogWarning(ex, "Error sending call status change for appearance {id}", appearance.Key);
            }
        }

        #region Conferencing

        /// <summary>
        /// True when at least one appearance on this line is part of a local conference.
        /// </summary>
        public bool ConferenceActive
        {
            get { return Appearances != null && Appearances.Any(a => a.InConferenceState); }
        }

        /// <summary>
        /// Number of appearances on this line currently part of a local conference.
        /// </summary>
        public int ConferenceCount
        {
            get { return Appearances == null ? 0 : Appearances.Count(a => a.InConferenceState); }
        }

        /// <summary>
        /// Joins a call appearance into the line's local conference. When <paramref name="callAppearance"/>
        /// is zero, all currently off-hook appearances are joined; otherwise only the specified appearance
        /// is joined. Conferencing is supported on VoIP lines only.
        /// </summary>
        /// <param name="callAppearance">1-based call appearance number, or 0 to conference all active appearances.</param>
        public void Conference(int callAppearance = 0)
        {
            if (!IsVoip)
            {
                this.LogDebug("Conference ignored - line {line} is not a VoIP line", LineNumber);
                return;
            }

            if (callAppearance <= 0)
            {
                ConferenceAllActive();
                return;
            }

            var appearance = Appearances.FirstOrDefault(a => a.AppearanceNumber == callAppearance);
            if (appearance != null)
                appearance.Conference();
            else
                this.LogDebug("Conference ignored - appearance {n} not found on line {line}", callAppearance, LineNumber);
        }

        /// <summary>
        /// Joins all currently off-hook (active or held) appearances on this line into a single local
        /// conference. Conferencing is supported on VoIP lines only; the device enforces its own limit
        /// on the number of appearances that can be conferenced.
        /// </summary>
        public void ConferenceAllActive()
        {
            if (!IsVoip)
            {
                this.LogDebug("ConferenceAllActive ignored - line {line} is not a VoIP line", LineNumber);
                return;
            }

            foreach (var appearance in Appearances.Where(a => a.OffHookStatus && !a.InConferenceState))
            {
                appearance.Conference();
            }
        }

        /// <summary>
        /// Removes a call appearance from the line's local conference. When <paramref name="callAppearance"/>
        /// is zero, every conferenced appearance is removed; otherwise only the specified appearance is
        /// removed. Conferencing is supported on VoIP lines only.
        /// </summary>
        /// <param name="callAppearance">1-based call appearance number, or 0 to remove all conferenced appearances.</param>
        public void LeaveConference(int callAppearance = 0)
        {
            if (!IsVoip)
            {
                this.LogDebug("LeaveConference ignored - line {line} is not a VoIP line", LineNumber);
                return;
            }

            if (callAppearance > 0)
            {
                var target = Appearances.FirstOrDefault(a => a.AppearanceNumber == callAppearance);
                if (target != null)
                    target.LeaveConference();
                else
                    this.LogDebug("LeaveConference ignored - appearance {n} not found on line {line}", callAppearance, LineNumber);
                return;
            }

            foreach (var appearance in Appearances.Where(a => a.InConferenceState))
            {
                appearance.LeaveConference();
            }
        }

        /// <summary>
        /// Joins the appearance associated with the supplied active call into the local conference.
        /// Implements <see cref="IJoinCalls"/>.
        /// </summary>
        /// <param name="activeCall">Active call to join into the conference.</param>
        public void JoinCall(CodecActiveCallItem activeCall)
        {
            var appearance = FindAppearance(activeCall);
            if (appearance != null)
                appearance.Conference();
            else
                ConferenceAllActive();
        }

        /// <summary>
        /// Joins all active appearances on this line into the local conference. Implements
        /// <see cref="IJoinCalls"/>.
        /// </summary>
        public void JoinAllCalls()
        {
            ConferenceAllActive();
        }

        #endregion

        /// <summary>
        /// Registers mobile-control messengers for this line. VoIP lines additionally register a
        /// conference messenger so conference state and control are available to mobile UIs.
        /// </summary>
        protected override void CreateMobileControlMessengers()
        {
            var mc = DeviceManager.AllDevices.OfType<IMobileControl>().FirstOrDefault();

            if (mc == null)
            {
                this.LogInformation("Mobile Control not found");
                base.CreateMobileControlMessengers();
                return;
            }

            if (IsVoip)
            {
                var conferenceMessenger = new DialerConferenceMessenger($"{Key}-ConferenceMessenger", $"/device/{Key}", this);
                mc.AddDeviceMessenger(conferenceMessenger);
            }

            var controlMessenger = new DialerControlMessenger($"{Key}-ControlMessenger", $"/device/{Key}", this);
            mc.AddDeviceMessenger(controlMessenger);

            base.CreateMobileControlMessengers();
        }

        #region Subscriptions

        /// <summary>
        /// Subscribe to all line data.
        /// </summary>
        public override void Subscribe()
        {
            if (IsVoip)
            {
                DialerCustomName = string.Format("{0}__VoIPDialer{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                AutoAnswerCustomName = string.Format("{0}__VoIPDialerAutoAnswer{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                ControlStatusCustomName = string.Format("{0}__VoIPControl{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                LastDialedCustomName = string.Format("{0}__VoIPLastNumber{1}", InstanceTag1, Index1).Replace(" ", string.Empty);

                AddCustomName(ControlStatusCustomName);
                SendSubscriptionCommand(ControlStatusCustomName, "callState", 250, 2);

                AddCustomName(AutoAnswerCustomName);
                SendSubscriptionCommand(AutoAnswerCustomName, "autoAnswer", 500, 1);

                AddCustomName(LastDialedCustomName);
                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);

                if (!string.IsNullOrEmpty(InstanceTag2))
                {
                    LineReadyCustomName = string.Format("{0}__VoIPLineReady{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                    AddCustomName(LineReadyCustomName);
                    SendSubscriptionCommand(LineReadyCustomName, "lineReady", 500, 2);
                }

                SendFullCommand("get", "dndEnable", null, 1);
            }
            else
            {
                PotsDialerCustomName = string.Format("{0}__PotsDialer{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                LastDialedCustomName = string.Format("{0}__PotsLastNumber{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                HookStateCustomName = string.Format("{0}__HookState{1}", InstanceTag1, Index1).Replace(" ", string.Empty);

                SendSubscriptionCommand(PotsDialerCustomName, "callState", 250, 1);
                AddCustomName(PotsDialerCustomName);

                SendSubscriptionCommand(HookStateCustomName, "hookState", 500, 2);
                AddCustomName(HookStateCustomName);

                if (!string.IsNullOrEmpty(InstanceTag2))
                {
                    DialToneCustomName = string.Format("{0}__PotsDialTone{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                    AddCustomName(DialToneCustomName);
                    SendSubscriptionCommand(DialToneCustomName, "dialToneDetected", 500, 2);

                    BusyToneCustomName = string.Format("{0}__PotsBusyTone{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                    AddCustomName(BusyToneCustomName);
                    SendSubscriptionCommand(BusyToneCustomName, "busyToneDetected", 500, 2);

                    RingBackToneCustomName = string.Format("{0}__PotsRingBackTone{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                    AddCustomName(RingBackToneCustomName);
                    SendSubscriptionCommand(RingBackToneCustomName, "ringBackToneDetected", 500, 2);

                    RingDetectedCustomName = string.Format("{0}__PotsRinging{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                    AddCustomName(RingDetectedCustomName);
                    SendSubscriptionCommand(RingDetectedCustomName, "ringing", 500, 2);

                    DialingCustomName = string.Format("{0}__PotsDialing{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                    AddCustomName(DialingCustomName);
                    SendSubscriptionCommand(DialingCustomName, "dialing", 500, 2);
                }

                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);
                AddCustomName(LastDialedCustomName);

                SendFullCommand("get", "autoAnswer", null, 1);
            }

            ApplyConfiguredAttributes();

            // Poll the phonebook once on subscribe. Recurring polling is available via DoPoll but is
            // intentionally not started automatically.
            if (_phonebookEnabled)
            {
                PollPhonebook();
            }
        }

        /// <summary>
        /// Pushes optional device attributes from configuration (redialEnable, autoAnswerRingCount)
        /// to the control/status block on subscribe. Both attributes live on InstanceTag2.
        /// </summary>
        private void ApplyConfiguredAttributes()
        {
            if (string.IsNullOrEmpty(InstanceTag2)) return;

            // redialEnable is a VoIP-only attribute.
            if (IsVoip && _redialEnable.HasValue)
            {
                SendFullCommand("set", "redialEnable", _redialEnable.Value ? "true" : "false", 2);
            }

            if (!string.IsNullOrEmpty(_autoAnswerRingCount))
            {
                SendFullCommand("set", "autoAnswerRingCount", _autoAnswerRingCount, 2);
            }
        }

        /// <summary>
        /// Unsubscribe from all line data.
        /// </summary>
        public override void Unsubscribe()
        {
            if (IsVoip)
            {
                VoipIsSubscribed = false;
                AutoAnswerIsSubscribed = false;

                DialerCustomName = string.Format("{0}__VoIPDialer{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                AutoAnswerCustomName = string.Format("{0}__VoIPDialerAutoAnswer{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                ControlStatusCustomName = string.Format("{0}__VoIPControl{1}", InstanceTag2, Index1).Replace(" ", string.Empty);
                LastDialedCustomName = string.Format("{0}__VoIPLastNumber{1}", InstanceTag1, Index1).Replace(" ", string.Empty);

                SendUnSubscriptionCommand(ControlStatusCustomName, "callState", 2);
                SendUnSubscriptionCommand(AutoAnswerCustomName, "autoAnswer", 1);
                SendUnSubscriptionCommand(LastDialedCustomName, "lastNum", 1);

                if (!string.IsNullOrEmpty(LineReadyCustomName))
                    SendUnSubscriptionCommand(LineReadyCustomName, "lineReady", 2);
            }
            else
            {
                DialerCustomName = string.Format("{0}__PotsDialer{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                LastDialedCustomName = string.Format("{0}__PotsLastNumber{1}", InstanceTag1, Index1).Replace(" ", string.Empty);
                HookStateCustomName = string.Format("{0}__HookState{1}", InstanceTag1, Index1).Replace(" ", string.Empty);

                SendUnSubscriptionCommand(DialerCustomName, "callState", 2);
                SendUnSubscriptionCommand(HookStateCustomName, "hookState", 2);
                SendUnSubscriptionCommand(LastDialedCustomName, "lastNum", 2);

                if (!string.IsNullOrEmpty(DialToneCustomName))
                    SendUnSubscriptionCommand(DialToneCustomName, "dialToneDetected", 2);
                if (!string.IsNullOrEmpty(BusyToneCustomName))
                    SendUnSubscriptionCommand(BusyToneCustomName, "busyToneDetected", 2);
                if (!string.IsNullOrEmpty(RingBackToneCustomName))
                    SendUnSubscriptionCommand(RingBackToneCustomName, "ringBackToneDetected", 2);
                if (!string.IsNullOrEmpty(RingDetectedCustomName))
                    SendUnSubscriptionCommand(RingDetectedCustomName, "ringing", 2);
                if (!string.IsNullOrEmpty(DialingCustomName))
                    SendUnSubscriptionCommand(DialingCustomName, "dialing", 2);
            }
        }

        #endregion

        #region Parsing

        //Pulls Entire Value "array" and seperates call appearances
        private const string appearancePattern = "\\[([^\\[\\]]+)\\]";
        private static readonly Regex appearanceRegex = new Regex(appearancePattern);
        //Seperates each call appearance into their constituent parts
        private const string parseAppearancePattern = "\\[(?<state>\\d+)\\s+(?<line>\\d+)\\s+(?<call>\\d+)\\s+(?<action>\\d+)\\s+(?<cid>\".+\"|\"\")\\s+(?<prompt>\\d+)\\]";
        private static readonly Regex parseAppearanceRegex = new Regex(parseAppearancePattern);
        //Pulls CallerID Data
        private const string callerIdPattern = "(?:(?:\\\\\"(?<time>.*)\\\\\")(?:\\\\\"(?<number>.*)\\\\\")(?:\\\\\"(?<name>.*)\\\\\"))|\"\"";
        private static readonly Regex callerIdRegex = new Regex(callerIdPattern);

        /// <summary>
        /// Parses incoming subscription-related messages directed to this line.
        /// </summary>
        /// <param name="customName">CustomName of subscribed control.</param>
        /// <param name="value">Data to be parsed.</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
            try
            {
                if (customName == ControlStatusCustomName || customName == PotsDialerCustomName)
                {
                    var matches = appearanceRegex.Matches(value);
                    this.LogVerbose("Call States - {callStates}", value);

                    foreach (var appearance in Appearances)
                    {
                        // POTS has no real call appearances: the device always reports its single
                        // call on appearance slot 0. VoIP reports each appearance, offset by line.
                        var slot = IsVoip ? ((appearance.AppearanceNumber - 1) + ((LineNumber - 1) * 6)) : 0;
                        if (slot < 0 || slot >= matches.Count) continue;
                        ProcessAppearanceState(appearance, matches[slot].Value);
                    }
                }
            }
            catch (Exception e)
            {
                this.LogError("Error in ParseSubscriptionMessage - {message}", e.Message);
                this.LogDebug(e, "Stack Trace: ");
            }

            if (customName == AutoAnswerCustomName)
            {
                AutoAnswerState = bool.Parse(value);
                AutoAnswerIsSubscribed = true;
                AutoAnswerFeedback.FireUpdate();
            }

            if (customName == HookStateCustomName)
            {
                var primary = Appearances.FirstOrDefault();
                if (primary != null)
                {
                    if (value.IndexOf("OFF", StringComparison.Ordinal) > -1)
                        primary.SetOffHookFromHookState(true);
                    if (value.IndexOf("ON", StringComparison.Ordinal) > -1)
                        primary.SetOffHookFromHookState(false);
                }
            }

            if (customName == LineReadyCustomName)
            {
                LineReady = ParseProgressBool(value);
                LineReadyFeedback.FireUpdate();
                return;
            }

            if (customName == DialToneCustomName)
            {
                DialToneDetected = ParseProgressBool(value);
                DialToneDetectedFeedback.FireUpdate();
                return;
            }

            if (customName == BusyToneCustomName)
            {
                BusyToneDetected = ParseProgressBool(value);
                BusyToneDetectedFeedback.FireUpdate();
                return;
            }

            if (customName == RingBackToneCustomName)
            {
                RingBackToneDetected = ParseProgressBool(value);
                RingBackToneDetectedFeedback.FireUpdate();
                return;
            }

            if (customName == RingDetectedCustomName)
            {
                RingDetected = ParseProgressBool(value);
                RingDetectedFeedback.FireUpdate();
                return;
            }

            if (customName == DialingCustomName)
            {
                Dialing = ParseProgressBool(value);
                DialingFeedback.FireUpdate();
                return;
            }

            if (customName != LastDialedCustomName) return;
            LastDialed = value;
            LastDialedFeedback.FireUpdate();
        }

        /// <summary>
        /// Parses a boolean call-progress subscription value, tolerating surrounding quotes/whitespace.
        /// </summary>
        private static bool ParseProgressBool(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            bool result;
            return bool.TryParse(value.Trim().Trim('"'), out result) && result;
        }

        private void ProcessAppearanceState(TesiraCallAppearance appearance, string appearanceValue)
        {
            var match = parseAppearanceRegex.Match(appearanceValue);
            if (!match.Success) return;

            this.LogVerbose("Appearance {appearance} Subscribed Response = {response}", appearance.AppearanceNumber, appearanceValue);

            var callStatusInt = int.Parse(match.Groups["state"].Value);

            if (IsVoip)
            {
                appearance.CallStatusEnum = (ECallStatus)callStatusInt;
            }
            else
            {
                switch (callStatusInt)
                {
                    case 1: appearance.CallStatusEnum = ECallStatus.IDLE; break;
                    case 2: appearance.CallStatusEnum = ECallStatus.DIALING; break;
                    case 3: appearance.CallStatusEnum = ECallStatus.RINGBACK; break;
                    case 4: appearance.CallStatusEnum = ECallStatus.BUSY; break;
                    case 5: appearance.CallStatusEnum = ECallStatus.REJECT; break;
                    case 6: appearance.CallStatusEnum = ECallStatus.ACTIVE; break;
                    case 7: appearance.CallStatusEnum = ECallStatus.RINGING; break;
                    case 8: appearance.CallStatusEnum = ECallStatus.REJECT; break;
                    case 12: appearance.CallStatusEnum = ECallStatus.INIT; break;
                    case 13: appearance.CallStatusEnum = ECallStatus.FAULT; break;
                    case 14: appearance.CallStatusEnum = ECallStatus.SILENT; break;
                    default: return;
                }
            }

            var cidMatch = callerIdRegex.Match(match.Groups["cid"].Value);
            if (cidMatch.Success)
            {
                appearance.CallerIdNumber = cidMatch.Groups["number"].Value;
                appearance.CallerIdName = cidMatch.Groups["name"].Value;
                appearance.CallerIdTimestamp = cidMatch.Groups["time"].Value;
                appearance.ActiveCall.Name = appearance.CallerIdName;
                appearance.ActiveCall.Number = appearance.CallerIdNumber;
            }
            else
            {
                appearance.ActiveCall.Name = "";
                appearance.ActiveCall.Number = "";
            }

            if (IsVoip)
                VoipIsSubscribed = true;
            else
                PotsIsSubscribed = true;
        }

        private const string messagePattern = "[^ ]* (.*)";
        private static readonly Regex messageRegex = new Regex(messagePattern);

        /// <summary>
        /// Parses any subscription-unrelated messages directed to this line.
        /// </summary>
        /// <param name="attributeCode">Message attribute code to determine parsing algorithm.</param>
        /// <param name="message">Data to be parsed.</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                this.LogVerbose("Parsing Message - {message} AttributeCode: {attributeCode}", message, attributeCode);

                var match = messageRegex.Match(message);
                if (!match.Success) return;
                var value = match.Groups[1].Value;

                this.LogDebug("Response: {attributeCode} Value: {value}", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;

                // Phonebook (speed-dial) get responses encode the entry index in the attribute code,
                // e.g. "speedDialLabel 3" / "speedDialNum 3".
                if (attributeCode.StartsWith("speedDial", StringComparison.Ordinal))
                {
                    ParsePhonebookResponse(attributeCode, value);
                    return;
                }

                switch (attributeCode)
                {
                    case "autoAnswer":
                        AutoAnswerState = bool.Parse(value);
                        this.LogDebug("AutoAnswerState: {autoAnswerState}", AutoAnswerState);
                        AutoAnswerFeedback.FireUpdate();
                        break;
                    case "dndEnable":
                        DoNotDisturbState = bool.Parse(value);
                        this.LogDebug("DoNotDisturbState: {doNotDisturbState}", DoNotDisturbState);
                        DoNotDisturbFeedback.FireUpdate();
                        break;
                    default:
                        this.LogDebug("Response does not match expected attribute codes: {message}", message);
                        break;
                }
            }
            catch (Exception e)
            {
                this.LogError("Unable to parse message {message}: {exception}", message, e.Message);
                this.LogDebug(e, "Stack Trace: ");
            }
        }

        #endregion

        #region Phonebook implementation

        /// <summary>
        /// Polls the device for all speed-dial entries on this line. Recurring polling is intentionally
        /// not started automatically; call this method (or <see cref="DoPoll"/>) to refresh on demand.
        /// </summary>
        public void PollPhonebook()
        {
            if (!_phonebookEnabled || _phonebookEntryCount <= 0) return;

            _phonebookBuffer = new List<CodecPhonebookEntry>();
            for (var i = 0; i < _phonebookEntryCount; i++)
            {
                _phonebookBuffer.Add(new CodecPhonebookEntry { Name = string.Empty, Number = string.Empty });
            }

            _pendingPhonebookResponses = _phonebookEntryCount * 2;

            for (var entry = 1; entry <= _phonebookEntryCount; entry++)
            {
                EnqueuePhonebookGet("speedDialLabel", entry);
                EnqueuePhonebookGet("speedDialNum", entry);
            }
        }

        /// <summary>
        /// Refreshes the phonebook on demand. No recurring timer is started by this plugin.
        /// </summary>
        public override void DoPoll()
        {
            PollPhonebook();
        }

        /// <summary>
        /// Sets a speed-dial entry (0-based index) and re-reads it from the device.
        /// </summary>
        /// <param name="index">Zero-based entry index.</param>
        /// <param name="name">Entry label.</param>
        /// <param name="number">Entry number.</param>
        public void SetPhonebookEntry(int index, string name, string number)
        {
            if (!_phonebookEnabled)
            {
                this.LogDebug("SetPhonebookEntry ignored - phonebook not enabled for line {line}", LineNumber);
                return;
            }
            if (index < 0 || index >= _phonebookEntryCount)
            {
                this.LogDebug("SetPhonebookEntry ignored - index {index} out of range (0..{max})", index, _phonebookEntryCount - 1);
                return;
            }

            var entry = index + 1;
            EnqueueRawCommand(string.Format("{0} set speedDialLabel {1} {2} \"{3}\"", InstanceTag1, LineNumber, entry, name ?? string.Empty));
            EnqueueRawCommand(string.Format("{0} set speedDialNum {1} {2} \"{3}\"", InstanceTag1, LineNumber, entry, number ?? string.Empty));

            if (_phonebookBuffer == null)
            {
                PollPhonebook();
                return;
            }

            _pendingPhonebookResponses += 2;
            EnqueuePhonebookGet("speedDialLabel", entry);
            EnqueuePhonebookGet("speedDialNum", entry);
        }

        /// <summary>
        /// Dials the speed-dial entry at the specified 0-based index, if it has a number assigned.
        /// </summary>
        /// <param name="index">Zero-based entry index.</param>
        public void DialPhonebookEntry(int index)
        {
            if (!_phonebookEnabled)
            {
                this.LogDebug("DialPhonebookEntry ignored - phonebook not enabled for line {line}", LineNumber);
                return;
            }
            if (index < 0 || index >= _phonebookEntryCount)
            {
                this.LogDebug("DialPhonebookEntry ignored - index {index} out of range (0..{max})", index, _phonebookEntryCount - 1);
                return;
            }

            // Dial the device-stored speed-dial entry directly by index. This does not depend on the
            // phonebook having been read first - the device dials its own stored number for the entry.
            var entry = index + 1;
            var appearance = GetAvailableAppearance();
            var appearanceNumber = appearance != null ? appearance.AppearanceNumber : 1;

            EnqueueRawCommand(string.Format("{0} speedDial {1} {2} {3}", InstanceTag1, LineNumber, appearanceNumber, entry));
        }

        private void EnqueuePhonebookGet(string attribute, int entry)
        {
            var cmd = string.Format("{0} get {1} {2} {3}", InstanceTag1, attribute, LineNumber, entry);
            var attributeCode = string.Format("{0} {1}", attribute, entry);
            Parent.CommandQueue.EnqueueCommand(new QueuedCommand(cmd, attributeCode, this, priority: (int)CommandPriority.Critical));
        }

        private void EnqueueRawCommand(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return;
            Parent.CommandQueue.EnqueueCommand(cmd, priority: (int)CommandPriority.Critical);
        }

        private void ParsePhonebookResponse(string attributeCode, string value)
        {
            var parts = attributeCode.Split(' ');
            if (parts.Length < 2) return;

            int entry;
            if (!int.TryParse(parts[1], out entry)) return;

            var index = entry - 1;
            if (_phonebookBuffer == null || index < 0 || index >= _phonebookBuffer.Count) return;

            var parsed = StripValue(value);
            if (parts[0] == "speedDialLabel")
                _phonebookBuffer[index].Name = parsed;
            else if (parts[0] == "speedDialNum")
                _phonebookBuffer[index].Number = parsed;

            if (_pendingPhonebookResponses > 0)
                _pendingPhonebookResponses--;

            if (_pendingPhonebookResponses <= 0)
                PublishPhonebook();
        }

        private void PublishPhonebook()
        {
            if (_phonebookBuffer == null) return;

            var newList = _phonebookBuffer
                .Select(x => new CodecPhonebookEntry { Name = x.Name, Number = x.Number })
                .ToList();

            _phonebookEntries = newList;

            var handler = ListChanged;
            if (handler != null)
                handler(this, new PhonebookListChangedEventArgs(newList));
        }

        private static string StripValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var v = raw.Trim();
            const string prefix = "\"value\":";
            var idx = v.IndexOf(prefix, StringComparison.Ordinal);
            if (idx >= 0)
                v = v.Substring(idx + prefix.Length).Trim();

            if (v.Length >= 2 && v.StartsWith("\"", StringComparison.Ordinal) && v.EndsWith("\"", StringComparison.Ordinal))
                v = v.Substring(1, v.Length - 2);

            return v;
        }

        #endregion

        #region AudioCodecBase / IHasDialer overrides

        /// <summary>
        /// Dials a number. Targets the specified call appearance, or the first available appearance when
        /// <paramref name="callAppearance"/> is zero.
        /// </summary>
        /// <param name="number">Number to dial.</param>
        /// <param name="callAppearance">1-based call appearance number, or 0 to use the first available appearance.</param>
        public void Dial(string number, int callAppearance = 0)
        {
            var appearance = ResolveAppearance(callAppearance, GetAvailableAppearance);
            if (appearance != null)
                appearance.Dial(number);
        }

        /// <inheritdoc />
        public override void Dial(string number)
        {
            Dial(number, 0);
        }

        /// <summary>
        /// Ends a specific call by its active-call item.
        /// </summary>
        /// <param name="activeCall">Active call to end.</param>
        public override void EndCall(CodecActiveCallItem activeCall)
        {
            var appearance = FindAppearance(activeCall);
            if (appearance != null)
                appearance.End();
            else
                EndAllCalls();
        }

        /// <summary>
        /// Ends all calls on the line.
        /// </summary>
        public override void EndAllCalls()
        {
            foreach (var appearance in Appearances)
                appearance.End();
        }

        /// <summary>
        /// Ends a call. Targets the specified call appearance, or ends all calls on the line when
        /// <paramref name="callAppearance"/> is zero.
        /// </summary>
        /// <param name="callAppearance">1-based call appearance number, or 0 to end all calls.</param>
        public void EndCall(int callAppearance = 0)
        {
            if (callAppearance <= 0)
            {
                EndAllCalls();
                return;
            }

            var appearance = Appearances.FirstOrDefault(a => a.AppearanceNumber == callAppearance);
            if (appearance != null)
                appearance.End();
            else
                this.LogDebug("EndCall ignored - appearance {n} not found on line {line}", callAppearance, LineNumber);
        }

        /// <summary>
        /// Holds a call. Targets the specified call appearance, or the first off-hook appearance when
        /// <paramref name="callAppearance"/> is zero.
        /// </summary>
        /// <param name="callAppearance">1-based call appearance number, or 0 to use the first off-hook appearance.</param>
        public void Hold(int callAppearance = 0)
        {
            var appearance = ResolveAppearance(callAppearance, () => Appearances.FirstOrDefault(a => a.OffHookStatus));
            if (appearance != null)
                appearance.HoldCall();
        }

        /// <summary>
        /// Resumes a held call. Targets the specified call appearance, or the first held appearance when
        /// <paramref name="callAppearance"/> is zero.
        /// </summary>
        /// <param name="callAppearance">1-based call appearance number, or 0 to use the first held appearance.</param>
        public void Resume(int callAppearance = 0)
        {
            var appearance = ResolveAppearance(callAppearance, () => Appearances.FirstOrDefault(a => a.HoldCallFeedback.BoolValue));
            if (appearance != null)
                appearance.ResumeCall();
        }

        /// <summary>
        /// Redials the last number dialed. Targets the specified call appearance, or the first available
        /// appearance when <paramref name="callAppearance"/> is zero.
        /// </summary>
        /// <param name="callAppearance">1-based call appearance number, or 0 to use the first available appearance.</param>
        public void Redial(int callAppearance = 0)
        {
            var appearance = ResolveAppearance(callAppearance, GetAvailableAppearance);
            if (appearance != null)
                appearance.Redial();
            else
                this.LogDebug("Redial ignored - no appearance available on line {line}", LineNumber);
        }

        /// <summary>
        /// Sends a hook-flash on the line. Hook-flash is an analog (POTS) feature and is ignored on VoIP
        /// lines. POTS lines have a single call appearance, so flash is a line-level operation.
        /// </summary>
        public void Flash()
        {
            if (IsVoip)
            {
                this.LogDebug("Flash ignored - line {line} is a VoIP line", LineNumber);
                return;
            }

            var appearance = Appearances.FirstOrDefault();
            if (appearance != null)
                appearance.Flash();
            else
                this.LogDebug("Flash ignored - no appearance available on line {line}", LineNumber);
        }

        /// <summary>
        /// Accepts an incoming call.
        /// </summary>
        /// <param name="item">Active call to accept.</param>
        public override void AcceptCall(CodecActiveCallItem item)
        {
            var appearance = FindAppearance(item) ?? GetIncomingAppearance();
            if (appearance != null)
                appearance.Answer();
        }

        /// <summary>
        /// Rejects an incoming call.
        /// </summary>
        /// <param name="item">Active call to reject.</param>
        public override void RejectCall(CodecActiveCallItem item)
        {
            var appearance = FindAppearance(item) ?? GetIncomingAppearance();
            if (appearance != null)
                appearance.End();
        }

        /// <summary>
        /// Sends a DTMF digit to the currently off-hook appearance.
        /// </summary>
        /// <param name="digit">Digit to send.</param>
        public override void SendDtmf(string digit)
        {
            var appearance = Appearances.FirstOrDefault(a => a.OffHookStatus) ?? Appearances.FirstOrDefault();
            if (appearance != null)
                appearance.SendDtmf(digit);
        }

        private TesiraCallAppearance FindAppearance(CodecActiveCallItem item)
        {
            if (item == null) return null;
            return Appearances.FirstOrDefault(a => ReferenceEquals(a.ActiveCall, item))
                ?? Appearances.FirstOrDefault(a => a.ActiveCall.Id == item.Id);
        }

        private TesiraCallAppearance GetAvailableAppearance()
        {
            return Appearances.FirstOrDefault(a => !a.OffHookStatus) ?? Appearances.FirstOrDefault();
        }

        private TesiraCallAppearance GetIncomingAppearance()
        {
            return Appearances.FirstOrDefault(a => a.IncomingCallState) ?? Appearances.FirstOrDefault();
        }

        private TesiraCallAppearance ResolveAppearance(int callAppearance, Func<TesiraCallAppearance> defaultSelector)
        {
            if (callAppearance > 0)
            {
                var match = Appearances.FirstOrDefault(a => a.AppearanceNumber == callAppearance);
                if (match != null)
                    return match;
                this.LogDebug("Call appearance {n} not found on line {line}; using default selection", callAppearance, LineNumber);
            }
            return defaultSelector();
        }

        #endregion

        #region Line-level controls

        /// <summary>Enable Auto Answer for the line.</summary>
        public void AutoAnswerOn()
        {
            SendFullCommand("set", "autoAnswer", "true", 1);
            if (IsVoip) return;
            SendFullCommand("get", "autoAnswer", null, 1);
        }

        /// <summary>Disable Auto Answer for the line.</summary>
        public void AutoAnswerOff()
        {
            SendFullCommand("set", "autoAnswer", "false", 1);
            if (IsVoip) return;
            SendFullCommand("get", "autoAnswer", null, 1);
        }

        /// <summary>Toggle Auto Answer for the line.</summary>
        public void AutoAnswerToggle()
        {
            SendFullCommand("toggle", "autoAnswer", null, 1);
            if (IsVoip) return;
            SendFullCommand("get", "autoAnswer", null, 1);
        }

        /// <summary>Enable Do Not Disturb for the line.</summary>
        public void DoNotDisturbOn()
        {
            SendFullCommand("set", "dndEnable", "true", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }

        /// <summary>Disable Do Not Disturb for the line.</summary>
        public void DoNotDisturbOff()
        {
            SendFullCommand("set", "dndEnable", "false", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }

        /// <summary>Toggle Do Not Disturb for the line.</summary>
        public void DoNotDisturbToggle()
        {
            SendFullCommand("set", "dndEnable", DoNotDisturbState ? "false" : "true", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }

        #endregion

        /// <summary>
        /// Standalone bridge link. Wires line-level controls plus the primary appearance's call controls.
        /// </summary>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraDialerJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraDialerJoinMapAdvancedStandalone>(joinMapSerialized);

            bridge?.AddJoinMap(Key, joinMap);

            this.LogVerbose("Adding Dialer {dialerKey}", Key);

            var primary = Appearances.FirstOrDefault();
            if (primary == null)
            {
                this.LogError("No call appearances configured for dialer line {key}", Key);
                return;
            }

            for (var i = 0; i < joinMap.KeyPadNumeric.JoinSpan; i++)
            {
                var keyNumber = i;
                trilist.SetSigTrueAction(joinMap.KeyPadNumeric.JoinNumber + (uint)keyNumber, () => primary.SendKeypad((EKeypadKeys)keyNumber));
            }

            trilist.SetSigTrueAction(joinMap.KeyPadStar.JoinNumber, () => primary.SendKeypad(EKeypadKeys.Star));
            trilist.SetSigTrueAction(joinMap.KeyPadPound.JoinNumber, () => primary.SendKeypad(EKeypadKeys.Pound));
            trilist.SetSigTrueAction(joinMap.KeyPadClear.JoinNumber, () => primary.SendKeypad(EKeypadKeys.Clear));
            trilist.SetSigTrueAction(joinMap.KeyPadBackspace.JoinNumber, () => primary.SendKeypad(EKeypadKeys.Backspace));

            trilist.SetSigTrueAction(joinMap.KeyPadDial.JoinNumber, primary.Dial);
            trilist.SetSigTrueAction(joinMap.DoNotDisturbToggle.JoinNumber, DoNotDisturbToggle);
            trilist.SetSigTrueAction(joinMap.DoNotDisturbOn.JoinNumber, DoNotDisturbOn);
            trilist.SetSigTrueAction(joinMap.DoNotDisturbOff.JoinNumber, DoNotDisturbOff);
            trilist.SetSigTrueAction(joinMap.AutoAnswerToggle.JoinNumber, AutoAnswerToggle);
            trilist.SetSigTrueAction(joinMap.AutoAnswerOn.JoinNumber, AutoAnswerOn);
            trilist.SetSigTrueAction(joinMap.AutoAnswerOff.JoinNumber, AutoAnswerOff);
            trilist.SetSigTrueAction(joinMap.Answer.JoinNumber, primary.Answer);
            trilist.SetSigTrueAction(joinMap.EndCall.JoinNumber, EndAllCalls);
            trilist.SetSigTrueAction(joinMap.OnHook.JoinNumber, primary.OnHook);
            trilist.SetSigTrueAction(joinMap.OffHook.JoinNumber, primary.OffHook);

            trilist.SetSigTrueAction(joinMap.HoldCall.JoinNumber, primary.HoldCall);
            trilist.SetSigTrueAction(joinMap.ResumeCall.JoinNumber, primary.ResumeCall);
            trilist.SetSigTrueAction(joinMap.HoldToggle.JoinNumber, primary.HoldToggle);

            trilist.SetStringSigAction(joinMap.DialString.JoinNumber, primary.SetDialString);

            DisplayNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.DisplayNumber.JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);

            DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbToggle.JoinNumber]);
            DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOn.JoinNumber]);
            DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOff.JoinNumber]);

            primary.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.KeyPadDial.JoinNumber]);
            primary.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.OffHook.JoinNumber]);
            primary.OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.OnHook.JoinNumber]);
            primary.IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IncomingCall.JoinNumber]);

            AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerToggle.JoinNumber]);
            AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerOn.JoinNumber]);
            AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoAnswerOff.JoinNumber]);

            primary.DialStringFeedback.LinkInputSig(trilist.StringInput[joinMap.DialString.JoinNumber]);
            primary.CallerIdNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.CallerIdNumberFb.JoinNumber]);
            primary.CallerIdNameFeedback.LinkInputSig(trilist.StringInput[joinMap.CallerIdNameFb.JoinNumber]);
            LastDialedFeedback.LinkInputSig(trilist.StringInput[joinMap.LastNumberDialerFb.JoinNumber]);

            primary.HoldCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.HoldCall.JoinNumber]);
            primary.HoldCallFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.ResumeCall.JoinNumber]);
            primary.HoldCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.HoldToggle.JoinNumber]);

            primary.CallStateFeedback.LinkInputSig(trilist.UShortInput[joinMap.CallState.JoinNumber]);

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }
            };
        }
    }
}
