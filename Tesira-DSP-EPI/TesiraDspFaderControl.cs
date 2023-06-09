using System;
using Newtonsoft.Json;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using System.Collections.Generic;
using Tesira_DSP_EPI.Extensions;
using Tesira_DSP_EPI.Interfaces;

namespace Tesira_DSP_EPI
{
    public class TesiraDspFaderControl : TesiraDspControlPoint, IBasicVolumeWithFeedback, IVolumeComponent
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
        private EPdtLevelTypes _type;
        private string LevelControlPointTag { get { return InstanceTag1; } }

        public BoolFeedback MuteFeedback { get; private set; }
        public BoolFeedback VisibleFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
        public IntFeedback TypeFeedback { get; private set; }
        public IntFeedback ControlTypeFeedback { get; private set; }
        public IntFeedback PermissionsFeedback { get; private set; }

        private Dictionary<string, SubscriptionTrackingObject> SubscriptionTracker { get; set; }

        
        CTimer _volumeUpRepeatTimer;
        CTimer _volumeDownRepeatTimer;
        CTimer _volumeUpRepeatDelayTimer;
        CTimer _volumeDownRepeatDelayTimer;

        //private bool LevelSubscribed { get; set; }

        bool _volDownPressTracker;
        bool _volUpPressTracker;


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
                var trackingBool = true;
                foreach (var item in SubscriptionTracker)
                {
                    Debug.Console(2, this, "{0} is {1} and {2}", item.Key, item.Value.Enabled ? "Enabled" : "Disabled", item.Value.Subscribed ? "Subscribed" : "Not Subscribed");
                    if (item.Value.Enabled && !item.Value.Subscribed)
                    {
                        trackingBool = false;
                    }
                }
                Debug.Console(1, this, "Item is {0}.", trackingBool ? "subscribed" : "not subscribed");
                return trackingBool;

            }
            protected set { }
        }

        private bool AutomaticUnmuteOnVolumeUp { get; set; }

        public readonly string MuteCustomName;
        public readonly string LevelCustomName;

        /// <summary>
        /// Component has Mute
        /// </summary>
        public bool HasMute { get; private set; }

        /// <summary>
        /// Component Has Level
        /// </summary>
        public bool HasLevel { get; private set; }

        /// <summary>
        /// Constructor for Component
        /// </summary>
        /// <param name="key">Unique Identifier for component</param>
        /// <param name="config">Config Object of Component</param>
        /// <param name="parent">Parent object of Component</param>
        public TesiraDspFaderControl(string key, TesiraFaderControlBlockConfig config, TesiraDsp parent)
            : base(config.LevelInstanceTag, config.MuteInstanceTag, config.Index1, config.Index2, parent, String.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {

            MuteCustomName = (string.Format("{0}__mute{1}", InstanceTag2, Index1)).Replace(" ", string.Empty);

            LevelCustomName = (string.Format("{0}__level{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);

            Initialize(config);
        }

        private void Initialize(TesiraFaderControlBlockConfig config)
        {
            _type = config.IsMic ? EPdtLevelTypes.Microphone : EPdtLevelTypes.Speaker;

            Debug.Console(2, this, "Adding LevelControl '{0}'", Key);
            AddCustomName(LevelCustomName);
            AddCustomName(MuteCustomName);

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

            SubscriptionTracker = new Dictionary<string, SubscriptionTrackingObject>
            {
                {"mute", new SubscriptionTrackingObject(HasMute)},
                {"level", new SubscriptionTrackingObject(HasLevel)}
            };




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
            /*
            if (IsSubscribed && HasMute)
            {
                AddCustomName(MuteCustomName);

                SendSubscriptionCommand(MuteCustomName, "mute", 500, 2);
                return;
            }

            if (IsSubscribed && HasLevel)
            {
                SendSubscriptionCommand(LevelCustomName, "level", 250, 1);
                AddCustomName(LevelCustomName);
                return;
            }
             * */
                //Subscribe to Mute
            if (HasMute)
            {
                AddCustomName(MuteCustomName);
                SendSubscriptionCommand(MuteCustomName, "mute", 500, 2);
            }

            //Subscribe to Level
            if (HasLevel)
            {
                AddCustomName(LevelCustomName);
                // MUST use InstanceTag1 for levels, it is the first instance tag in the JSON config
                SendSubscriptionCommand(LevelCustomName, "level", 250, 1);

            }
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            try
            {
                //Unsubscribe to Mute
                if (HasMute)
                {
                    Debug.Console(1, this, "Unsubscribe from Mute");
                    SendUnSubscriptionCommand(MuteCustomName, "mute", 2);
                    SubscriptionTracker["mute"].Subscribed = false;

                }

                //Unubscribe to Level
                if (HasLevel)
                {
                    SendUnSubscriptionCommand(LevelCustomName, "level", 2);
                    SubscriptionTracker["level"].Subscribed = false;
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Exception : {0}", e.Message);
            }
        }

        /// <summary>
        /// Parses subscription response data for the component
        /// </summary>
        /// <param name="customName">Subscription Identifier for component</param>
        /// <param name="value">Component data to be parsed</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
            Debug.Console(1, this, "Parsing Data - Name: {0} - Value {1}", customName, value);
            if (HasMute && customName == MuteCustomName)
            {
                IsMuted = bool.Parse(value);
                SubscriptionTracker["mute"].Subscribed = true;
            }
            else if (HasLevel && customName == LevelCustomName)
            {
                var localValue = Double.Parse(value);

                VolumeLevel = UseAbsoluteValue ? (ushort)localValue :  (ushort)localValue.Scale(MinLevel, MaxLevel, 0, 65535, this);

                SubscriptionTracker["level"].Subscribed = true;
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

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;
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
                    case "level":
                    {
                        var localValue = Double.Parse(value);

                        VolumeLevel = UseAbsoluteValue ? (ushort) localValue : (ushort)localValue.Scale(MinLevel, MaxLevel, 0, 65535, this);

                        Debug.Console(1, this, "VolumeLevel is '{0}'", VolumeLevel);

                        break;

                    }
                    default:
                    {
                        Debug.Console(0, "Response does not match expected attribute codes: '{0}'", message);

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
            switch (level)
            {
                case (ushort.MinValue):
                {
                    SendFullCommand("set", "level", string.Format("{0:0.000000}", MinLevel), 1);
                    break;
                }

                case (ushort.MaxValue):
                {
                    SendFullCommand("set", "level", string.Format("{0:0.000000}", MaxLevel), 1);
                    break;
                }
                default:
                {
                    var newLevel = Convert.ToDouble(level);

                    var volumeLevel = UseAbsoluteValue ? level : newLevel.Scale(0, 65535, MinLevel, MaxLevel, this);

                    SendFullCommand("set", "level", string.Format("{0:0.000000}", volumeLevel), 1);
                    break;

                }
            }
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
            if (!HasLevel) return;
            SendFullCommand("get", "level", String.Empty, 1);
        }

        /// <summary>
        /// Polls minimum level of fader component
        /// </summary>
        public void GetMinLevel()
        {
            if (!HasLevel) return;
            SendFullCommand("get", "minLevel", null, 1);
        }

        /// <summary>
        /// poll maximum level of fader component
        /// </summary>
        public void GetMaxLevel()
        {
            if (!HasLevel) return;
            SendFullCommand("get", "maxLevel", null, 1);
        }

        /// <summary>
        /// Polls the current mute state of Component
        /// </summary>
        public void GetMute()
        {
            if (!HasMute) return;
            SendFullCommand("get", "mute", String.Empty, 2);
        }

        /// <summary>
        /// Toggle component mute
        /// </summary>
        public void MuteToggle()
        {
            if (!HasMute) return;
            SendFullCommand("toggle", "mute", String.Empty, 2);
        }

        /// <summary>
        /// Decrements component level
        /// </summary>
        /// <param name="press">Trigger map to bridge or UI component</param>
        public void VolumeDown(bool press)
        {
            if (!HasLevel) return;
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
            if (!HasLevel) return;
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
        /// Possible LevelType enums
        /// </summary>
        public enum EPdtLevelTypes
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