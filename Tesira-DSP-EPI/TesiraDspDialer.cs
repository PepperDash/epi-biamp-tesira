using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp;
using PepperDash.Essentials.Devices.Common.Codec;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Essentials.Devices;
using System.Text.RegularExpressions;

namespace Tesira_DSP_EPI {
    public class TesiraDspDialer : TesiraDspDialerControlPoint
    {

        //public TesiraDsp Parent { get; private set; }
        public bool IsVoip { get; private set; }
        public string DialString { get; private set; }

        private bool _OffHookStatus { get; set; }
        public bool OffHookStatus {
            get {
                return _OffHookStatus;
            }
            private set {
                _OffHookStatus = value;
                Debug.Console(2, this, "_OffHookStatus = {0}", value.ToString());
                this.OffHookFeedback.FireUpdate();
            }
        }

        private string _LastDialed { get; set; }

        private int CallAppearance { get; set; }

        private bool AppendDtmf { get; set; }
        private bool ClearOnHangup { get; set; }

        private bool _AutoAnswerState { get; set; }
        public bool AutoAnswerState { get; set; }

        public string DisplayNumber { get; set; }



        private bool _DoNotDisturbState { get; set; }
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
            set { }
        }

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
                        this.DialStringFeedback.FireUpdate();
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
            get {
                if (CallStatusEnum == eCallStatus.RINGING)
                    return true;
                else
                    return false;
            }
            set { 
            }
        }

        public int LineNumber { get; private set; }
      
        private string _CallerIDNumber { get; set; }
        public string CallerIDNumber {
            get {
                return _CallerIDNumber;
            }
            set {
                _CallerIDNumber = value;
                CallerIDNumberFeedback.FireUpdate();
            }
        }

        private string _CallerIDName { get; set; }
        public string CallerIDName {
            get {
                return _CallerIDName;
            }
            set {
                _CallerIDName = value;
                CallerIDNameFeedback.FireUpdate();
            }
        }

        public BoolFeedback OffHookFeedback;
        public BoolFeedback AutoAnswerFeedback;
        public BoolFeedback DoNotDisturbFeedback;
        public StringFeedback DialStringFeedback;
        public StringFeedback CallerIDNumberFeedback;
        public StringFeedback CallerIDNameFeedback;
        public BoolFeedback IncomingCallFeedback;
        public IntFeedback CallStateFeedback;
        public StringFeedback LastDialedFeedback;

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
            : base(key, config.dialerInstanceTag, config.controlStatusInstanceTag, config.index, config.callAppearance, parent) {

            DialStringFeedback = new StringFeedback(() => { return DialString; });
            OffHookFeedback = new BoolFeedback(() => { return OffHookStatus; });
            AutoAnswerFeedback = new BoolFeedback(() => { return AutoAnswerState; });
            DoNotDisturbFeedback = new BoolFeedback(() => { return DoNotDisturbState; });
            CallerIDNumberFeedback = new StringFeedback(() => { return CallerIDNumber; });
            CallerIDNameFeedback = new StringFeedback(() => { return CallerIDName; });
            IncomingCallFeedback = new BoolFeedback(() => { return IncomingCallState; });
            CallStateFeedback = new IntFeedback(() => { return (int)CallStatusEnum; });
            LastDialedFeedback = new StringFeedback(() => { return _LastDialed; });

            Initialize(key, config);

        }

        public void Initialize(string key, TesiraDialerControlBlockConfig config) {
            Key = string.Format("{0}--{1}", Parent.Key, key);

            DeviceManager.AddDevice(this);

            Debug.Console(2, this, "Adding LevelControl '{0}'", Key);

            IsSubscribed = false;
            PotsIsSubscribed = false;
            VoipIsSubscribed = false;
            AutoAnswerIsSubscribed = false;

            Label = config.label;
            IsVoip = config.isVoip;
            LineNumber = config.index;
            AppendDtmf = config.appendDtmf;
            ClearOnHangup = config.clearOnHangup;
            Enabled = config.enabled;
            CallAppearance = config.callAppearance;
            DisplayNumber = config.displayNumber;

            base.ActiveCalls = new List<CodecActiveCallItem>();
            CodecActiveCallItem ActiveCall = new CodecActiveCallItem();
            ActiveCall.Name = "";
            ActiveCall.Number = "";
            ActiveCall.Type = eCodecCallType.Audio;
            ActiveCall.Status = eCodecCallStatus.Idle;
            ActiveCall.Direction = eCodecCallDirection.Unknown;
            ActiveCall.Id = this.Key;

            ActiveCalls.Add(ActiveCall);
        }

        public override void AcceptCall(CodecActiveCallItem item) {
            SendFullCommand(null, "answer", null, 1);
        }

        public override void Subscribe() {
            if (IsVoip) {
                DialerCustomName = string.Format("{0}~VoIPDialer{1}", this.InstanceTag1, this.Index1);
                AutoAnswerCustomName = string.Format("{0}~VoIPDialerAutoAnswer{1}", this.InstanceTag1, this.Index1);
                ControlStatusCustomName = string.Format("{0}~VoIPControl{1}", this.InstanceTag2, this.Index1);
                LastDialedCustomName = string.Format("{0}~VoIPLastNumber{1}", this.InstanceTag1, this.Index1);


                SendSubscriptionCommand(ControlStatusCustomName, "callState", 250, 2);

                SendSubscriptionCommand(AutoAnswerCustomName, "autoAnswer", 500, 1);

                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);
            }
            else if (!IsVoip) {
                PotsDialerCustomName = string.Format("{0}~PotsDialer{1}", this.InstanceTag1, this.Index1);
                LastDialedCustomName = string.Format("{0}~PotsLastNumber{1}", this.InstanceTag1, this.Index1);

                HookStateCustomName = string.Format("{0}~HookState{1}", this.InstanceTag1, this.Index1);

                SendSubscriptionCommand(DialerCustomName, "callState", 250, 1);

                SendSubscriptionCommand(HookStateCustomName, "hookState", 500, 1);

                SendSubscriptionCommand(LastDialedCustomName, "lastNum", 500, 1);

                SendFullCommand("get", "autoAnswer", null, 1);
            }

            SendFullCommand("get", "dndEnable", null, 1);
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
                    string pattern1 = "\\[([^\\[\\]]+)\\]";
                    //Seperates each call appearance into their constituent parts
                    string pattern2 = "\\[(?<state>\\d+)\\s+(?<line>\\d+)\\s+(?<call>\\d+)\\s+(?<action>\\d+)\\s+(?<cid>\".+\"|\"\")\\s+(?<prompt>\\d+)\\]";
                    //Pulls CallerID Data
                    string pattern3 = "(?:(?:\\\\\"(?<time>.*)\\\\\")(?:\\\\\"(?<number>.*)\\\\\")(?:\\\\\"(?<name>.*)\\\\\"))|\"\"";

                    var myMatches = Regex.Matches(value, pattern1);

                    Debug.Console(2, this, "This is the list of Call States - {0}", myMatches.ToString());

                    Match match = myMatches[CallAppearance - 1];
                    Match match2 = Regex.Match(match.Value, pattern2);
                    if (match2.Success)
                    {
                        Debug.Console(2, this, "VoIPControlStatus Subscribed Response = {0}", match.Value);
                        int lineNumber = (int)(ushort.Parse(match2.Groups["line"].Value) + 1);
                        var CallStatusInt = int.Parse(match2.Groups["state"].Value.ToString());
                        CallStatusEnum = (eCallStatus)(CallStatusInt);
                        Debug.Console(2, this, "Callstate for Line {0} is {1}", lineNumber, int.Parse(match2.Groups["state"].Value));
                        Debug.Console(2, this, "Callstate Enum for Line {0} is {1}", lineNumber, (int)CallStatusEnum);
                        if (CallStatusEnum == eCallStatus.RINGING)
                        {
                            IncomingCallState = true;
                        }
                        else
                            IncomingCallState = false;
                        this.IncomingCallFeedback.FireUpdate();

                        this.OffHookFeedback.FireUpdate();

                        Match match3 = Regex.Match(match2.Groups["cid"].Value, pattern3);
                        if (match3.Success)
                        {
                            CallerIDNumber = match3.Groups["number"].Value;
                            CallerIDName = match3.Groups["name"].Value;
                            ActiveCalls.First().Name = CallerIDName;
                            ActiveCalls.First().Number = CallerIDNumber;
                            if (lineNumber == LineNumber)
                            {
                                Debug.Console(2, this, "CallState Complete - Firing Updates");
                                this.CallerIDNumberFeedback.FireUpdate();
                                this.OffHookFeedback.FireUpdate();
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

                this.AutoAnswerFeedback.FireUpdate();
            }

            if (customName == HookStateCustomName)
            {

                if (value.IndexOf("OFF") > -1)
                    OffHookStatus = true;
                if (value.IndexOf("ON") > -1)
                    OffHookStatus = false;

                this.OffHookFeedback.FireUpdate();
            }
            if (customName == LastDialedCustomName)
            {
                _LastDialed = value;
                this.LastDialedFeedback.FireUpdate();
            }

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
                string pattern = "[^ ]* (.*)";

                Match match = Regex.Match(message, pattern);

                if (match.Success) {

                    string value = match.Groups[1].Value;

                    Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                    if (message.IndexOf("+OK") > -1) {
                        switch (attributeCode) {
                            case "autoAnswer": {
                                    AutoAnswerState = bool.Parse(value);

                                    Debug.Console(1, this, "AutoAnswerState is '{0}'", AutoAnswerState);

                                    this.AutoAnswerFeedback.FireUpdate();

                                    break;
                                }
                            case "dndEnable": {
                                    DoNotDisturbState = bool.Parse(value);

                                    Debug.Console(1, this, "DoNotDisturbState is '{0}'", DoNotDisturbState);

                                    this.DoNotDisturbFeedback.FireUpdate();

                                    break;
                                }
                            default: {
                                    Debug.Console(2, "Response does not match expected attribute codes: '{0}'", message);

                                    break;
                                }
                        }
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
                    if (ClearOnHangup) {
                        DialString = String.Empty;
                        this.DialStringFeedback.FireUpdate();
                    }
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
                    if (ClearOnHangup) {
                        DialString = String.Empty;
                        this.DialStringFeedback.FireUpdate();
                    }
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
            this.DialStringFeedback.FireUpdate();
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
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num1:
                        DialString = DialString + "1";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num2:
                        DialString = DialString + "2";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num3:
                        DialString = DialString + "3";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num4:
                        DialString = DialString + "4";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num5:
                        DialString = DialString + "5";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num6:
                        DialString = DialString + "6";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num7:
                        DialString = DialString + "7";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num8:
                        DialString = DialString + "8";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Num9:
                        DialString = DialString + "9";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Star:
                        DialString = DialString + "*";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Pound:
                        DialString = DialString + "#";
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Clear:
                        DialString = String.Empty;
                        this.DialStringFeedback.FireUpdate();
                        break;
                    case eKeypadKeys.Backspace:
                        DialString = DialString.Remove(DialString.Length - 1, 1);
                        this.DialStringFeedback.FireUpdate();
                        break;
                    default:
                        break;
                }
            }
            if (OffHookStatus) {
                switch (data) {
                    case eKeypadKeys.Num0:
                        SendFullCommand(null, "dtmf", "0", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "0";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num1:
                        SendFullCommand(null, "dtmf", "1", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "1";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num2:
                        SendFullCommand(null, "dtmf", "2", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "2";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num3:
                        SendFullCommand(null, "dtmf", "3", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "3";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num4:
                        SendFullCommand(null, "dtmf", "4", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "4";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num5:
                        SendFullCommand(null, "dtmf", "5", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "5";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num6:
                        SendFullCommand(null, "dtmf", "6", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "6";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num7:
                        SendFullCommand(null, "dtmf", "7", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "7";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num8:
                        SendFullCommand(null, "dtmf", "8", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "8";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Num9:
                        SendFullCommand(null, "dtmf", "9", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "9";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Star:
                        SendFullCommand(null, "dtmf", "*", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "*";
                            this.DialStringFeedback.FireUpdate();
                        }
                        break;
                    case eKeypadKeys.Pound:
                        SendFullCommand(null, "dtmf", "#", 1);
                        if (AppendDtmf) {
                            DialString = DialString + "#";
                            this.DialStringFeedback.FireUpdate();
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

    }
}