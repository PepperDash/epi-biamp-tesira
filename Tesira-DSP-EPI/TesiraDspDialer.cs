using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Devices.Common.Codec;
using System.Text.RegularExpressions;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Tesira_DSP_EPI {
    public class TesiraDspDialer : TesiraDspDialerControlPoint, IBridgeAdvanced
    {

        public FeedbackCollection<Feedback> Feedbacks; 

        private bool IsVoip { get; set; }
		private string _dialString;
		public string DialString
		{
			get
			{
				return _dialString;
			}
			set
			{
				_dialString = value;
				DialStringFeedback.FireUpdate();
			}
		}

        private bool _offHookStatus;

        /// <summary>
        /// Current Hook Status for component
        /// </summary>
        public bool OffHookStatus
        {
            get {
                return _offHookStatus;
            }
            protected set {
                _offHookStatus = value;
                Debug.Console(2, this, "_OffHookStatus = {0}", value.ToString());
                OffHookFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Last Dialed Number for component
        /// </summary>
        public string LastDialed { get; protected set; }

        /// <summary>
        /// CallApperance for component
        /// </summary>
        public int CallAppearance { get; protected set; }

        private bool AppendDtmf { get; set; }
        private bool ClearOnHangup { get; set; }


        /// <summary>
        /// Number to be displayed on remote callerId for component
        /// </summary>
        public string DisplayNumber { get; protected set; }

        /// <summary>
        /// Current Do Not Disturb State for component
        /// </summary>
        public bool DoNotDisturbState { get; protected set; }
        /// <summary>
        /// Current Auto Answer State for component
        /// </summary>
        public bool AutoAnswerState { get; protected set; }
        /// <summary>
        /// Dialer Subscription Identifier for Component
        /// </summary>
        public string DialerCustomName { get; protected set; }

        /// <summary>
        /// ControlStatus Subscription Identifier for Component
        /// </summary>
        public string ControlStatusCustomName { get; protected set; }

        /// <summary>
        /// AutoAnswer Subscription Identifier for Component
        /// </summary>
        public string AutoAnswerCustomName { get; protected set; }

        /// <summary>
        /// HookState Subscription Identifier for Component
        /// </summary>
        public string HookStateCustomName { get; protected set; }

        /// <summary>
        /// POTS Dialer Subscription Identifier for Component
        /// </summary>
        public string PotsDialerCustomName { get; protected set; }

        /// <summary>
        /// Last Dialed Subscription Identifier for Component
        /// </summary>
        public string LastDialedCustomName { get; protected set; }

        /// <summary>
        /// Current call status of line
        /// </summary>
        public string CallStatus
        {
            get { return CallStatusEnum.ToString(); }
        }

        /// <summary>
        /// Boolean Feedback for Hook Status
        /// </summary>
        public BoolFeedback OffHookFeedback;

        /// <summary>
        /// Boolean Feedback for Auto Answer Status
        /// </summary>
        public BoolFeedback AutoAnswerFeedback;

        /// <summary>
        /// Boolean Feedback for Do Not Disturb Status
        /// </summary>
        public BoolFeedback DoNotDisturbFeedback;

        /// <summary>
        /// String Feedback for the current dial string
        /// </summary>
        public StringFeedback DialStringFeedback;

        /// <summary>
        /// String Feedback for CallerId Number for current call
        /// </summary>
        public StringFeedback CallerIdNumberFeedback;

        /// <summary>
        /// String Feedback for CallerId Name for current call
        /// </summary>
        public StringFeedback CallerIdNameFeedback;

        /// <summary>
        /// Boolean Feedback for Incoming Call Status - True when call incoming
        /// </summary>
        public BoolFeedback IncomingCallFeedback;

        /// <summary>
        /// Integer Feedback for current Call State - see eCallStatus enumeration
        /// </summary>
        public IntFeedback CallStateFeedback;

        /// <summary>
        /// String feedback for the last dialer number
        /// </summary>
        public StringFeedback LastDialedFeedback;

        /// <summary>
        /// String Feedback for component Friendly Name
        /// </summary>
        public StringFeedback NameFeedback;

        /// <summary>
        /// String feedback for the displayed number
        /// </summary>
        public StringFeedback DisplayNumberFeedback;

        /// <summary>
        /// Bool Feedback for Hold Status
        /// </summary>
        public BoolFeedback HoldCallFeedback;

        // ReSharper disable once InconsistentNaming
        private ECallStatus _CallStatusEnum { get; set; }

        public ECallStatus CallStatusEnum
        {
            get
            {
                return _CallStatusEnum;
            }
            protected set
            {
                _CallStatusEnum = value;
				if (CallStatusEnum == ECallStatus.DIAL_TONE ||
					CallStatusEnum == ECallStatus.SILENT ||
					CallStatusEnum == ECallStatus.DIALING ||
					CallStatusEnum == ECallStatus.RINGBACK ||
					CallStatusEnum == ECallStatus.BUSY ||
					CallStatusEnum == ECallStatus.ANSWERING ||
					CallStatusEnum == ECallStatus.INVALID_NUMBER ||
					CallStatusEnum == ECallStatus.ACTIVE ||
					CallStatusEnum == ECallStatus.ACTIVE_MUTED ||
					CallStatusEnum == ECallStatus.ON_HOLD ||
					CallStatusEnum == ECallStatus.WAITING_RING ||
					CallStatusEnum == ECallStatus.CONF_ACTIVE ||
					CallStatusEnum == ECallStatus.CONF_HOLD)
				{
					OffHookStatus = true;
				    var cidCmd = IsVoip ? String.Format("cid {0} {1}", Index1, CallAppearance) : "cid";
                    SendFullCommand("get", cidCmd, null, 2);
				}
				else
				{
					OffHookStatus = false;
				}
                if (value == ECallStatus.IDLE)
                {
                    if (ClearOnHangup)
                    {
                        DialString = String.Empty;
                        DialStringFeedback.FireUpdate();
                    }
                }
                CallStateFeedback.FireUpdate();
                switch (CallStatusEnum)
                {
                    case ECallStatus.INIT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.FAULT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.IDLE:
                        ActiveCalls.First().Status = eCodecCallStatus.Idle;
                        break;
                    case ECallStatus.DIAL_TONE:
                        ActiveCalls.First().Status = eCodecCallStatus.Idle;
                        break;
                    case ECallStatus.SILENT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.DIALING:
                        ActiveCalls.First().Status = eCodecCallStatus.Connecting;
                        ActiveCalls.First().Direction = eCodecCallDirection.Outgoing;
                        break;
                    case ECallStatus.RINGBACK:
                        ActiveCalls.First().Status = eCodecCallStatus.Connecting;
                        break;
                    case ECallStatus.RINGING:
                        ActiveCalls.First().Status = eCodecCallStatus.Ringing;
                        ActiveCalls.First().Direction = eCodecCallDirection.Incoming;
                        break;
                    case ECallStatus.BUSY:
                        ActiveCalls.First().Status = eCodecCallStatus.Disconnecting;
                        break;
                    case ECallStatus.REJECT:
                        ActiveCalls.First().Status = eCodecCallStatus.Disconnecting;
                        break;
                    case ECallStatus.INVALID_NUMBER:
                        ActiveCalls.First().Status = eCodecCallStatus.Disconnecting;
                        break;
                    case ECallStatus.ACTIVE:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case ECallStatus.ACTIVE_MUTED:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case ECallStatus.ON_HOLD:
                        ActiveCalls.First().Status = eCodecCallStatus.OnHold;
                        break;
                    case ECallStatus.WAITING_RING:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case ECallStatus.CONF_ACTIVE:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case ECallStatus.CONF_HOLD:
                        ActiveCalls.First().Status = eCodecCallStatus.OnHold;
                        break;
                    case ECallStatus.XFER_INIT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_Silent:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_ReqDialing:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_Process:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_ReplacesProcess:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_Active:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_RingBack:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_OnHold:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_Decision:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_InitError:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case ECallStatus.XFER_WAIT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                }
            }
        }

        /// <summary>
        /// Status of Incoming Call
        /// </summary>
        public bool IncomingCallState
        {
            get
            {
                return CallStatusEnum == ECallStatus.RINGING;
            }
        }

        /// <summary>
        /// Line Number for component
        /// </summary>
        public int LineNumber { get; protected set; }

        private string _callerIdNumber;

        /// <summary>
        /// CallerId Number
        /// </summary>
        public string CallerIdNumber
        {
            get {
                return _callerIdNumber;
            }
            protected set {
                _callerIdNumber = value;
                CallerIdNumberFeedback.FireUpdate();
            }
        }

        // ReSharper disable once InconsistentNaming
        private string _callerIdName { get; set; }

        /// <summary>
        /// CallerId Name
        /// </summary>
        public string CallerIdName {
            get {
                return _callerIdName;
            }
            protected set {
                _callerIdName = value;
                CallerIdNameFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Subscription state of component
        /// </summary>
        public override bool IsSubscribed
        {
            get
            {
                bool isSubscribed;
                if (IsVoip)
                {
                    if (VoipIsSubscribed && AutoAnswerIsSubscribed)
                        isSubscribed = true;
                    else
                        isSubscribed = false;
                }
                else if (!IsVoip)
                {
                    isSubscribed = PotsIsSubscribed;
                }
                else
                    isSubscribed = false;
                return isSubscribed;
            }
            protected set { }
        }

        private bool VoipIsSubscribed { get; set; }
        private bool AutoAnswerIsSubscribed { get; set; }
        private bool PotsIsSubscribed { get; set; }


        /// <summary>
        /// Constructor for Tesira DSP Dialer
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="config">Component Config</param>
        /// <param name="parent">Parent Device</param>
		public TesiraDspDialer(string key, TesiraDialerControlBlockConfig config, TesiraDsp parent)
            : base(key, config.DialerInstanceTag, config.ControlStatusInstanceTag, config.Index, config.CallAppearance, parent, config.BridgeIndex)
        {

            Key = string.Format("{0}--{1}", parent.Key, key);

            Feedbacks = new FeedbackCollection<Feedback>();

            DialStringFeedback = new StringFeedback(Key + "-DialStringFeedback", () => DialString);
            OffHookFeedback = new BoolFeedback(Key + "-OffHookFeedback", () => OffHookStatus);
            AutoAnswerFeedback = new BoolFeedback(Key + "-AutoAnswerFeedback", () => AutoAnswerState);
            DoNotDisturbFeedback = new BoolFeedback(Key + "-DoNotDisturbFeedback", () => DoNotDisturbState);
            CallerIdNumberFeedback = new StringFeedback(Key + "-CallerIDNumberFeedback", () => CallerIdNumber);
            CallerIdNameFeedback = new StringFeedback(Key + "-CallerIDNameFeedback", () => CallerIdName);
            IncomingCallFeedback = new BoolFeedback(Key + "-IncomingCallFeedback", () => IncomingCallState);
            CallStateFeedback = new IntFeedback(Key + "-CallStateFeedback", () => (int)CallStatusEnum);
            LastDialedFeedback = new StringFeedback(Key + "-LastDialedFeedback", () => LastDialed);
            NameFeedback = new StringFeedback(Key + "-NameFeedback", () => Name);
            DisplayNumberFeedback = new StringFeedback(Key + "-DisplayNumberFeedback", () => DisplayNumber);
            HoldCallFeedback = new BoolFeedback(Key + "-HoldCallFeedback", () => CallStatusEnum == ECallStatus.ON_HOLD);

            Feedbacks.Add(DialStringFeedback);
            Feedbacks.Add(OffHookFeedback);
            Feedbacks.Add(AutoAnswerFeedback);
            Feedbacks.Add(DoNotDisturbFeedback);
            Feedbacks.Add(CallerIdNumberFeedback);
            Feedbacks.Add(CallerIdNameFeedback);
            Feedbacks.Add(IncomingCallFeedback);
            Feedbacks.Add(CallStateFeedback);
            Feedbacks.Add(LastDialedFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(DisplayNumberFeedback);
            Feedbacks.Add(HoldCallFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);

        }

		private void Initialize(TesiraDialerControlBlockConfig config)
		{

            if (config.Enabled)
            {
                DeviceManager.AddDevice(this);
            }

            Debug.Console(2, this, "Adding Dialer '{0}'", Key);

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
            CallAppearance = config.CallAppearance;
            DisplayNumber = config.DisplayNumber;

            ActiveCalls = new List<CodecActiveCallItem>();
            var activeCall = new CodecActiveCallItem
            {
                Name = "",
                Number = "",
                Type = eCodecCallType.Audio,
                Status = eCodecCallStatus.Idle,
                Direction = eCodecCallDirection.Unknown,
                Id = Key
            };

		    ActiveCalls.Add(activeCall);
        }

        /// <summary>
        /// Accept Incoming Call
        /// </summary>
        /// <param name="item">Call Object to Accept</param>
        public override void AcceptCall(CodecActiveCallItem item) {
            SendFullCommand(null, "answer", null, 1);
        }

        /// <summary>
        /// Subscribe to all component data
        /// </summary>
        public override void Subscribe()
        {
            if (IsVoip)
            {
                DialerCustomName = (string.Format("{0}__VoIPDialer{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);
                AutoAnswerCustomName = (string.Format("{0}__VoIPDialerAutoAnswer{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);
                ControlStatusCustomName = (string.Format("{0}__VoIPControl{1}", InstanceTag2, Index1)).Replace(" ", string.Empty);
                LastDialedCustomName = (string.Format("{0}__VoIPLastNumber{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);

                AddCustomName(ControlStatusCustomName);
                SendSubscriptionCommand(ControlStatusCustomName, "callState", 250, 2);

                AddCustomName(AutoAnswerCustomName);
                SendSubscriptionCommand(AutoAnswerCustomName, "autoAnswer", 500, 1);

                AddCustomName(LastDialedCustomName);
                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);
                

                SendFullCommand("get", "dndEnable", null, 1);
            }
            else if (!IsVoip)
            {

                PotsDialerCustomName = (string.Format("{0}__PotsDialer{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);
                LastDialedCustomName = (string.Format("{0}__PotsLastNumber{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);

                HookStateCustomName = (string.Format("{0}__HookState{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);

                SendSubscriptionCommand(PotsDialerCustomName, "callState", 250, 1);
                AddCustomName(PotsDialerCustomName);

                SendSubscriptionCommand(HookStateCustomName, "hookState", 500, 2);
                AddCustomName(HookStateCustomName);

                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);
                AddCustomName(LastDialedCustomName);

                SendFullCommand("get", "autoAnswer", null, 1);
            }


        }

        /// <summary>
        /// Unsubscribe from all component data
        /// </summary>
        public override void Unsubscribe()
        {
            if (IsVoip)
            {
                VoipIsSubscribed = false;
                AutoAnswerIsSubscribed = false;

                DialerCustomName = (string.Format("{0}__VoIPDialer{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);
                AutoAnswerCustomName = (string.Format("{0}__VoIPDialerAutoAnswer{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);
                ControlStatusCustomName = (string.Format("{0}__VoIPControl{1}", InstanceTag2, Index1)).Replace(" ", string.Empty);
                LastDialedCustomName = (string.Format("{0}__VoIPLastNumber{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);


                SendUnSubscriptionCommand(ControlStatusCustomName, "callState", 2);

                SendUnSubscriptionCommand(AutoAnswerCustomName, "autoAnswer", 1);

                SendUnSubscriptionCommand(LastDialedCustomName, "lastNum", 1);
            }
            else if (!IsVoip)
            {
				DialerCustomName = (string.Format("{0}__PotsDialer{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);
				LastDialedCustomName = (string.Format("{0}__PotsLastNumber{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);

                HookStateCustomName = (string.Format("{0}__HookState{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);

				SendUnSubscriptionCommand(DialerCustomName, "callState", 2);

				SendUnSubscriptionCommand(HookStateCustomName, "hookState", 2);

				SendUnSubscriptionCommand(LastDialedCustomName, "lastNum", 2);

            }
        }

        /// <summary>
        /// Parses incoming subscription-related messages directed to this object
        /// </summary>
        /// <param name="customName">CustomName of subscribed control within the component</param>
        /// <param name="value">Data to be parsed</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
            try
            {
                Debug.Console(2, this, "New Subscription Message to Dialer");
                if (customName == ControlStatusCustomName || customName == PotsDialerCustomName)
                {
                    //Pulls Entire Value "array" and seperates call appearances
                    const string pattern1 = "\\[([^\\[\\]]+)\\]";
                    //Seperates each call appearance into their constituent parts
                    const string pattern2 = "\\[(?<state>\\d+)\\s+(?<line>\\d+)\\s+(?<call>\\d+)\\s+(?<action>\\d+)\\s+(?<cid>\".+\"|\"\")\\s+(?<prompt>\\d+)\\]";
                    //Pulls CallerID Data
                    const string pattern3 = "(?:(?:\\\\\"(?<time>.*)\\\\\")(?:\\\\\"(?<number>.*)\\\\\")(?:\\\\\"(?<name>.*)\\\\\"))|\"\"";

                    var myMatches = Regex.Matches(value, pattern1);

                    Debug.Console(2, this, "This is the list of Call States - {0}", myMatches.ToString());

                    var match = myMatches[CallAppearance - 1 + (IsVoip ? ((Index1 - 1) * 6) : 0)];
                    var match2 = Regex.Match(match.Value, pattern2);
                    if (match2.Success)
                    {
                        Debug.Console(2, this, "VoIPControlStatus Subscribed Response = {0}", match.Value);
                        var lineNumber = ushort.Parse(match2.Groups["line"].Value) + 1;
                        var callStatusInt = int.Parse(match2.Groups["state"].Value);

						if (IsVoip)
						{
							CallStatusEnum = (ECallStatus)(callStatusInt);
						}
						// Set call stauts for POTS 
						else
						{
							switch (callStatusInt)
							{
								case 1: CallStatusEnum = ECallStatus.IDLE; break;
								case 2: CallStatusEnum = ECallStatus.DIALING; break;
								case 3: CallStatusEnum = ECallStatus.RINGBACK; break;
								case 4: CallStatusEnum = ECallStatus.BUSY; break;
								case 5: CallStatusEnum = ECallStatus.REJECT; break;
								case 6: CallStatusEnum = ECallStatus.ACTIVE; break;
								case 7: CallStatusEnum = ECallStatus.RINGING; break;
								case 8: CallStatusEnum = ECallStatus.REJECT; break;
								case 12: CallStatusEnum = ECallStatus.INIT; break;
								case 13: CallStatusEnum = ECallStatus.FAULT; break;
								case 14: CallStatusEnum = ECallStatus.SILENT; break;
								//default: CallStatusEnum = eCallStatus.IDLE;


							}
						}
                        
                        Debug.Console(2, this, "Callstate for Line {0} is {1}", lineNumber, int.Parse(match2.Groups["state"].Value));
                        Debug.Console(2, this, "Callstate Enum for Line {0} is {1}", lineNumber, (int)CallStatusEnum);

                        IncomingCallFeedback.FireUpdate();

                        OffHookFeedback.FireUpdate();

                        var match3 = Regex.Match(match2.Groups["cid"].Value, pattern3);
                        if (match3.Success)
                        {
                            CallerIdNumber = match3.Groups["number"].Value;
                            CallerIdName = match3.Groups["name"].Value;
                            ActiveCalls.First().Name = CallerIdName;
                            ActiveCalls.First().Number = CallerIdNumber;
                            if (lineNumber == LineNumber)
                            {
                                Debug.Console(2, this, "CallState Complete - Firing Updates");
                                CallerIdNumberFeedback.FireUpdate();
                                OffHookFeedback.FireUpdate();
                                if (IsVoip)
                                    VoipIsSubscribed = true;
                                if (!IsVoip)
                                    PotsIsSubscribed = true;
                            }
                        }
                        else
                        {
                            ActiveCalls.First().Name = "";
                            ActiveCalls.First().Number = "";
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Error in ParseSubscriptioMessage - {0}", e.Message);
            }
            if (customName == AutoAnswerCustomName)
            {
                AutoAnswerState = bool.Parse(value);

                AutoAnswerIsSubscribed = true;

                AutoAnswerFeedback.FireUpdate();
            }

            if (customName == HookStateCustomName)
            {

                if (value.IndexOf("OFF", StringComparison.Ordinal) > -1)
                    OffHookStatus = true;
                if (value.IndexOf("ON", StringComparison.Ordinal) > -1)
                    OffHookStatus = false;

                OffHookFeedback.FireUpdate();
            }
            if (customName != LastDialedCustomName) return;
            LastDialed = value;
            LastDialedFeedback.FireUpdate();
        }

        /// <summary>
        /// Parses any subscription-unrelated messages directed to this object
        /// </summary>
        /// <param name="attributeCode">Message attribute code to determine parsing algorithm</param>
        /// <param name="message">Data to be parsed</param>
        public override void ParseGetMessage(string attributeCode, string message) {
            try {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message
                const string pattern = "[^ ]* (.*)";

                var match = Regex.Match(message, pattern);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;
                switch (attributeCode) {
                    case "autoAnswer": {
                        AutoAnswerState = bool.Parse(value);

                        Debug.Console(1, this, "AutoAnswerState is '{0}'", AutoAnswerState);

                        AutoAnswerFeedback.FireUpdate();

                        break;
                    }
                    case "dndEnable": {
                        DoNotDisturbState = bool.Parse(value);

                        Debug.Console(1, this, "DoNotDisturbState is '{0}'", DoNotDisturbState);

                        DoNotDisturbFeedback.FireUpdate();

                        break;
                    }
                    default: {
                        Debug.Console(2, "Response does not match expected attribute codes: '{0}'", message);

                        break;
                    }
                }
            }
            catch (Exception e) {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
            }
        }

        /// <summary>
        /// Dials current value of DialString property
        /// </summary>
        public void Dial() {
            if (IsVoip) {
                if (OffHookStatus) {
                    SendFullCommand(null, "end", null, 2);

                    if (!ClearOnHangup) return;
                    DialString = String.Empty;
                    DialStringFeedback.FireUpdate();
                }
                else if (!OffHookStatus) {
                    if (!String.IsNullOrEmpty(DialString))
                    {
                        SendFullCommand(null, "dial", DialString, 2);
                    }
                }
            }

            else if (!IsVoip) {
                if (OffHookStatus) {
                    OnHook();

                    if (!ClearOnHangup) return;
                    DialString = String.Empty;
                    DialStringFeedback.FireUpdate();
                }
                else if (!OffHookStatus) {
                    if (!String.IsNullOrEmpty(DialString)) {
                        SendFullCommand(null, "dial", DialString, 1);
                    }
                    else
						SendFullCommand(null,"OFFHOOK", null, 1);
                
                        //SendFullCommand("set", "hookState", "OFFHOOK", 1);
                }
            }
        }

        /// <summary>
        /// Sets the DialString property for Dialing
        /// </summary>
        /// <param name="data">Value to set DialString to</param>
        public void SetDialString(string data) {
            DialString = data;
            DialStringFeedback.FireUpdate();
        }

        /// <summary>
        /// Place the call "receiver" "on hook" or otherwise end a call
        /// </summary>
        public void OnHook() {
            if (IsVoip) {
                SendFullCommand(null, "end", null, 2);
            }
            if (!IsVoip) {
                SendFullCommand("set", "hookState", "ONHOOK", 2);
            }
        }

        /// <summary>
        /// Take the call "receiver" "off hook", or otherwise answer an incoming call
        /// </summary>
        public void OffHook()
        {
			//if (IsVoip)
			//    Answer();
			//if (!IsVoip) {
			//    SendFullCommand("set", "hookState", "OFFHOOK", 2);
			//}
			SendFullCommand("offHook", "offHook", "", 1);
        }

        /// <summary>
        /// Answer an incoming call
        /// </summary>
        public void Answer() {
            if (IsVoip)
                SendFullCommand(null, "answer", null, 1);
        }
        /// <summary>
        /// Enable Auto Answer for the component
        /// </summary>
        public void AutoAnswerOn() {
            SendFullCommand("set", "autoAnswer", "true", 1);
            if (!IsVoip)
                SendFullCommand("get", "autoAnswer", null, 1);
        }

        /// <summary>
        /// Hold Call
        /// </summary>
        public void HoldCall()
        {
            SendFullCommand("set", "hold", null, 1);
        }
        /// <summary>
        /// Resume Call
        /// </summary>
        public void ResumeCall()
        {
            SendFullCommand("set", "resume", null, 1);
        }

        public void HoldToggle()
        {
            if (CallStatusEnum != ECallStatus.ON_HOLD) ResumeCall();
            else HoldCall();
        }

        /// <summary>
        /// Disable Auto Answer for the component
        /// </summary>
        public void AutoAnswerOff() {
            SendFullCommand("set", "autoAnswer", "false", 1);
            if (!IsVoip)
                SendFullCommand("get", "autoAnswer", null, 1);
        }

        /// <summary>
        /// Toggle the state of Auto Answer for the component
        /// </summary>
        public void AutoAnswerToggle() {
            SendFullCommand("toggle", "autoAnswer", null, 1);
            if (!IsVoip)
                SendFullCommand("get", "autoAnswer", null, 1);
        }

        /// <summary>
        /// Enable Do Not Disturb for the component
        /// </summary>
        public void DoNotDisturbOn() {
            SendFullCommand("set", "dndEnable", "true", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }

        /// <summary>
        /// Disable Do Not Distrub for the component
        /// </summary>
        public void DoNotDisturbOff() {
            SendFullCommand("set", "dndEnable", "false", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }

        /// <summary>
        /// End all connected calls
        /// </summary>
		public override void EndAllCalls()
		{
			OnHook();
		}
        /// <summary>
        /// Toggle Do Not Disturb for the component.
        /// </summary>
        public void DoNotDisturbToggle()
        {
            SendFullCommand("set", "dndEnable", DoNotDisturbState ? "false" : "true", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }

        /// <summary>
        /// Reject incoming call
        /// </summary>
        /// <param name="item">Call Object to Reject</param>
        public override void RejectCall(CodecActiveCallItem item) {
            SendFullCommand(null, "end", null, 1);
        }

        /// <summary>
        /// Send DTMF Digit to Component - Use SendKeypad instead
        /// </summary>
        /// <param name="digit">Digit to send</param>
        public override void SendDtmf(string digit) {
            SendFullCommand(null, "dtmf", digit, 1);
        }

        /// <summary>
        /// Send Keypad Digit to component - Will Send DTMF if call active
        /// </summary>
        /// <param name="data">Keypad press to send</param>
        public void SendKeypad(EKeypadKeys data) {
            if (!OffHookStatus) {
                switch (data) {
                    case EKeypadKeys.Num0:
                        DialString = DialString + "0";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num1:
                        DialString = DialString + "1";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num2:
                        DialString = DialString + "2";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num3:
                        DialString = DialString + "3";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num4:
                        DialString = DialString + "4";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num5:
                        DialString = DialString + "5";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num6:
                        DialString = DialString + "6";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num7:
                        DialString = DialString + "7";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num8:
                        DialString = DialString + "8";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Num9:
                        DialString = DialString + "9";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Star:
                        DialString = DialString + "*";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Pound:
                        DialString = DialString + "#";
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Clear:
                        DialString = String.Empty;
                        DialStringFeedback.FireUpdate();
                        break;
                    case EKeypadKeys.Backspace:
						if (DialString.Length > 0)
						{
							DialString = DialString.Remove(DialString.Length - 1, 1);
							DialStringFeedback.FireUpdate();
						} 
                        break;
                }
            }

            if (!OffHookStatus) return;

            switch (data) {
                case EKeypadKeys.Num0:
                    SendFullCommand(null, "dtmf", "0", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "0";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num1:
                    SendFullCommand(null, "dtmf", "1", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "1";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num2:
                    SendFullCommand(null, "dtmf", "2", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "2";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num3:
                    SendFullCommand(null, "dtmf", "3", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "3";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num4:
                    SendFullCommand(null, "dtmf", "4", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "4";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num5:
                    SendFullCommand(null, "dtmf", "5", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "5";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num6:
                    SendFullCommand(null, "dtmf", "6", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "6";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num7:
                    SendFullCommand(null, "dtmf", "7", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "7";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num8:
                    SendFullCommand(null, "dtmf", "8", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "8";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Num9:
                    SendFullCommand(null, "dtmf", "9", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "9";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Star:
                    SendFullCommand(null, "dtmf", "*", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "*";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Pound:
                    SendFullCommand(null, "dtmf", "#", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "#";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case EKeypadKeys.Clear:
                    break;
                case EKeypadKeys.Backspace:
                    break;
            }
        }

        /// <summary>
        /// List of possible Call Status values returned from component
        /// </summary>
        public enum ECallStatus {
// ReSharper disable InconsistentNaming
            INIT = 1,
            FAULT,
            IDLE,
            DIAL_TONE,
            SILENT,
            DIALING,
            RINGBACK,
            RINGING,
            ANSWERING,
            BUSY,
            REJECT,
            INVALID_NUMBER,
            ACTIVE,
            ACTIVE_MUTED,
            ON_HOLD,
            WAITING_RING,
            CONF_ACTIVE,
            CONF_HOLD,
            XFER_INIT,
            XFER_Silent,
            XFER_ReqDialing,
            XFER_Process,
            XFER_ReplacesProcess,
            XFER_Active,
            XFER_RingBack,
            XFER_OnHold,
            XFER_Decision,
            XFER_InitError,
            XFER_WAIT
// ReSharper restore InconsistentNaming
        }

        /// <summary>
        /// List of valid keypad keys to send to component
        /// </summary>
        public enum EKeypadKeys {
            Num0 = 0,
            Num1,
            Num2,
            Num3,
            Num4,
            Num5,
            Num6,
            Num7,
            Num8,
            Num9,
            Star,
            Pound,
            Clear,
            Backspace
        }


        void IBridgeAdvanced.LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraDialerJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraDialerJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(2, "Adding Dialer {0}", Key);

            for (var i = 0; i < joinMap.KeyPadNumeric.JoinSpan; i++)
            {
                var keyNumber = i;
                trilist.SetSigTrueAction((joinMap.KeyPadNumeric.JoinNumber + (uint)keyNumber), () => SendKeypad((EKeypadKeys)keyNumber));
            }

            trilist.SetSigTrueAction((joinMap.KeyPadStar.JoinNumber), () => SendKeypad(EKeypadKeys.Star));
            trilist.SetSigTrueAction((joinMap.KeyPadPound.JoinNumber), () => SendKeypad(EKeypadKeys.Pound));
            trilist.SetSigTrueAction((joinMap.KeyPadClear.JoinNumber), () => SendKeypad(EKeypadKeys.Clear));
            trilist.SetSigTrueAction((joinMap.KeyPadBackspace.JoinNumber), () => SendKeypad(EKeypadKeys.Backspace));

            trilist.SetSigTrueAction(joinMap.KeyPadDial.JoinNumber, Dial);
            trilist.SetSigTrueAction(joinMap.DoNotDisturbToggle.JoinNumber, DoNotDisturbToggle);
            trilist.SetSigTrueAction(joinMap.DoNotDisturbOn.JoinNumber, DoNotDisturbOn);
            trilist.SetSigTrueAction(joinMap.DoNotDisturbOff.JoinNumber, DoNotDisturbOff);
            trilist.SetSigTrueAction(joinMap.AutoAnswerToggle.JoinNumber, AutoAnswerToggle);
            trilist.SetSigTrueAction(joinMap.AutoAnswerOn.JoinNumber, AutoAnswerOn);
            trilist.SetSigTrueAction(joinMap.AutoAnswerOff.JoinNumber, AutoAnswerOff);
            trilist.SetSigTrueAction(joinMap.Answer.JoinNumber, Answer);
            trilist.SetSigTrueAction(joinMap.EndCall.JoinNumber, EndAllCalls);
            trilist.SetSigTrueAction(joinMap.OnHook.JoinNumber, OnHook);
            trilist.SetSigTrueAction(joinMap.OffHook.JoinNumber, OffHook);

            trilist.SetSigTrueAction(joinMap.HoldCall.JoinNumber, HoldCall);
            trilist.SetSigTrueAction(joinMap.ResumeCall.JoinNumber, ResumeCall);
            trilist.SetSigTrueAction(joinMap.HoldToggle.JoinNumber, HoldToggle);


            trilist.SetStringSigAction(joinMap.DialString.JoinNumber, SetDialString);

            DisplayNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.DisplayNumber.JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);

            DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbToggle.JoinNumber]);
            DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOn.JoinNumber]);
            DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOff.JoinNumber]);

            OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.KeyPadDial.JoinNumber]);
            OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.OffHook.JoinNumber]);
            OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.OnHook.JoinNumber]);
            IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IncomingCall.JoinNumber]);

            AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerToggle.JoinNumber]);
            AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerOn.JoinNumber]);
            AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoAnswerOff.JoinNumber]);

            DialStringFeedback.LinkInputSig(trilist.StringInput[joinMap.DialString.JoinNumber]);
            CallerIdNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.CallerIdNumberFb.JoinNumber]);
            CallerIdNameFeedback.LinkInputSig(trilist.StringInput[joinMap.CallerIdNameFb.JoinNumber]);
            LastDialedFeedback.LinkInputSig(trilist.StringInput[joinMap.LastNumberDialerFb.JoinNumber]);

            HoldCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.HoldCall.JoinNumber]);
            HoldCallFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.ResumeCall.JoinNumber]);
            HoldCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.HoldToggle.JoinNumber]);




            CallStateFeedback.LinkInputSig(trilist.UShortInput[joinMap.CallState.JoinNumber]);

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