using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;

namespace Tesira_DSP_EPI {
    public class TesiraDspLevelControl : TesiraDspControlPoint, IBasicVolumeWithFeedback, IKeyed {
        bool _IsMuted;
        ushort _VolumeLevel;
        public int Permissions { get; set; }

        public int ControlType { get; set; }

        public BoolFeedback MuteFeedback { get; private set; }

        public IntFeedback VolumeLevelFeedback { get; private set; }

        public string IncrementAmount { get; set; }
        public bool UseAbsoluteValue { get; set; }
        public ePdtLevelTypes Type;
        public string LevelControlPointTag { get { return base.InstanceTag1; } }
        CTimer VolumeUpRepeatTimer;
        CTimer VolumeDownRepeatTimer;
        CTimer VolumeUpRepeatDelayTimer;
        CTimer VolumeDownRepeatDelayTimer;

        bool VolDownPressTracker;
        bool VolUpPressTracker;

        /// <summary>
        /// Used to identify level subscription values
        /// </summary>
        public string LevelCustomName { get; private set; }

        /// <summary>
        /// Used to identify mute subscription value
        /// </summary>
        public string MuteCustomName { get; private set; }

        private double _MinLevel { get; set; }
        /// <summary>
        /// Minimum fader level
        /// </summary>
        double MinLevel {
            get {
                return _MinLevel;
            }
            set {
                _MinLevel = value;
                SendFullCommand("get", "maxLevel", null, 1);
            }
        }

        private double _MaxLevel { get; set; }
        /// <summary>
        /// Maximum fader level
        /// </summary>
        double MaxLevel {
            get {
                return _MaxLevel;
            }
            set {
                _MaxLevel = value;
                SendSubscriptionCommand(LevelCustomName, "level", 250, 1);
            }
        }

        /// <summary>
        /// Checks if a valid subscription string has been recieved for all subscriptions
        /// </summary>
        public override bool IsSubscribed {
            get {
                bool isSubscribed = true;

                if (HasMute && !MuteIsSubscribed)
                    isSubscribed = false;

                if (HasLevel && !LevelIsSubscribed)
                    isSubscribed = false;

                return isSubscribed;
            }
            protected set { }
        }

        public bool AutomaticUnmuteOnVolumeUp { get; private set; }

        public bool HasMute { get; private set; }

        public bool HasLevel { get; private set; }

        bool MuteIsSubscribed;

        bool LevelIsSubscribed;

        public TesiraDspLevelControl(string key, TesiraLevelControlBlockConfig config, TesiraDsp parent)
            : base(config.levelInstanceTag, config.muteInstanceTag, config.index1, config.index2, parent) {

            Initialize(key, config);

        }

        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="key">key of the control</param>
        /// <param name="label">friendly name of the control</param>
        /// <param name="hasMute">defines if the control has a mute</param>
        /// <param name="hasLevel">defines if the control has a level</param>
        public void Initialize(string key, TesiraLevelControlBlockConfig config) {
            Key = string.Format("{0}--{1}", Parent.Key, key);

            DeviceManager.AddDevice(this);

            this.Type = config.isMic ? ePdtLevelTypes.microphone : ePdtLevelTypes.speaker;

            Debug.Console(2, this, "Adding LevelControl '{0}'", Key);

            IsSubscribed = false;

            MuteFeedback = new BoolFeedback(() => _IsMuted);

            VolumeLevelFeedback = new IntFeedback(() => _VolumeLevel);

            Label = config.label;
            HasMute = config.hasMute;
            HasLevel = config.hasLevel;
            UseAbsoluteValue = config.useAbsoluteValue;
            Enabled = config.enabled;
            Permissions = config.permissions;
            IncrementAmount = config.incrementAmount;
            AutomaticUnmuteOnVolumeUp = config.unmuteOnVolChange;
            VolumeUpRepeatTimer = new CTimer(VolumeUpRepeat, Timeout.Infinite);
            VolumeDownRepeatTimer = new CTimer(VolumeDownRepeat, Timeout.Infinite);
            VolumeUpRepeatDelayTimer = new CTimer(VolumeUpRepeatDelay, Timeout.Infinite);
            VolumeDownRepeatDelayTimer = new CTimer(VolumeDownRepeatDelay, Timeout.Infinite);

            if (HasMute && HasLevel) {
                ControlType = 0;
            }
            else if (!HasMute && HasLevel) {
                ControlType = 1;
            }

            else if (HasMute && !HasLevel) {
                ControlType = 2;
            }

            
        }

