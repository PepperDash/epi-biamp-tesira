using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;

namespace Tesira_DSP_EPI
{
    public class TesiraDspRoomCombiner : TesiraDspControlPoint, IBasicVolumeWithFeedback, IKeyed
    {
        private bool _OutIsMuted;
        protected bool OutIsMuted
        {
            get
            {
                return _OutIsMuted;
            }
            set
            {
                _OutIsMuted = value;
                MuteFeedback.FireUpdate();
            }
        }

        private int _OutVolumeLevel;
        protected int OutVolumeLevel
        {
            get
            {
                return _OutVolumeLevel;
            }
            set
            {
                _OutVolumeLevel = value;
                VolumeLevelFeedback.FireUpdate();
            }
        }

        private int _RoomGroup;
        protected int RoomGroup
        {
            get
            {
                return _RoomGroup;
            }
            set
            {
                _RoomGroup = value;
                RoomGroupFeedback.FireUpdate();
            }
        }



        public int Permissions { get; set; }

        public int ControlType;

        public BoolFeedback MuteFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
        public IntFeedback RoomGroupFeedback { get; private set; }

        public string IncrementAmount { get; set; }
        public bool UseAbsoluteValue { get; set; }
        public string LevelControlPointTag { get { return base.InstanceTag1; } }
        CTimer _VolumeUpRepeatTimer;
        CTimer _VolumeDownRepeatTimer;
        CTimer _VolumeUpRepeatDelayTimer;
        CTimer _VolumeDownRepeatDelayTimer;

        CTimer _PollTimer;

        bool VolDownPressTracker;
        bool VolUpPressTracker;

        /// <summary>
        /// Used to identify level subscription values
        /// </summary>
        public string LevelCustomName { get; private set; }

        private double _MinLevel;
        /// <summary>
        /// Minimum fader level
        /// </summary>
        double MinLevel
        {
            get
            {
                return _MinLevel;
            }
            set
            {
                _MinLevel = value;
                SendFullCommand("get", "levelOutMax", null, 1);
            }
        }

        private double _MaxLevel;
        /// <summary>
        /// Maximum fader level
        /// </summary>
        double MaxLevel
        {
            get
            {
                return _MaxLevel;
            }
            set
            {
                _MaxLevel = value;
                //LevelSubscribed = true;
                SendSubscriptionCommand(LevelCustomName, "levelOut", 250, 1);
            }
        }

        /// <summary>
        /// Checks if a valid subscription string has been recieved for all subscriptions
        /// </summary>
        public override bool IsSubscribed
        {
            get
            {
                bool isSubscribed = true;

                if (HasLevel && !LevelIsSubscribed)
                    isSubscribed = false;

                return isSubscribed;
            }
            protected set { }
        }

        public bool AutomaticUnmuteOnVolumeUp { get; private set; }

        public bool HasMute { get; private set; }

        public bool HasLevel { get; private set; }
        bool LevelIsSubscribed;

        public TesiraDspRoomCombiner(string key, TesiraRoomCombinerBlockConfig config, TesiraDsp parent)
            : base(config.roomCombinerInstanceTag, "", config.roomIndex, 0, parent)
        {
            Initialize(key, config);
        }

        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="key">key of the control</param>
        /// <param name="label">friendly name of the control</param>
        /// <param name="hasMute">defines if the control has a mute</param>
        /// <param name="hasLevel">defines if the control has a level</param>
        public void Initialize(string key, TesiraRoomCombinerBlockConfig config)
        {
            Key = string.Format("{0}-{1}", Parent.Key, key);

            if (config.enabled)
            {
                DeviceManager.AddDevice(this);
            }

            Debug.Console(2, this, "Adding RoomCombiner '{0}'", Key);

            IsSubscribed = false;

            MuteFeedback = new BoolFeedback(() => OutIsMuted);
            VolumeLevelFeedback = new IntFeedback(() => OutVolumeLevel);
            RoomGroupFeedback = new IntFeedback(() => RoomGroup);


            Label = config.label;
            HasMute = config.hasMute;
            HasLevel = config.hasLevel;
            UseAbsoluteValue = config.useAbsoluteValue;
            Enabled = config.enabled;
            Permissions = config.permissions;
            IncrementAmount = config.incrementAmount;
            AutomaticUnmuteOnVolumeUp = config.unmuteOnVolChange;
            _VolumeUpRepeatTimer = new CTimer((o) => VolumeUpRepeat(), Timeout.Infinite);
            _VolumeDownRepeatTimer = new CTimer((o) => VolumeDownRepeat(), Timeout.Infinite);
            _VolumeUpRepeatDelayTimer = new CTimer((o) => VolumeUpRepeatDelay(), Timeout.Infinite);
            _VolumeDownRepeatDelayTimer = new CTimer((o) => VolumeDownRepeatDelay(), Timeout.Infinite);

            _PollTimer = new CTimer((o) => DoPoll(), Timeout.Infinite);


            if (HasMute && HasLevel)
            {
                ControlType = 0;
            }
            else if (!HasMute && HasLevel)
            {
                ControlType = 1;
            }

            else if (HasMute && !HasLevel)
            {
                ControlType = 2;
            }
        }

