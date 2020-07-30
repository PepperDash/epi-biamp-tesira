using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;

namespace Tesira_DSP_EPI
{
    public class TesiraDspFaderControl : TesiraDspControlPoint, IBasicVolumeWithFeedback
    {
        private bool _isMuted;
        protected bool IsMuted
        {
            get
            {
                return _isMuted;
            }
            set
            {
                _isMuted = value;
                MuteFeedback.FireUpdate();
            }
        }
        private int _volumeLevel;
        protected int VolumeLevel
        {
            get
            {
                return _volumeLevel;
            }
            set
            {
                _volumeLevel = value;
                VolumeLevelFeedback.FireUpdate();
            }
        }

        private const string KeyFormatter = "{0}--{1}";

        private int Permissions { get; set; }
        private int ControlType { get; set; }

        private string IncrementAmount { get; set; }
        private bool UseAbsoluteValue { get; set; }
        private ePdtLevelTypes _type;
        private string LevelControlPointTag { get { return InstanceTag1; } }

        public BoolFeedback MuteFeedback { get; private set; }
        public BoolFeedback VisibleFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
        public IntFeedback TypeFeedback { get; private set; }
        public IntFeedback ControlTypeFeedback { get; private set; }
        public IntFeedback PermissionsFeedback { get; private set; }


        
        CTimer _volumeUpRepeatTimer;
        CTimer _volumeDownRepeatTimer;
        CTimer _volumeUpRepeatDelayTimer;
        CTimer _volumeDownRepeatDelayTimer;

        //private bool LevelSubscribed { get; set; }

        bool _volDownPressTracker;
        bool _volUpPressTracker;

        /// <summary>
        /// Used to identify level subscription values
        /// </summary>
        public string LevelCustomName { get; protected set; }

        /// <summary>
        /// Used to identify mute subscription value
        /// </summary>
        public string MuteCustomName { get; protected set; }

        private double _minLevel;
        /// <summary>
        /// Minimum fader level
        /// </summary>
        double MinLevel
        {
            get
            {
                return _minLevel;
            }
            set
            {
                _minLevel = value;
                SendFullCommand("get", "maxLevel", null, 1);
            }
        }

        private double _maxLevel;
        /// <summary>
        /// Maximum fader level
        /// </summary>
        double MaxLevel
        {
            get
            {
                return _maxLevel;
            }
            set
            {
                _maxLevel = value;
                //LevelSubscribed = true;
                SendSubscriptionCommand(LevelCustomName, "level", 250, 1);
            }
        }

        /// <summary>
        /// Checks if a valid subscription string has been recieved for all subscriptions
        /// </summary>
        public override bool IsSubscribed
        {
            get
            {
                var isSubscribed = !HasMute && !_muteIsSubscribed;

                if (HasLevel && !_levelIsSubscribed)
                    isSubscribed = false;

                return isSubscribed;
            }
            protected set { }
        }

        private bool AutomaticUnmuteOnVolumeUp { get; set; }

        /// <summary>
        /// Component has Mute
        /// </summary>
        public bool HasMute { get; private set; }

        /// <summary>
        /// Component Has Level
        /// </summary>
        public bool HasLevel { get; private set; }

        private bool _muteIsSubscribed;

        private bool _levelIsSubscribed;