        public void VolumeUpRepeat(object callbackObject) {
            if(VolUpPressTracker)
                this.VolumeUp(true);
        }
        public void VolumeDownRepeat(object callbackObject) {
            if(VolDownPressTracker)
                this.VolumeDown(true);
        }

        public void VolumeUpRepeatDelay(object callbackObject) {
            VolUpPressTracker = true;
            this.VolumeUp(true);
        }
        public void VolumeDownRepeatDelay(object callbackObject) {
            VolDownPressTracker = true;
            this.VolumeDown(true);
        }

        public override void Subscribe() {
            //Subscribe to Level
            if (this.HasLevel) {
                // MUST use InstanceTag1 for levels, it is the first instance tag in the JSON config
                LevelCustomName = string.Format("{0}~level{1}", this.InstanceTag1, this.Index1);
                SendFullCommand("get", "minLevel", null, 1);
            }

            //Subscribe to Mute
            if (this.HasMute) {
				// MUST use InstanceTag2 for mute, it is the second instance tag in the JSON config
                MuteCustomName = string.Format("{0}~mute{1}", this.InstanceTag2, this.Index1);

                SendSubscriptionCommand(MuteCustomName, "mute", 500, 2);
            }

			
        }

        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        public void ParseSubscriptionMessage(string customName, string value) {

            if (this.HasMute && customName == MuteCustomName) {
                //if (value.IndexOf("+OK") > -1)
                //{
                //    int pointer = value.IndexOf(" +OK");

                //    MuteIsSubscribed = true;

                //    // Removes the +OK
                //    value = value.Substring(0, value.Length - (value.Length - (pointer - 1)));
                //}

                _IsMuted = bool.Parse(value);
                MuteIsSubscribed = true;
                

                MuteFeedback.FireUpdate();
            }
            else if (this.HasLevel && customName == LevelCustomName) {


                var _value = Double.Parse(value);

                _VolumeLevel = (ushort)Scale(_value, MinLevel, MaxLevel, 0, 65535);

                LevelIsSubscribed = true;

                VolumeLevelFeedback.FireUpdate();
            }

        }

