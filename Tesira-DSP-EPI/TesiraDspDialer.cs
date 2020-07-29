using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Devices.Common.Codec;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Essentials.Devices;
using System.Text.RegularExpressions;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Tesira_DSP_EPI {
    public class TesiraDspDialer : TesiraDspDialerControlPoint, IBridgeAdvanced
    {

        public FeedbackCollection<Feedback> Feedbacks; 

        //public TesiraDsp Parent { get; private set; }
        public bool IsVoip { get; private set; }
        public string DialString { get; private set; }

        private bool _offHookStatus;
        public bool OffHookStatus {
            get {
                return _offHookStatus;
            }
            private set {
                _offHookStatus = value;
                Debug.Console(2, this, "_OffHookStatus = {0}", value.ToString());
                OffHookFeedback.FireUpdate();
            }
        }

        private string LastDialed { get; set; }

        private int CallAppearance { get; set; }

        private bool AppendDtmf { get; set; }
        private bool ClearOnHangup { get; set; }

        public bool AutoAnswerState { get; set; }

        public string DisplayNumber { get; set; }


        public bool DoNotDisturbState { get; private set; }

        public string DialerCustomName { get; set; }
        public string ControlStatusCustomName { get; set; }
        public string AutoAnswerCustomName { get; set; }
        public string HookStateCustomName { get; set; }
        public string PotsDialerCustomName { get; set; }
        public string LastDialedCustomName { get; set; }


        public string CallStatus {
            get {
                return CallStatusEnum.ToString();
            }
        }

        public BoolFeedback OffHookFeedback;
        public BoolFeedback AutoAnswerFeedback;
        public BoolFeedback DoNotDisturbFeedback;
        public StringFeedback DialStringFeedback;
        public StringFeedback CallerIdNumberFeedback;
        public StringFeedback CallerIdNameFeedback;
        public BoolFeedback IncomingCallFeedback;
        public IntFeedback CallStateFeedback;
        public StringFeedback LastDialedFeedback;
        public StringFeedback NameFeedback;

        private eCallStatus _CallStatusEnum { get; set; }
        private eCallStatus CallStatusEnum
        {
            get
            {
                return _CallStatusEnum;
            }
            set
            {
                _CallStatusEnum = value;
                if (CallStatusEnum == eCallStatus.DIAL_TONE ||
                    CallStatusEnum == eCallStatus.DIALING ||
                    CallStatusEnum == eCallStatus.ANSWERING ||
                    CallStatusEnum == eCallStatus.ACTIVE ||
                    CallStatusEnum == eCallStatus.ACTIVE_MUTED ||
                    CallStatusEnum == eCallStatus.BUSY ||
                    CallStatusEnum == eCallStatus.INVALID_NUMBER ||
                    CallStatusEnum == eCallStatus.ON_HOLD)
                {
                    if (IsVoip)
                        OffHookStatus = true;
                }
                else
                    if (IsVoip)
                        OffHookStatus = false;
                if (value == eCallStatus.IDLE && IsVoip)
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
                    case eCallStatus.INIT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.FAULT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.IDLE:
                        ActiveCalls.First().Status = eCodecCallStatus.Idle;
                        break;
                    case eCallStatus.DIAL_TONE:
                        ActiveCalls.First().Status = eCodecCallStatus.Idle;
                        break;
                    case eCallStatus.SILENT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.DIALING:
                        ActiveCalls.First().Status = eCodecCallStatus.Connecting;
                        ActiveCalls.First().Direction = eCodecCallDirection.Outgoing;
                        break;
                    case eCallStatus.RINGBACK:
                        ActiveCalls.First().Status = eCodecCallStatus.Connecting;
                        break;
                    case eCallStatus.RINGING:
                        ActiveCalls.First().Status = eCodecCallStatus.Ringing;
                        ActiveCalls.First().Direction = eCodecCallDirection.Incoming;
                        break;
                    case eCallStatus.BUSY:
                        ActiveCalls.First().Status = eCodecCallStatus.Disconnecting;
                        break;
                    case eCallStatus.REJECT:
                        ActiveCalls.First().Status = eCodecCallStatus.Disconnecting;
                        break;
                    case eCallStatus.INVALID_NUMBER:
                        ActiveCalls.First().Status = eCodecCallStatus.Disconnecting;
                        break;
                    case eCallStatus.ACTIVE:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case eCallStatus.ACTIVE_MUTED:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case eCallStatus.ON_HOLD:
                        ActiveCalls.First().Status = eCodecCallStatus.OnHold;
                        break;
                    case eCallStatus.WAITING_RING:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case eCallStatus.CONF_ACTIVE:
                        ActiveCalls.First().Status = eCodecCallStatus.Connected;
                        break;
                    case eCallStatus.CONF_HOLD:
                        ActiveCalls.First().Status = eCodecCallStatus.OnHold;
                        break;
                    case eCallStatus.XFER_INIT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_Silent:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_ReqDialing:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_Process:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_ReplacesProcess:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_Active:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_RingBack:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_OnHold:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_Decision:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_InitError:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    case eCallStatus.XFER_WAIT:
                        ActiveCalls.First().Status = eCodecCallStatus.Unknown;
                        break;
                    default:
                        break;
                }
            }
        }

        public bool IncomingCallState {
            get
            {
                return CallStatusEnum == eCallStatus.RINGING;
            }
        }

        public int LineNumber { get; private set; }
      
        private string _callerIdNumber { get; set; }
        public string CallerIdNumber {
            get {
                return _callerIdNumber;
            }
            set {
                _callerIdNumber = value;
                CallerIdNumberFeedback.FireUpdate();
            }
        }

        private string _callerIdName { get; set; }
        public string CallerIdName {
            get {
                return _callerIdName;
            }
            set {
                _callerIdName = value;
                CallerIdNameFeedback.FireUpdate();
            }
        }

        public override bool IsSubscribed {
            get {
                bool isSubscribed;
                if (IsVoip) {
                    if (VoipIsSubscribed && AutoAnswerIsSubscribed)
                        isSubscribed =  true;
                    else
                        isSubscribed =  false;
                }
                else if (!IsVoip) {
                    if (PotsIsSubscribed && HookStateIsSubscribed)
                        isSubscribed = true;
                    else
                        isSubscribed = false;
                }
                else
                    isSubscribed =  false;
                return isSubscribed;
            }
            protected set { }
        }

        private bool VoipIsSubscribed { get; set; }
        private bool DndIsSubscribed { get; set; }
        private bool AutoAnswerIsSubscribed { get; set; }
        private bool HookStateIsSubscribed { get; set; }
        private bool PotsIsSubscribed { get; set; }

		public TesiraDspDialer(string key, TesiraDialerControlBlockConfig config, TesiraDsp parent)
            : base(key, config.DialerInstanceTag, config.ControlStatusInstanceTag, config.Index, config.CallAppearance, parent, config.BridgeIndex)
        {

            Key = string.Format("{0}--Dialer{1}", parent.Key, key);

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

            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(key, config);

        }

		public void Initialize(string key, TesiraDialerControlBlockConfig config)
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
            var activeCall = new CodecActiveCallItem();
            activeCall.Name = "";
            activeCall.Number = "";
            activeCall.Type = eCodecCallType.Audio;
            activeCall.Status = eCodecCallStatus.Idle;
            activeCall.Direction = eCodecCallDirection.Unknown;
            activeCall.Id = Key;

            ActiveCalls.Add(activeCall);
        }

        public override void AcceptCall(CodecActiveCallItem item) {
            SendFullCommand(null, "answer", null, 1);
        }

        public override void Subscribe() {
            if (IsVoip) {
                DialerCustomName = string.Format("{0}~VoIPDialer{1}", InstanceTag1, Index1);
                AutoAnswerCustomName = string.Format("{0}~VoIPDialerAutoAnswer{1}", InstanceTag1, Index1);
                ControlStatusCustomName = string.Format("{0}~VoIPControl{1}", InstanceTag2, Index1);
                LastDialedCustomName = string.Format("{0}~VoIPLastNumber{1}", InstanceTag1, Index1);


                SendSubscriptionCommand(ControlStatusCustomName, "callState", 250, 2);

                SendSubscriptionCommand(AutoAnswerCustomName, "autoAnswer", 500, 1);

                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);
            }
            else if (!IsVoip) {
                PotsDialerCustomName = string.Format("{0}~PotsDialer{1}", InstanceTag1, Index1);
                LastDialedCustomName = string.Format("{0}~PotsLastNumber{1}", InstanceTag1, Index1);

                HookStateCustomName = string.Format("{0}~HookState{1}", InstanceTag1, Index1);

                SendSubscriptionCommand(DialerCustomName, "callState", 250, 1);

                SendSubscriptionCommand(HookStateCustomName, "hookState", 500, 1);

                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);

                SendFullCommand("get", "autoAnswer", null, 1);
            }

            SendFullCommand("get", "dndEnable", null, 1);
        }

        public override void Unsubscribe()
        {
            if (IsVoip)
            {
                DialerCustomName = string.Format("{0}~VoIPDialer{1}", InstanceTag1, Index1);
                AutoAnswerCustomName = string.Format("{0}~VoIPDialerAutoAnswer{1}", InstanceTag1, Index1);
                ControlStatusCustomName = string.Format("{0}~VoIPControl{1}", InstanceTag2, Index1);
                LastDialedCustomName = string.Format("{0}~VoIPLastNumber{1}", InstanceTag1, Index1);


                SendUnSubscriptionCommand(ControlStatusCustomName, "callState", 2);

                SendUnSubscriptionCommand(AutoAnswerCustomName, "autoAnswer", 1);

                SendUnSubscriptionCommand(LastDialedCustomName, "lastNum", 1);
            }
            else if (!IsVoip)
            {
                PotsDialerCustomName = string.Format("{0}~PotsDialer{1}", InstanceTag1, Index1);
                LastDialedCustomName = string.Format("{0}~PotsLastNumber{1}", InstanceTag1, Index1);

                HookStateCustomName = string.Format("{0}~HookState{1}", InstanceTag1, Index1);

                SendUnSubscriptionCommand(DialerCustomName, "callState", 1);

                SendUnSubscriptionCommand(HookStateCustomName, "hookState", 1);

                SendUnSubscriptionCommand(LastDialedCustomName, "lastNum", 1);

            }
        }

        // <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        public void ParseSubscriptionMessage(string customName, string value)
        {
            try
            {
                Debug.Console(2, this, "New Subscription Message to Dialer");
                if (customName == ControlStatusCustomName || customName == PotsDialerCustomName)
                {
                    //Pulls Entire Value "array" and seperates call appearances
                    var pattern1 = "\\[([^\\[\\]]+)\\]";
                    //Seperates each call appearance into their constituent parts
                    var pattern2 = "\\[(?<state>\\d+)\\s+(?<line>\\d+)\\s+(?<call>\\d+)\\s+(?<action>\\d+)\\s+(?<cid>\".+\"|\"\")\\s+(?<prompt>\\d+)\\]";
                    //Pulls CallerID Data
                    var pattern3 = "(?:(?:\\\\\"(?<time>.*)\\\\\")(?:\\\\\"(?<number>.*)\\\\\")(?:\\\\\"(?<name>.*)\\\\\"))|\"\"";

                    var myMatches = Regex.Matches(value, pattern1);

                    Debug.Console(2, this, "This is the list of Call States - {0}", myMatches.ToString());

                    var match = myMatches[CallAppearance - 1];
                    var match2 = Regex.Match(match.Value, pattern2);
                    if (match2.Success)
                    {
                        Debug.Console(2, this, "VoIPControlStatus Subscribed Response = {0}", match.Value);
                        var lineNumber = (int)(ushort.Parse(match2.Groups["line"].Value) + 1);
                        var callStatusInt = int.Parse(match2.Groups["state"].Value);
                        CallStatusEnum = (eCallStatus)(callStatusInt);
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
        /// Parses any non-subscribed messages destined for this class
        /// </summary>
        /// <param name="attributeCode"></param>
        /// <param name="message"></param>
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

        public void Dial() {
            if (IsVoip) {
                if (OffHookStatus) {
                    SendFullCommand(null, "end", null, 1);

                    if (!ClearOnHangup) return;
                    DialString = String.Empty;
                    DialStringFeedback.FireUpdate();
                }
                else if (!OffHookStatus) {
                    if (!String.IsNullOrEmpty(DialString))
                    {
                        SendFullCommand(null, "dial", DialString, 1);
                    }
                }
            }

            else if (!IsVoip) {
                if (OffHookStatus) {
                    SendFullCommand("set", "hookState", "ONHOOK", 1);

                    if (!ClearOnHangup) return;
                    DialString = String.Empty;
                    DialStringFeedback.FireUpdate();
                }
                else if (!OffHookStatus) {
                    if (!String.IsNullOrEmpty(DialString)) {
                        SendFullCommand(null, "dial", DialString, 1);
                    }
                    else
                        SendFullCommand("set", "hookState", "OFFHOOK", 1);
                }
            }
        }

        public void SetDialString(string data) {
            DialString = data;
            DialStringFeedback.FireUpdate();
        }

        public void OnHook() {
            if (IsVoip) {
                SendFullCommand(null, "onHook", null, 1);
            }
            if (!IsVoip) {
                SendFullCommand("set", "hookState", "ONHOOK", 1);
            }
        }

        public void OffHook() {
            if (IsVoip)
                SendFullCommand(null, "answer", null, 1);
            if (!IsVoip) {
                SendFullCommand("set", "hookState", "OFFHOOK", 1);
            }
        }

        public void Answer() {
            if (IsVoip)
                SendFullCommand(null, "answer", null, 1);
        }

        

        

        public void AutoAnswerOn() {
            SendFullCommand("set", "autoAnswer", "true", 1);
            if (!IsVoip)
                SendFullCommand("get", "autoAnswer", null, 1);
        }

        public void AutoAnswerOff() {
            SendFullCommand("set", "autoAnswer", "false", 1);
            if (!IsVoip)
                SendFullCommand("get", "autoAnswer", null, 1);
        }

        public void AutoAnswerToggle() {
            SendFullCommand("toggle", "autoAnswer", null, 1);
            if (!IsVoip)
                SendFullCommand("get", "autoAnswer", null, 1);
        }

        public void DoNotDisturbOn() {
            SendFullCommand("set", "dndEnable", "true", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }
        public void DoNotDisturbOff() {
            SendFullCommand("set", "dndEnable", "false", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }
        public void DoNotDisturbToggle() {
            if (DoNotDisturbState) 
                SendFullCommand("set", "dndEnable", "false", 1);
            else
                SendFullCommand("set", "dndEnable", "true", 1);
            SendFullCommand("get", "dndEnable", null, 1);
        }


        public override void RejectCall(CodecActiveCallItem item) {
            SendFullCommand(null, "end", null, 1);
        }

        public override void SendDtmf(string digit) {
            throw new NotImplementedException();
        }

        public void SendKeypad(eKeypadKeys data) {
            if (!OffHookStatus) {
                switch (data) {
                    case eKeypadKeys.Num0:
                        DialString = DialString + "0";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num1:
                        DialString = DialString + "1";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num2:
                        DialString = DialString + "2";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num3:
                        DialString = DialString + "3";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num4:
                        DialString = DialString + "4";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num5:
                        DialString = DialString + "5";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num6:
                        DialString = DialString + "6";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num7:
                        DialString = DialString + "7";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num8:
                        DialString = DialString + "8";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num9:
                        DialString = DialString + "9";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Star:
                        DialString = DialString + "*";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Pound:
                        DialString = DialString + "#";
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Clear:
                        DialString = String.Empty;
                        DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Backspace:
                        DialString = DialString.Remove(DialString.Length - 1, 1);
                        DialStringFeedback.FireUpdate();
                        break;
                    default:
                        break;
                }
            }

            if (!OffHookStatus) return;

            switch (data) {
                case eKeypadKeys.Num0:
                    SendFullCommand(null, "dtmf", "0", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "0";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num1:
                    SendFullCommand(null, "dtmf", "1", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "1";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num2:
                    SendFullCommand(null, "dtmf", "2", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "2";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num3:
                    SendFullCommand(null, "dtmf", "3", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "3";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num4:
                    SendFullCommand(null, "dtmf", "4", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "4";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num5:
                    SendFullCommand(null, "dtmf", "5", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "5";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num6:
                    SendFullCommand(null, "dtmf", "6", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "6";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num7:
                    SendFullCommand(null, "dtmf", "7", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "7";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num8:
                    SendFullCommand(null, "dtmf", "8", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "8";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Num9:
                    SendFullCommand(null, "dtmf", "9", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "9";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Star:
                    SendFullCommand(null, "dtmf", "*", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "*";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Pound:
                    SendFullCommand(null, "dtmf", "#", 1);
                    if (AppendDtmf) {
                        DialString = DialString + "#";
                        DialStringFeedback.FireUpdate();
                    }
                    break;
                case eKeypadKeys.Clear:
                    break;
                case eKeypadKeys.Backspace:
                    break;
                default:
                    break;
            }
        }

        private enum eCallStatus {
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
        }

        public enum eKeypadKeys {
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
                trilist.SetSigTrueAction((joinMap.KeyPadNumeric.JoinNumber + (uint)i), () => SendKeypad(eKeypadKeys.Num0));

            }

            trilist.SetSigTrueAction((joinMap.KeyPadStar.JoinNumber), () => SendKeypad(eKeypadKeys.Star));
            trilist.SetSigTrueAction((joinMap.KeyPadPound.JoinNumber), () => SendKeypad(eKeypadKeys.Pound));
            trilist.SetSigTrueAction((joinMap.KeyPadClear.JoinNumber), () => SendKeypad(eKeypadKeys.Clear));
            trilist.SetSigTrueAction((joinMap.KeyPadBackspace.JoinNumber), () => SendKeypad(eKeypadKeys.Backspace));

            trilist.StringInput[joinMap.Label.JoinNumber].StringValue = Label;
            trilist.StringInput[joinMap.DisplayNumber.JoinNumber].StringValue = DisplayNumber;

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

            trilist.SetStringSigAction(joinMap.DialString.JoinNumber, SetDialString);

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