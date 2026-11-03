using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.Codec;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Dialer
{
    /// <summary>
    /// Represents a single call appearance on a Tesira dialer line. A line owns one or more
    /// call appearances. Each appearance tracks its own call state, caller ID, hook status and
    /// dial string, and exposes the per-call actions (dial, answer, end, hold, etc.). All device
    /// commands are routed through the owning line so the correct line/appearance indexes are used.
    /// </summary>
    public class TesiraCallAppearance
    {
        private readonly TesiraDspDialer _line;

        /// <summary>
        /// Call appearance number on the line (1-based).
        /// </summary>
        public int AppearanceNumber { get; }

        /// <summary>
        /// Optional bridge index used to offset this appearance's joins on the holistic bridge.
        /// </summary>
        public uint? BridgeIndex { get; }

        /// <summary>
        /// Friendly label for this appearance.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Unique key for this appearance.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The active call item tracked for this appearance.
        /// </summary>
        public CodecActiveCallItem ActiveCall { get; }

        /// <summary>
        /// Collection of feedbacks owned by this appearance.
        /// </summary>
        public FeedbackCollection<Feedback> Feedbacks { get; }

        private string _dialString;
        /// <summary>
        /// Current dial string for this appearance.
        /// </summary>
        public string DialString
        {
            get { return _dialString; }
            set
            {
                _dialString = value;
                DialStringFeedback.FireUpdate();
            }
        }

        private bool _offHookStatus;
        /// <summary>
        /// Current hook status for this appearance.
        /// </summary>
        public bool OffHookStatus
        {
            get { return _offHookStatus; }
            protected set
            {
                _offHookStatus = value;
                OffHookFeedback.FireUpdate();
            }
        }

        private string _callerIdNumber;
        /// <summary>
        /// Caller ID number for the current call.
        /// </summary>
        public string CallerIdNumber
        {
            get { return _callerIdNumber; }
            set
            {
                _callerIdNumber = value;
                CallerIdNumberFeedback.FireUpdate();
            }
        }

        private string _callerIdName;
        /// <summary>
        /// Caller ID name for the current call.
        /// </summary>
        public string CallerIdName
        {
            get { return _callerIdName; }
            set
            {
                _callerIdName = value;
                CallerIdNameFeedback.FireUpdate();
            }
        }

        private string _callerIdTimestamp;
        /// <summary>
        /// Device-reported caller ID timestamp (MMDDHHmm) for the current call.
        /// </summary>
        public string CallerIdTimestamp
        {
            get { return _callerIdTimestamp; }
            set
            {
                _callerIdTimestamp = value;
                CallerIdTimestampFeedback.FireUpdate();
            }
        }

        // ReSharper disable once InconsistentNaming
        private TesiraDspDialer.ECallStatus _callStatusEnum;
        /// <summary>
        /// Current call status of this appearance.
        /// </summary>
        public TesiraDspDialer.ECallStatus CallStatusEnum
        {
            get { return _callStatusEnum; }
            set
            {
                _callStatusEnum = value;
                UpdateFromCallStatus();
            }
        }

        /// <summary>
        /// True when the appearance has an incoming (ringing) call.
        /// </summary>
        public bool IncomingCallState
        {
            get { return CallStatusEnum == TesiraDspDialer.ECallStatus.RINGING; }
        }

        /// <summary>
        /// True when this appearance is part of a local conference (active or on hold). Conferencing
        /// is supported on VoIP lines only.
        /// </summary>
        public bool InConferenceState
        {
            get
            {
                return CallStatusEnum == TesiraDspDialer.ECallStatus.CONF_ACTIVE ||
                       CallStatusEnum == TesiraDspDialer.ECallStatus.CONF_HOLD;
            }
        }

        /// <summary>String feedback for the current dial string.</summary>
        public StringFeedback DialStringFeedback;
        /// <summary>Bool feedback for the hook status.</summary>
        public BoolFeedback OffHookFeedback;
        /// <summary>String feedback for the caller ID number.</summary>
        public StringFeedback CallerIdNumberFeedback;
        /// <summary>String feedback for the caller ID name.</summary>
        public StringFeedback CallerIdNameFeedback;
        /// <summary>String feedback for the caller ID timestamp.</summary>
        public StringFeedback CallerIdTimestampFeedback;
        /// <summary>Bool feedback for incoming call status.</summary>
        public BoolFeedback IncomingCallFeedback;
        /// <summary>Int feedback for the current call state.</summary>
        public IntFeedback CallStateFeedback;
        /// <summary>Bool feedback for hold status.</summary>
        public BoolFeedback HoldCallFeedback;
        /// <summary>Bool feedback for conference participation status.</summary>
        public BoolFeedback InConferenceFeedback;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="line">Owning dialer line.</param>
        /// <param name="appearanceNumber">Call appearance number (1-based).</param>
        /// <param name="label">Friendly label.</param>
        /// <param name="bridgeIndex">Optional bridge index.</param>
        public TesiraCallAppearance(TesiraDspDialer line, int appearanceNumber, string label, uint? bridgeIndex)
        {
            _line = line;
            AppearanceNumber = appearanceNumber;
            Label = label;
            BridgeIndex = bridgeIndex;
            Key = string.Format("{0}--CA{1}", line.Key, appearanceNumber);

            ActiveCall = new CodecActiveCallItem
            {
                Name = "",
                Number = "",
                Type = eCodecCallType.Audio,
                Status = eCodecCallStatus.Idle,
                Direction = eCodecCallDirection.Unknown,
                Id = Key
            };

            Feedbacks = new FeedbackCollection<Feedback>();

            DialStringFeedback = new StringFeedback(Key + "-DialStringFeedback", () => DialString);
            OffHookFeedback = new BoolFeedback(Key + "-OffHookFeedback", () => OffHookStatus);
            CallerIdNumberFeedback = new StringFeedback(Key + "-CallerIDNumberFeedback", () => CallerIdNumber);
            CallerIdNameFeedback = new StringFeedback(Key + "-CallerIDNameFeedback", () => CallerIdName);
            CallerIdTimestampFeedback = new StringFeedback(Key + "-CallerIDTimestampFeedback", () => CallerIdTimestamp);
            IncomingCallFeedback = new BoolFeedback(Key + "-IncomingCallFeedback", () => IncomingCallState);
            CallStateFeedback = new IntFeedback(Key + "-CallStateFeedback", () => (int)CallStatusEnum);
            HoldCallFeedback = new BoolFeedback(Key + "-HoldCallFeedback", () => CallStatusEnum == TesiraDspDialer.ECallStatus.ON_HOLD);
            InConferenceFeedback = new BoolFeedback(Key + "-InConferenceFeedback", () => InConferenceState);

            Feedbacks.Add(DialStringFeedback);
            Feedbacks.Add(OffHookFeedback);
            Feedbacks.Add(CallerIdNumberFeedback);
            Feedbacks.Add(CallerIdNameFeedback);
            Feedbacks.Add(CallerIdTimestampFeedback);
            Feedbacks.Add(IncomingCallFeedback);
            Feedbacks.Add(CallStateFeedback);
            Feedbacks.Add(HoldCallFeedback);
            Feedbacks.Add(InConferenceFeedback);
        }

        private void UpdateFromCallStatus()
        {
            if (CallStatusEnum == TesiraDspDialer.ECallStatus.DIAL_TONE ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.SILENT ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.DIALING ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.RINGBACK ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.BUSY ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.ANSWERING ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.INVALID_NUMBER ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.ACTIVE ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.ACTIVE_MUTED ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.ON_HOLD ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.WAITING_RING ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.CONF_ACTIVE ||
                CallStatusEnum == TesiraDspDialer.ECallStatus.CONF_HOLD)
            {
                OffHookStatus = true;
                var cidCmd = _line.IsVoip ? string.Format("cid {0} {1}", _line.LineNumber, AppearanceNumber) : "cid";
                _line.SendFullCommand("get", cidCmd, null, 2);
            }
            else
            {
                OffHookStatus = false;
            }

            if (CallStatusEnum == TesiraDspDialer.ECallStatus.IDLE && _line.ClearOnHangup)
            {
                DialString = string.Empty;
            }

            CallStateFeedback.FireUpdate();
            IncomingCallFeedback.FireUpdate();
            HoldCallFeedback.FireUpdate();
            InConferenceFeedback.FireUpdate();

            switch (CallStatusEnum)
            {
                case TesiraDspDialer.ECallStatus.INIT:
                    ActiveCall.Status = eCodecCallStatus.Unknown;
                    break;
                case TesiraDspDialer.ECallStatus.FAULT:
                    ActiveCall.Status = eCodecCallStatus.Unknown;
                    break;
                case TesiraDspDialer.ECallStatus.IDLE:
                    ActiveCall.Status = eCodecCallStatus.Idle;
                    break;
                case TesiraDspDialer.ECallStatus.DIAL_TONE:
                    ActiveCall.Status = eCodecCallStatus.Idle;
                    break;
                case TesiraDspDialer.ECallStatus.SILENT:
                    ActiveCall.Status = eCodecCallStatus.Unknown;
                    break;
                case TesiraDspDialer.ECallStatus.DIALING:
                    ActiveCall.Status = eCodecCallStatus.Connecting;
                    ActiveCall.Direction = eCodecCallDirection.Outgoing;
                    break;
                case TesiraDspDialer.ECallStatus.RINGBACK:
                    ActiveCall.Status = eCodecCallStatus.Connecting;
                    break;
                case TesiraDspDialer.ECallStatus.RINGING:
                    ActiveCall.Status = eCodecCallStatus.Ringing;
                    ActiveCall.Direction = eCodecCallDirection.Incoming;
                    break;
                case TesiraDspDialer.ECallStatus.BUSY:
                    ActiveCall.Status = eCodecCallStatus.Disconnecting;
                    break;
                case TesiraDspDialer.ECallStatus.REJECT:
                    ActiveCall.Status = eCodecCallStatus.Disconnecting;
                    break;
                case TesiraDspDialer.ECallStatus.INVALID_NUMBER:
                    ActiveCall.Status = eCodecCallStatus.Disconnecting;
                    break;
                case TesiraDspDialer.ECallStatus.ACTIVE:
                    ActiveCall.Status = eCodecCallStatus.Connected;
                    break;
                case TesiraDspDialer.ECallStatus.ACTIVE_MUTED:
                    ActiveCall.Status = eCodecCallStatus.Connected;
                    break;
                case TesiraDspDialer.ECallStatus.ON_HOLD:
                    ActiveCall.Status = eCodecCallStatus.OnHold;
                    break;
                case TesiraDspDialer.ECallStatus.WAITING_RING:
                    ActiveCall.Status = eCodecCallStatus.Connected;
                    break;
                case TesiraDspDialer.ECallStatus.CONF_ACTIVE:
                    ActiveCall.Status = eCodecCallStatus.Connected;
                    break;
                case TesiraDspDialer.ECallStatus.CONF_HOLD:
                    ActiveCall.Status = eCodecCallStatus.OnHold;
                    break;
                default:
                    ActiveCall.Status = eCodecCallStatus.Unknown;
                    break;
            }

            _line.OnAppearanceCallStatusChanged(this);
        }

        /// <summary>
        /// Updates the hook status from a line-level hookState subscription (POTS).
        /// </summary>
        /// <param name="offHook">True when the line is off hook.</param>
        public void SetOffHookFromHookState(bool offHook)
        {
            OffHookStatus = offHook;
        }

        /// <summary>
        /// Sets the dial string for this appearance.
        /// </summary>
        /// <param name="data">Dial string value.</param>
        public void SetDialString(string data)
        {
            DialString = data;
        }

        /// <summary>
        /// Dials the current dial string for this appearance.
        /// </summary>
        public void Dial()
        {
            if (_line.IsVoip)
            {
                if (!_line.LineReady)
                {
                    _line.LogDebug("Dial ignored - VoIP line {line} is not ready", _line.LineNumber);
                    return;
                }
                if (OffHookStatus)
                {
                    _line.SendFullCommand(null, "end", null, 2, AppearanceNumber);
                    if (_line.ClearOnHangup) DialString = string.Empty;
                    return;
                }

                if (!string.IsNullOrEmpty(DialString))
                {
                    _line.SendFullCommand(null, "dial", DialString, 2, AppearanceNumber);
                }
                return;
            }

            if (OffHookStatus)
            {
                OnHook();
                if (_line.ClearOnHangup) DialString = string.Empty;
                return;
            }

            if (!string.IsNullOrEmpty(DialString))
            {
                _line.SendFullCommand(null, "dial", DialString, 1, AppearanceNumber);
                return;
            }

            _line.SendFullCommand(null, "OFFHOOK", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Dials a specific number on this appearance.
        /// </summary>
        /// <param name="number">Number to dial.</param>
        public void Dial(string number)
        {
            SetDialString(number);
            Dial();
        }

        /// <summary>
        /// Places the call on hook / ends the call.
        /// </summary>
        public void OnHook()
        {
            if (_line.IsVoip)
            {
                _line.SendFullCommand(null, "end", null, 2, AppearanceNumber);
                return;
            }
            _line.SendFullCommand("set", "hookState", "ONHOOK", 2, AppearanceNumber);
        }

        /// <summary>
        /// Takes the call off hook / answers.
        /// </summary>
        public void OffHook()
        {
            _line.SendFullCommand("offHook", "offHook", "", 1, AppearanceNumber);
        }

        /// <summary>
        /// Answers an incoming call.
        /// </summary>
        public void Answer()
        {
            if (!_line.IsVoip) return;
            _line.SendFullCommand(null, "answer", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Ends the call on this appearance.
        /// </summary>
        public void End()
        {
            OnHook();
        }

        /// <summary>
        /// Holds the call on this appearance.
        /// </summary>
        public void HoldCall()
        {
            _line.SendFullCommand("set", "hold", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Resumes the call on this appearance.
        /// </summary>
        public void ResumeCall()
        {
            _line.SendFullCommand("set", "resume", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Toggles hold/resume on this appearance.
        /// </summary>
        public void HoldToggle()
        {
            if (CallStatusEnum != TesiraDspDialer.ECallStatus.ON_HOLD)
            {
                ResumeCall();
                return;
            }
            HoldCall();
        }

        /// <summary>
        /// Joins this call appearance into the line's local conference. Conferencing is supported on
        /// VoIP lines only; the request is ignored for POTS lines.
        /// </summary>
        public void Conference()
        {
            if (!_line.IsVoip)
            {
                _line.LogDebug("Conference ignored - line {line} is not a VoIP line", _line.LineNumber);
                return;
            }
            _line.SendFullCommand(null, "lconf", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Removes this call appearance from the line's local conference. Conferencing is supported on
        /// VoIP lines only; the request is ignored for POTS lines.
        /// </summary>
        public void LeaveConference()
        {
            if (!_line.IsVoip)
            {
                _line.LogDebug("LeaveConference ignored - line {line} is not a VoIP line", _line.LineNumber);
                return;
            }
            _line.SendFullCommand(null, "leaveConf", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Redials the last number dialed on this appearance.
        /// </summary>
        public void Redial()
        {
            if (_line.IsVoip && !_line.LineReady)
            {
                _line.LogDebug("Redial ignored - VoIP line {line} is not ready", _line.LineNumber);
                return;
            }
            _line.SendFullCommand(null, "redial", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Sends a hook-flash on this appearance. Hook-flash is an analog (POTS) feature; the request is
        /// ignored on VoIP lines.
        /// </summary>
        public void Flash()
        {
            if (_line.IsVoip)
            {
                _line.LogDebug("Flash ignored - line {line} is a VoIP line", _line.LineNumber);
                return;
            }
            _line.SendFullCommand(null, "flash", null, 1, AppearanceNumber);
        }

        /// <summary>
        /// Sends a DTMF digit on this appearance's line. DTMF is a line-level command on the Tesira
        /// dialer (it is not qualified by call appearance).
        /// </summary>
        /// <param name="digit">Digit to send.</param>
        public void SendDtmf(string digit)
        {
            _line.SendFullCommand(null, "dtmf", digit, 1);
        }

        /// <summary>
        /// Sends a keypad press. Builds the dial string when on hook; sends DTMF when off hook.
        /// </summary>
        /// <param name="data">Keypad key.</param>
        public void SendKeypad(TesiraDspDialer.EKeypadKeys data)
        {
            if (!OffHookStatus)
            {
                switch (data)
                {
                    case TesiraDspDialer.EKeypadKeys.Num0: DialString += "0"; break;
                    case TesiraDspDialer.EKeypadKeys.Num1: DialString += "1"; break;
                    case TesiraDspDialer.EKeypadKeys.Num2: DialString += "2"; break;
                    case TesiraDspDialer.EKeypadKeys.Num3: DialString += "3"; break;
                    case TesiraDspDialer.EKeypadKeys.Num4: DialString += "4"; break;
                    case TesiraDspDialer.EKeypadKeys.Num5: DialString += "5"; break;
                    case TesiraDspDialer.EKeypadKeys.Num6: DialString += "6"; break;
                    case TesiraDspDialer.EKeypadKeys.Num7: DialString += "7"; break;
                    case TesiraDspDialer.EKeypadKeys.Num8: DialString += "8"; break;
                    case TesiraDspDialer.EKeypadKeys.Num9: DialString += "9"; break;
                    case TesiraDspDialer.EKeypadKeys.Star: DialString += "*"; break;
                    case TesiraDspDialer.EKeypadKeys.Pound: DialString += "#"; break;
                    case TesiraDspDialer.EKeypadKeys.Clear: DialString = string.Empty; break;
                    case TesiraDspDialer.EKeypadKeys.Backspace:
                        if (!string.IsNullOrEmpty(DialString))
                            DialString = DialString.Remove(DialString.Length - 1);
                        break;
                }
                return;
            }

            // Off hook: send DTMF, optionally appending to the dial string.
            switch (data)
            {
                case TesiraDspDialer.EKeypadKeys.Num0: SendDtmf("0"); AppendDtmf("0"); break;
                case TesiraDspDialer.EKeypadKeys.Num1: SendDtmf("1"); AppendDtmf("1"); break;
                case TesiraDspDialer.EKeypadKeys.Num2: SendDtmf("2"); AppendDtmf("2"); break;
                case TesiraDspDialer.EKeypadKeys.Num3: SendDtmf("3"); AppendDtmf("3"); break;
                case TesiraDspDialer.EKeypadKeys.Num4: SendDtmf("4"); AppendDtmf("4"); break;
                case TesiraDspDialer.EKeypadKeys.Num5: SendDtmf("5"); AppendDtmf("5"); break;
                case TesiraDspDialer.EKeypadKeys.Num6: SendDtmf("6"); AppendDtmf("6"); break;
                case TesiraDspDialer.EKeypadKeys.Num7: SendDtmf("7"); AppendDtmf("7"); break;
                case TesiraDspDialer.EKeypadKeys.Num8: SendDtmf("8"); AppendDtmf("8"); break;
                case TesiraDspDialer.EKeypadKeys.Num9: SendDtmf("9"); AppendDtmf("9"); break;
                case TesiraDspDialer.EKeypadKeys.Star: SendDtmf("*"); AppendDtmf("*"); break;
                case TesiraDspDialer.EKeypadKeys.Pound: SendDtmf("#"); AppendDtmf("#"); break;
                case TesiraDspDialer.EKeypadKeys.Clear: break;
                case TesiraDspDialer.EKeypadKeys.Backspace: break;
            }
        }

        private void AppendDtmf(string digit)
        {
            if (_line.AppendDtmf)
                DialString += digit;
        }
    }
}