        public void VolumeUpRepeat()
        {
            if (VolUpPressTracker)
                this.VolumeUp(true);
        }
        public void VolumeDownRepeat()
        {
            if (VolDownPressTracker)
                this.VolumeDown(true);
        }

        public void VolumeUpRepeatDelay()
        {
            VolUpPressTracker = true;
            this.VolumeUp(true);
        }
        public void VolumeDownRepeatDelay()
        {
            VolDownPressTracker = true;
            this.VolumeDown(true);
        }

        public override void Subscribe()
        {
            //Subsribe to Level
            if (this.HasLevel)
            {
                LevelCustomName = string.Format("{0}~roomCombiner{1}", this.InstanceTag1, this.Index1);
                SendFullCommand("get", "levelOutMin", null, 1);
            }
            SendFullCommand("get", "group", null, 1);
        }

        public override void Unsubscribe()
        {
            if (this.HasLevel)
            {
                LevelCustomName = string.Format("{0}~roomCombiner{1}", this.InstanceTag1, this.Index1);
                SendUnSubscriptionCommand(LevelCustomName, "levelOut", 1);
            }
        }

        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        public void ParseSubscriptionMessage(string customName, string value)
        {
            if (this.HasLevel && customName == LevelCustomName)
            {
                var _value = Double.Parse(value);

                OutVolumeLevel = (ushort)Scale(_value, MinLevel, MaxLevel, 0, 65535);

                LevelIsSubscribed = true;

                _PollTimer.Reset(30000);
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
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message
                string pattern = "[^ ]* (.*)";

                Match match = Regex.Match(message, pattern);

                if (match.Success)
                {

                    string value = match.Groups[1].Value;

                    Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                    if (message.IndexOf("+OK") > -1)
                    {
                        switch (attributeCode)
                        {
                            case "levelOutMin" :
                                {
                                    MinLevel = Double.Parse(value);
                                    Debug.Console(1, this, "MinLevel is '{0}'", MinLevel);
                                    break;
                                }
                            case "levelOutMax" :
                                {
                                    MaxLevel = Double.Parse(value);
                                    Debug.Console(1, this, "MaxLevel is '{0}'", MaxLevel);
                                    break;
                                }
                            case "muteOut" :
                                {
                                    OutIsMuted = bool.Parse(value);
                                    Debug.Console(1, this, "MuteState is '{0}'", value);
                                    _PollTimer.Reset(30000);
                                    break;
                                }

                            case "group":
                                {
                                    RoomGroup = int.Parse(value);
                                    Debug.Console(1, this, "Room Group is '{0}'", value);
                                    _PollTimer.Reset(30000);
                                    break;
                                }
                            default:
                                {
                                    Debug.Console(2, "Response does not match expected attribute codes: '{0}'", message);
                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
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
            Debug.Console(1, this, "volume: {0}", level);
            // Unmute volume if new level is higher than existing
            if (level > _OutVolumeLevel && AutomaticUnmuteOnVolumeUp)
                if (_OutIsMuted)
                    MuteOff();

            double volumeLevel = Scale(level, 0, 65535, MinLevel, MaxLevel);

            SendFullCommand("set", "levelOut", string.Format("{0:0.000000}", volumeLevel), 1);
        }

        /// <summary>
        /// Set the room group to the specified value
        /// </summary>
        /// <param name="group"></param>
        public void SetRoomGroup(ushort group)
        {
            Debug.Console(1, this, "group: {0}", group);
            SendFullCommand("set", "group", Convert.ToString(group), 1);
        }

        /// <summary>
        /// Polls all data for this control
        /// </summary>
        public override void DoPoll()
        {
            if (this.HasLevel)
            {
                GetVolume();
            }

            if (this.HasMute)
            {
                GetMute();
            }

            GetRoomGroup();
        }

        /// <summary>
        /// Polls the current volume level
        /// </summary>
        public void GetVolume()
        {
            SendFullCommand("get", "levelOut", String.Empty, 1);
        }

        /// <summary>
        /// Polls the current mute state
        /// </summary>
        public void GetMute()
        {
            SendFullCommand("get", "muteOut", String.Empty, 1);
        }

        /// <summary>
        /// Polls the current room group
        /// </summary>
        public void GetRoomGroup()
        {
            SendFullCommand("get", "group", String.Empty, 1);
        }

        /// <summary>
        /// Toggles mute status
        /// </summary>
        public void MuteToggle()
        {
            SendFullCommand("toggle", "muteOut", String.Empty, 1);
        }

        /// <summary>
        /// Decrements volume level
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeDown(bool press)
        {
            Debug.Console(2, "VolumeDown Sent for {0}", LevelControlPointTag);
            if (press)
            {
                if (VolDownPressTracker)
                {
                    _VolumeDownRepeatTimer.Reset(100);
                    SendFullCommand("decrement", "levelOut", IncrementAmount, 1);
                }
                else if (!VolDownPressTracker)
                {
                    _VolumeDownRepeatDelayTimer.Reset(750);
                    SendFullCommand("decrement", "levelOut", IncrementAmount, 1);
                }

            }
            if (!press)
            {
                VolDownPressTracker = false;
                _VolumeDownRepeatTimer.Stop();
                _VolumeDownRepeatDelayTimer.Stop();
            }
        }

        /// <summary>
        /// Increments volume level
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeUp(bool press)
        {
            Debug.Console(2, "VolumeUp Sent for {0}", LevelControlPointTag);

            if (press)
            {
                if (VolUpPressTracker)
                {
                    _VolumeUpRepeatTimer.Reset(100);
                    SendFullCommand("increment", "levelOut", IncrementAmount, 1);
                }
                else if (!VolUpPressTracker)
                {
                    _VolumeUpRepeatDelayTimer.Reset(750);
                    SendFullCommand("increment", "levelOut", IncrementAmount, 1);
                    if (AutomaticUnmuteOnVolumeUp)
                    {
                        if (_OutIsMuted)
                        {
                            MuteOff();
                        }
                    }
                }
            }
            if (!press)
            {
                VolUpPressTracker = false;
                _VolumeUpRepeatTimer.Stop();
                _VolumeUpRepeatDelayTimer.Stop();
            }
        }



        /// <summary>
        /// Scales the input from the input range to the output range
        /// </summary>
        /// <param name="input"></param>
        /// <param name="inMin"></param>
        /// <param name="inMax"></param>
        /// <param name="outMin"></param>
        /// <param name="outMax"></param>
        /// <returns></returns>
        double Scale(double input, double inMin, double inMax, double outMin, double outMax)
        {
            Debug.Console(1, this, "Scaling (double) input '{0}' with min '{1}'/max '{2}' to output range min '{3}'/max '{4}'", input, inMin, inMax, outMin, outMax);

            double inputRange = inMax - inMin;

            if (inputRange <= 0)
            {
                throw new ArithmeticException(string.Format("Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax));
            }

            double outputRange = outMax - outMin;

            var output = (((input - inMin) * outputRange) / inputRange) + outMin;

            Debug.Console(1, this, "Scaled output '{0}'", output);

            return output;
        }
    }
}