        /// <summary>
        /// Parses a non subscription response
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command</param>
        /// <param name="message">The message to parse</param>
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
                            case "minLevel": {
                                    MinLevel = Double.Parse(value);

                                    Debug.Console(1, this, "MinLevel is '{0}'", MinLevel);

                                    break;
                                }
                            case "maxLevel": {
                                    MaxLevel = Double.Parse(value);

                                    Debug.Console(1, this, "MaxLevel is '{0}'", MaxLevel);

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

        /// <summary>
        /// Turns the mute off
        /// </summary>
        public void MuteOff() {
            SendFullCommand("set", "mute", "false", 2);
        }

        /// <summary>
        /// Turns the mute on
        /// </summary>
        public void MuteOn() {
            SendFullCommand("set", "mute", "true", 2);
        }

        /// <summary>
        /// Sets the volume to a specified level
        /// </summary>
        /// <param name="level"></param>
        public void SetVolume(ushort level) {
            Debug.Console(1, this, "volume: {0}", level);
            // Unmute volume if new level is higher than existing
            if (level > _VolumeLevel && AutomaticUnmuteOnVolumeUp)
                if (_IsMuted)
                    MuteOff();

            double volumeLevel = Scale(level, 0, 65535, MinLevel, MaxLevel);

            SendFullCommand("set", "level", string.Format("{0:0.000000}", volumeLevel), 1);
        }

        /// <summary>
        /// Polls all data for this control
        /// </summary>
        public override void DoPoll() {
            if (this.HasLevel) {
                GetVolume();
            }
            if (this.HasMute) {
                GetMute();
            }
        }

        /// <summary>
        /// Polls the current volume level
        /// </summary>
        public void GetVolume() {
            SendFullCommand("get", "level", String.Empty, 1);
        }

        public void GetMute() {
            SendFullCommand("get", "mute", String.Empty, 2);
        }

        /// <summary>
        /// Toggles mute status
        /// </summary>
        public void MuteToggle() {
            SendFullCommand("toggle", "mute", String.Empty, 2);
        }

        /// <summary>
        /// Decrements volume level
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeDown(bool press) {
            Debug.Console(2, "VolumeDown Sent for {0}", LevelControlPointTag);
            if (press) {
                if (VolDownPressTracker) {
                    VolumeDownRepeatTimer.Reset(100);
                    SendFullCommand("decrement", "level", IncrementAmount, 1);
                }
                else if (!VolDownPressTracker) {
                    VolumeDownRepeatDelayTimer.Reset(750);
                    SendFullCommand("decrement", "level", IncrementAmount, 1);
                }
                
            }
            if(!press) {
                VolDownPressTracker = false;
                VolumeDownRepeatTimer.Stop();
                VolumeDownRepeatDelayTimer.Stop();
            }
        }

        /// <summary>
        /// Increments volume level
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeUp(bool press) {
            Debug.Console(2, "VolumeUp Sent for {0}", LevelControlPointTag);

            if (press) {
                if (VolUpPressTracker) {
                    VolumeUpRepeatTimer.Reset(100);
                    SendFullCommand("increment", "level", IncrementAmount, 1);
                }
                else if(!VolUpPressTracker){
                    VolumeUpRepeatDelayTimer.Reset(750);
                    SendFullCommand("increment", "level", IncrementAmount, 1);
                    if (AutomaticUnmuteOnVolumeUp) {
                        if (_IsMuted) {
                            MuteOff();
                        }
                    }
                }      
            }
            if (!press) {
                VolUpPressTracker = false;
                VolumeUpRepeatTimer.Stop();
                VolumeUpRepeatDelayTimer.Stop();
            }

            

        }

        ///// <summary>
        ///// Scales the input from the input range to the output range
        ///// </summary>
        ///// <param name="input"></param>
        ///// <param name="inMin"></param>
        ///// <param name="inMax"></param>
        ///// <param name="outMin"></param>
        ///// <param name="outMax"></param>
        ///// <returns></returns>
        //int Scale(int input, int inMin, int inMax, int outMin, int outMax)
        //{
        //    Debug.Console(1, this, "Scaling (int) input '{0}' with min '{1}'/max '{2}' to output range min '{3}'/max '{4}'", input, inMin, inMax, outMin, outMax);

        //    int inputRange = inMax - inMin;

        //    int outputRange = outMax - outMin;

        //    var output = (((input-inMin) * outputRange) / inputRange ) - outMin;

        //    Debug.Console(1, this, "Scaled output '{0}'", output);

        //    return output;
        //}

        /// <summary>
        /// Scales the input from the input range to the output range
        /// </summary>
        /// <param name="input"></param>
        /// <param name="inMin"></param>
        /// <param name="inMax"></param>
        /// <param name="outMin"></param>
        /// <param name="outMax"></param>
        /// <returns></returns>
        double Scale(double input, double inMin, double inMax, double outMin, double outMax) {
            Debug.Console(1, this, "Scaling (double) input '{0}' with min '{1}'/max '{2}' to output range min '{3}'/max '{4}'", input, inMin, inMax, outMin, outMax);

            double inputRange = inMax - inMin;

            if (inputRange <= 0) {
                throw new ArithmeticException(string.Format("Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax));
            }

            double outputRange = outMax - outMin;

            var output = (((input - inMin) * outputRange) / inputRange) + outMin;

            Debug.Console(1, this, "Scaled output '{0}'", output);

            return output;
        }

        public enum ePdtLevelTypes {
            speaker = 0,
            microphone = 1
        }
    }
}