        /// <summary>
        /// Constructor for Component
        /// </summary>
        /// <param name="key">Unique Identifier for component</param>
        /// <param name="config">Config Object of Component</param>
        /// <param name="parent">Parent object of Component</param>
        public TesiraDspFaderControl(string key, TesiraFaderControlBlockConfig config, TesiraDsp parent)
            : base(config.LevelInstanceTag, config.MuteInstanceTag, config.Index1, config.Index2, parent, String.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {

            Initialize(config);

        }

        private void Initialize(TesiraFaderControlBlockConfig config)
        {
            if (config.Enabled)
            {
                DeviceManager.AddDevice(this);
            }

            _type = config.IsMic ? ePdtLevelTypes.Microphone : ePdtLevelTypes.Speaker;

            Debug.Console(2, this, "Adding LevelControl '{0}'", Key);

            IsSubscribed = false;


            HasMute = config.HasMute;
            HasLevel = config.HasLevel;
            UseAbsoluteValue = config.UseAbsoluteValue;
            Enabled = config.Enabled;
            Permissions = config.Permissions;
            IncrementAmount = config.IncrementAmount;
            AutomaticUnmuteOnVolumeUp = config.UnmuteOnVolChange;
            _volumeUpRepeatTimer = new CTimer(VolumeUpRepeat, Timeout.Infinite);
            _volumeDownRepeatTimer = new CTimer(VolumeDownRepeat, Timeout.Infinite);
            _volumeUpRepeatDelayTimer = new CTimer(VolumeUpRepeatDelay, Timeout.Infinite);
            _volumeDownRepeatDelayTimer = new CTimer(VolumeDownRepeatDelay, Timeout.Infinite);

            

            if (HasMute && HasLevel)
            {
                ControlType = 0;
                Debug.Console(2, this, "{0} has BOTH Mute and Level", Key);
            }
            else if (!HasMute && HasLevel)
            {
                ControlType = 1;
                Debug.Console(2, this, "{0} has Level ONLY", Key);

            }

            else if (HasMute && !HasLevel)
            {
                Debug.Console(2, this, "{0} has MUTE ONLY", Key);
                ControlType = 2;
            }

            MuteFeedback = new BoolFeedback(Key + "-MuteFeedback", () => IsMuted);
            VisibleFeedback = new BoolFeedback(Key + "-VisibleFeedback", () => Enabled);

            VolumeLevelFeedback = new IntFeedback(Key + "-LevelFeedback", () => VolumeLevel);
            TypeFeedback = new IntFeedback(Key + "-TypeFeedback", () => (ushort)_type);
            ControlTypeFeedback = new IntFeedback(Key + "-ControlTypeFeedback", () => ControlType);
            PermissionsFeedback = new IntFeedback(Key + "-PermissionsFeedback", () => Permissions);

            Feedbacks.Add(MuteFeedback);
            Feedbacks.Add(VolumeLevelFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(VisibleFeedback);
            Feedbacks.Add(TypeFeedback);
            Feedbacks.Add(ControlTypeFeedback);
            Feedbacks.Add(PermissionsFeedback);

            Parent.Feedbacks.AddRange(Feedbacks);


        }

        private void VolumeUpRepeat(object callbackObject)
        {
            if (_volUpPressTracker)
                VolumeUp(true);
        }
        private void VolumeDownRepeat(object callbackObject)
        {
            if (_volDownPressTracker)
                VolumeDown(true);
        }

        private void VolumeUpRepeatDelay(object callbackObject)
        {
            _volUpPressTracker = true;
            VolumeUp(true);
        }
        private void VolumeDownRepeatDelay(object callbackObject)
        {
            _volDownPressTracker = true;
            VolumeDown(true);
        }

        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe()
        {
            //Subscribe to Mute
            if (HasMute)
            {
                // MUST use InstanceTag2 for mute, it is the second instance tag in the JSON config
                MuteCustomName = string.Format("{0}~mute{1}", InstanceTag2, Index1);


                SendSubscriptionCommand(MuteCustomName, "mute", 500, 2);
            }

            //Subscribe to Level
            if (HasLevel)
            {
                // MUST use InstanceTag1 for levels, it is the first instance tag in the JSON config
                LevelCustomName = string.Format("{0}~level{1}", InstanceTag1, Index1);
                SendFullCommand("get", "minLevel", null, 1);
            }
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            //Subscribe to Mute
            if (HasMute)
            {
                // MUST use InstanceTag2 for mute, it is the second instance tag in the JSON config
                MuteCustomName = string.Format("{0}~mute{1}", InstanceTag2, Index1);


                SendUnSubscriptionCommand(MuteCustomName, "mute", 2);
            }

            //Subscribe to Level
            if (!HasLevel) return;

            // MUST use InstanceTag1 for levels, it is the first instance tag in the JSON config
            LevelCustomName = string.Format("{0}~level{1}", InstanceTag1, Index1);
            SendUnSubscriptionCommand(LevelCustomName, "level", 2);
        }

        /// <summary>
        /// Parses subscription response data for the component
        /// </summary>
        /// <param name="customName">Subscription Identifier for component</param>
        /// <param name="value">Component data to be parsed</param>
        public void ParseSubscriptionMessage(string customName, string value)
        {

            if (HasMute && customName == MuteCustomName)
            {
                IsMuted = bool.Parse(value);
                _muteIsSubscribed = true;
            }
            else if (HasLevel && customName == LevelCustomName)
            {


                var localValue = Double.Parse(value);

                VolumeLevel = (ushort)Scale(localValue, MinLevel, MaxLevel, 0, 65535);

                _levelIsSubscribed = true;
            }

        }

        /// <summary>
        /// Parses non-subscription response data for the component
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command for the parsing algorithm</param>
        /// <param name="message">Component data to be parsed</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message
                const string pattern = "[^ ]* (.*)";

                var match = Regex.Match(message, pattern);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                if (message.IndexOf("+OK", System.StringComparison.Ordinal) <= -1) return;
                switch (attributeCode)
                {
                    case "minLevel":
                    {
                        MinLevel = Double.Parse(value);

                        Debug.Console(1, this, "MinLevel is '{0}'", MinLevel);

                        break;
                    }
                    case "maxLevel":
                    {
                        MaxLevel = Double.Parse(value);

                        Debug.Console(1, this, "MaxLevel is '{0}'", MaxLevel);

                        break;
                    }
                    default:
                    {
                        Debug.Console(2, "Response does not match expected attribute codes: '{0}'", message);

                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
            }

        }

        /// <summary>
        /// Disable Mute
        /// </summary>
        public void MuteOff()
        {
            SendFullCommand("set", "mute", "false", 2);
        }

        /// <summary>
        /// Enable Mute
        /// </summary>
        public void MuteOn()
        {
            SendFullCommand("set", "mute", "true", 2);
        }

        /// <summary>
        /// Set level to specified value
        /// </summary>
        /// <param name="level">Level from 0 - 100, as a percentage of the total range</param>
        public void SetVolume(ushort level)
        {
            Debug.Console(1, this, "volume: {0}", level);
            // Unmute volume if new level is higher than existing
            if (level > _volumeLevel && AutomaticUnmuteOnVolumeUp)
                if (_isMuted)
                    MuteOff();

            var volumeLevel = Scale(level, 0, 65535, MinLevel, MaxLevel);

            SendFullCommand("set", "level", string.Format("{0:0.000000}", volumeLevel), 1);
        }

        /// <summary>
        /// Polls all data for component
        /// </summary>
        public override void DoPoll()
        {
            if (HasLevel)
            {
                GetVolume();
            }
            if (HasMute)
            {
                GetMute();
            }
        }

        /// <summary>
        /// Polls the current volume level of component
        /// </summary>
        public void GetVolume()
        {
            SendFullCommand("get", "level", String.Empty, 1);
        }

        /// <summary>
        /// Polls the current mute state of Component
        /// </summary>
        public void GetMute()
        {
            SendFullCommand("get", "mute", String.Empty, 2);
        }

        /// <summary>
        /// Toggle component mute
        /// </summary>
        public void MuteToggle()
        {
            SendFullCommand("toggle", "mute", String.Empty, 2);
        }

        /// <summary>
        /// Decrements component level
        /// </summary>
        /// <param name="press">Trigger map to bridge or UI component</param>
        public void VolumeDown(bool press)
        {
            Debug.Console(2, "VolumeDown Sent for {0}", LevelControlPointTag);
            if (press)
            {
                if (_volDownPressTracker)
                {
                    _volumeDownRepeatTimer.Reset(100);
                    SendFullCommand("decrement", "level", IncrementAmount, 1);
                }
                else if (!_volDownPressTracker)
                {
                    _volumeDownRepeatDelayTimer.Reset(750);
                    SendFullCommand("decrement", "level", IncrementAmount, 1);
                }
                return;
            }

            _volDownPressTracker = false;
            _volumeDownRepeatTimer.Stop();
            _volumeDownRepeatDelayTimer.Stop();
        }

        /// <summary>
        /// Increments component level
        /// </summary>
        /// <param name="press">Trigger map to bridge or UI component</param>
        public void VolumeUp(bool press)
        {
            Debug.Console(2, "VolumeUp Sent for {0}", LevelControlPointTag);

            if (press)
            {
                if (_volUpPressTracker)
                {
                    _volumeUpRepeatTimer.Reset(100);
                    SendFullCommand("increment", "level", IncrementAmount, 1);
                }
                else if (!_volUpPressTracker)
                {
                    _volumeUpRepeatDelayTimer.Reset(750);
                    SendFullCommand("increment", "level", IncrementAmount, 1);
                    if (!AutomaticUnmuteOnVolumeUp) return;

                    if (_isMuted)
                    {
                        MuteOff();
                    }
                }
                return;
            }

            _volUpPressTracker = false;
            _volumeUpRepeatTimer.Stop();
            _volumeUpRepeatDelayTimer.Stop();
        }

        /// <summary>
        /// Scales two relative values given two sets of relative ranges
        /// </summary>
        /// <param name="input">Relative Input Value</param>
        /// <param name="inMin">Minimum Input Value</param>
        /// <param name="inMax">Maximum Input Value</param>
        /// <param name="outMin">Minimum Output Value</param>
        /// <param name="outMax">Maximum Output Value</param>
        /// <returns>Relative output value</returns>
        double Scale(double input, double inMin, double inMax, double outMin, double outMax)
        {
            Debug.Console(1, this, "Scaling (double) input '{0}' with min '{1}'/max '{2}' to output range min '{3}'/max '{4}'", input, inMin, inMax, outMin, outMax);

            var inputRange = inMax - inMin;

            if (inputRange <= 0)
            {
                throw new ArithmeticException(string.Format("Invalid Input Range '{0}' for Scaling.  Min '{1}' Max '{2}'.", inputRange, inMin, inMax));
            }

            var outputRange = outMax - outMin;

            var output = (((input - inMin) * outputRange) / inputRange) + outMin;

            Debug.Console(1, this, "Scaled output '{0}'", output);

            return output;
        }

        /// <summary>
        /// Possible LevelType enums
        /// </summary>
        public enum ePdtLevelTypes
        {
            Speaker = 0,
            Microphone = 1
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraFaderJoinMapAdvanceeStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraFaderJoinMapAdvanceeStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, "TesiraChannel {0} connect", Key);
            
            if (!Enabled) return;

            var genericChannel = this as IBasicVolumeWithFeedback;

            Debug.Console(2, this, "TesiraChannel {0} Is Enabled", Key);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);
            TypeFeedback.LinkInputSig(trilist.UShortInput[joinMap.Type.JoinNumber]);
            ControlTypeFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);
            PermissionsFeedback.LinkInputSig(trilist.UShortInput[joinMap.Permissions.JoinNumber]);
            VisibleFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Visible.JoinNumber]);

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