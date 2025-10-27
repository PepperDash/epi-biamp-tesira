using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDspFaderControl : TesiraDspControlPoint,
        IBasicVolumeWithFeedbackAdvanced,
        IVolumeComponent
    {
        private bool isMuted;
        protected bool IsMuted
        {
            get
            {
                return isMuted;
            }
            set
            {
                isMuted = value;
                MuteFeedback.FireUpdate();
            }
        }
        private int volumeLevel;
        protected int VolumeLevel
        {
            get
            {
                return volumeLevel;
            }
            set
            {
                volumeLevel = value;
                VolumeLevelFeedback.FireUpdate();
            }
        }

        private const string keyFormatter = "{0}--{1}";

        private int Permissions { get; set; }
        private int ControlType { get; set; }

        private string IncrementAmount { get; set; }
        private bool UseAbsoluteValue { get; set; }
        private int VolumeRepeatRateMs { get; set; }
        private EPdtLevelTypes type;
        private string LevelControlPointTag { get { return InstanceTag1; } }

        public BoolFeedback MuteFeedback { get; private set; }
        public BoolFeedback VisibleFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
        public IntFeedback TypeFeedback { get; private set; }
        public IntFeedback ControlTypeFeedback { get; private set; }
        public IntFeedback PermissionsFeedback { get; private set; }

        private Dictionary<string, SubscriptionTrackingObject> SubscriptionTracker { get; set; }


        System.Timers.Timer volumeUpRepeatTimer;
        System.Timers.Timer volumeDownRepeatTimer;
        System.Timers.Timer volumeUpRepeatDelayTimer;
        System.Timers.Timer volumeDownRepeatDelayTimer;

        //private bool LevelSubscribed { get; set; }

        bool volDownPressTracker;
        bool volUpPressTracker;


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
                    this.LogVerbose("Enabled: {enabled} Subscribed: {subscribed}", item.Value.Enabled, item.Value.Subscribed);
                    if (item.Value.Enabled && !item.Value.Subscribed)
                    {
                        trackingBool = false;
                    }
                }
                this.LogDebug("{subscribed}.", trackingBool ? "subscribed" : "not subscribed");
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

        public int RawVolumeLevel { get; private set; }

        public eVolumeLevelUnits Units
        {
            get
            {
                return eVolumeLevelUnits.Decibels;
            }
        }


        /// <summary>
        /// Constructor for Component
        /// </summary>
        /// <param name="key">Unique Identifier for component</param>
        /// <param name="config">Config Object of Component</param>
        /// <param name="parent">Parent object of Component</param>
        public TesiraDspFaderControl(string key, TesiraFaderControlBlockConfig config, TesiraDsp parent)
            : base(config.LevelInstanceTag, config.MuteInstanceTag, config.Index1, config.Index2, parent, string.Format(keyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {

            MuteCustomName = string.Format("{0}__mute{1}", InstanceTag2, Index1).Replace(" ", string.Empty);

            LevelCustomName = string.Format("{0}__level{1}", InstanceTag1, Index1).Replace(" ", string.Empty);

            Initialize(config);
        }

        private void Initialize(TesiraFaderControlBlockConfig config)
        {
            type = config.IsMic ? EPdtLevelTypes.Microphone : EPdtLevelTypes.Speaker;

            this.LogVerbose("Adding LevelControl {key}", Key);
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
            VolumeRepeatRateMs = config.VolumeRepeatRateMs;
            volumeUpRepeatTimer = new System.Timers.Timer();
            volumeUpRepeatTimer.Elapsed += (sender, e) => VolumeUpRepeat(null);
            volumeUpRepeatTimer.AutoReset = false;
            volumeUpRepeatTimer.Enabled = false;

            volumeDownRepeatTimer = new System.Timers.Timer();
            volumeDownRepeatTimer.Elapsed += (sender, e) => VolumeDownRepeat(null);
            volumeDownRepeatTimer.AutoReset = false;
            volumeDownRepeatTimer.Enabled = false;

            volumeUpRepeatDelayTimer = new System.Timers.Timer();
            volumeUpRepeatDelayTimer.Elapsed += (sender, e) => VolumeUpRepeatDelay(null);
            volumeUpRepeatDelayTimer.AutoReset = false;
            volumeUpRepeatDelayTimer.Enabled = false;

            volumeDownRepeatDelayTimer = new System.Timers.Timer();
            volumeDownRepeatDelayTimer.Elapsed += (sender, e) => VolumeDownRepeatDelay(null);
            volumeDownRepeatDelayTimer.AutoReset = false;
            volumeDownRepeatDelayTimer.Enabled = false;

            SubscriptionTracker = new Dictionary<string, SubscriptionTrackingObject>
            {
                {"mute", new SubscriptionTrackingObject(HasMute)},
                {"level", new SubscriptionTrackingObject(HasLevel)}
            };




            if (HasMute && HasLevel)
            {
                ControlType = 0;
                this.LogVerbose("{key} has BOTH Mute and Level", Key);
            }
            else if (!HasMute && HasLevel)
            {
                ControlType = 1;
                this.LogVerbose("{key} has Level ONLY", Key);

            }

            else if (HasMute && !HasLevel)
            {
                this.LogVerbose("{key} has MUTE ONLY", Key);
                ControlType = 2;
            }

            MuteFeedback = new BoolFeedback(Key + "-MuteFeedback", () => IsMuted);
            VisibleFeedback = new BoolFeedback(Key + "-VisibleFeedback", () => Enabled);

            VolumeLevelFeedback = new IntFeedback(Key + "-LevelFeedback", () => VolumeLevel);
            TypeFeedback = new IntFeedback(Key + "-TypeFeedback", () => (ushort)type);
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
            if (volUpPressTracker)
                VolumeUp(true);
        }
        private void VolumeDownRepeat(object callbackObject)
        {
            if (volDownPressTracker)
                VolumeDown(true);
        }

        private void VolumeUpRepeatDelay(object callbackObject)
        {
            volUpPressTracker = true;
            VolumeUp(true);
        }
        private void VolumeDownRepeatDelay(object callbackObject)
        {
            volDownPressTracker = true;
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
                    this.LogVerbose("Unsubscribe from Mute");
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
                this.LogError("Exception : {message}", e.Message);
                this.LogDebug(e, "Stack Trace: ");
            }
        }

        /// <summary>
        /// Parses subscription response data for the component
        /// </summary>
        /// <param name="customName">Subscription Identifier for component</param>
        /// <param name="value">Component data to be parsed</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
            this.LogVerbose("Parsing Data - Name: {customName} - Value {value}", customName, value);

            if (HasMute && customName == MuteCustomName)
            {
                IsMuted = bool.Parse(value);
                SubscriptionTracker["mute"].Subscribed = true;
            }
            else if (HasLevel && customName == LevelCustomName)
            {
                var localValue = double.Parse(value);

                RawVolumeLevel = (int)localValue;

                VolumeLevel = UseAbsoluteValue ? (ushort)localValue : (ushort)localValue.Scale(MinLevel, MaxLevel, 0, 65535, this);

                SubscriptionTracker["level"].Subscribed = true;
            }

        }

        const string parsePattern = "[^ ]* (.*)";
        private readonly static Regex parseRegex = new Regex(parsePattern);


        /// <summary>
        /// Parses non-subscription response data for the component
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command for the parsing algorithm</param>
        /// <param name="message">Component data to be parsed</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                this.LogVerbose("Parsing Message - {message}. AttributeCode: {attributeCode}", message, attributeCode);
                // Parse an "+OK" message

                var match = parseRegex.Match(message);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                this.LogVerbose("Response: '{attributeCode}' Value: '{value}'", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;
                switch (attributeCode)
                {
                    case "minLevel":
                        {
                            MinLevel = double.Parse(value);

                            this.LogDebug("MinLevel is '{MinLevel}'", MinLevel);

                            break;
                        }
                    case "maxLevel":
                        {
                            MaxLevel = double.Parse(value);

                            this.LogDebug("MaxLevel is '{MaxLevel}'", MaxLevel);

                            break;
                        }
                    case "level":
                        {
                            var localValue = double.Parse(value);

                            RawVolumeLevel = (int)localValue;

                            VolumeLevel = UseAbsoluteValue ? (ushort)localValue : (ushort)localValue.Scale(MinLevel, MaxLevel, 0, 65535, this);

                            this.LogDebug("VolumeLevel: {VolumeLevel}", VolumeLevel);

                            break;
                        }
                    default:
                        {
                            this.LogWarning("Response does not match expected attribute codes: '{message}'", message);
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                this.LogError("Exception parsing message: {message}. Error: {error}", message, e.Message);
                this.LogDebug(e, "Stack Trace: ");
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
            this.LogDebug("volume: {level}", level);
            // Unmute volume if new level is higher than existing
            if (level > volumeLevel && AutomaticUnmuteOnVolumeUp)
                if (isMuted)
                    MuteOff();
            switch (level)
            {
                case ushort.MinValue:
                    {
                        SendFullCommand("set", "level", string.Format("{0:0.000000}", MinLevel), 1);
                        break;
                    }

                case ushort.MaxValue:
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
            SendFullCommand("get", "level", string.Empty, 1);
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
            SendFullCommand("get", "mute", string.Empty, 2);
        }

        /// <summary>
        /// Toggle component mute
        /// </summary>
        public void MuteToggle()
        {
            if (!HasMute) return;
            SendFullCommand("toggle", "mute", string.Empty, 2);
        }

        /// <summary>
        /// Decrements component level
        /// </summary>
        /// <param name="press">Trigger map to bridge or UI component</param>
        public void VolumeDown(bool press)
        {
            if (!HasLevel) return;
            this.LogDebug("VolumeDown Sent for {LevelControlPointTag}", LevelControlPointTag);
            if (press)
            {
                if (volDownPressTracker)
                {
                    volumeDownRepeatTimer.Stop();
                    volumeDownRepeatTimer.Interval = VolumeRepeatRateMs;
                    volumeDownRepeatTimer.Start();
                    SendFullCommand("decrement", "level", IncrementAmount, 1);
                }
                else if (!volDownPressTracker)
                {
                    volumeDownRepeatDelayTimer.Stop();
                    volumeDownRepeatDelayTimer.Interval = 750;
                    volumeDownRepeatDelayTimer.Start();
                    SendFullCommand("decrement", "level", IncrementAmount, 1);
                }
                return;
            }

            volDownPressTracker = false;
            volumeDownRepeatTimer.Stop();
            volumeDownRepeatDelayTimer.Stop();
        }

        /// <summary>
        /// Increments component level
        /// </summary>
        /// <param name="press">Trigger map to bridge or UI component</param>
        public void VolumeUp(bool press)
        {
            if (!HasLevel) return;
            this.LogDebug("VolumeUp Sent for {LevelControlPointTag}", LevelControlPointTag);

            if (press)
            {
                if (volUpPressTracker)
                {
                    volumeUpRepeatTimer.Stop();
                    volumeUpRepeatTimer.Interval = VolumeRepeatRateMs;
                    volumeUpRepeatTimer.Start();
                    SendFullCommand("increment", "level", IncrementAmount, 1);
                }
                else if (!volUpPressTracker)
                {
                    volumeUpRepeatDelayTimer.Stop();
                    volumeUpRepeatDelayTimer.Interval = 750;
                    volumeUpRepeatDelayTimer.Start();
                    SendFullCommand("increment", "level", IncrementAmount, 1);
                    if (!AutomaticUnmuteOnVolumeUp) return;

                    if (isMuted)
                    {
                        MuteOff();
                    }
                }
                return;
            }

            volUpPressTracker = false;
            volumeUpRepeatTimer.Stop();
            volumeUpRepeatDelayTimer.Stop();
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
            var joinMap = new TesiraFaderJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraFaderJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            this.LogDebug("Linking to Trilist '{trilist.ID:X}'");

            this.LogDebug("TesiraChannel {Key} connect", Key);

            if (!Enabled) return;

            var genericChannel = this as IBasicVolumeWithFeedback;

            this.LogDebug("TesiraChannel {Key} Is Enabled", Key);

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