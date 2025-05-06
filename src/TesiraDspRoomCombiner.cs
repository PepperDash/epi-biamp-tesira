using System;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using Tesira_DSP_EPI.Extensions;
using Tesira_DSP_EPI.Interfaces;

#if SERIES4
using PepperDash.Core.Logging;
#endif

namespace Tesira_DSP_EPI
{
    public class TesiraDspRoomCombiner : TesiraDspControlPoint, IBasicVolumeWithFeedback, IVolumeComponent
    {
        private bool _outIsMuted;
        protected bool OutIsMuted
        {
            get
            {
                return _outIsMuted;
            }
            set
            {
                _outIsMuted = value;
                MuteFeedback.FireUpdate();
            }
        }

        private int _outVolumeLevel;
        protected int OutVolumeLevel
        {
            get
            {
                return _outVolumeLevel;
            }
            set
            {
                _outVolumeLevel = value;
                VolumeLevelFeedback.FireUpdate();
            }
        }

        private int _roomGroup;
        protected int RoomGroup
        {
            get
            {
                return _roomGroup;
            }
            set
            {
                _roomGroup = value;
                RoomGroupFeedback.FireUpdate();
            }
        }

        private const string KeyFormatter = "{0}--{1}";
        /// <summary>
        /// Component Permissions
        /// </summary>
        public int Permissions { get; set; }

        /// <summary>
        /// Boolean Feedback for Mute State
        /// </summary>
        public BoolFeedback MuteFeedback { get; private set; }

        /// <summary>
        /// Boolean Feedback for Visbility State
        /// </summary>
        public BoolFeedback VisibleFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for Volume Level
        /// </summary>
        public IntFeedback VolumeLevelFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for Control Type
        /// </summary>
        public IntFeedback TypeFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for Permissions
        /// </summary>
        public IntFeedback PermissionsFeedback { get; private set; }

        /// <summary>
        /// Integer Feedback for RoomGroup
        /// </summary>
        public IntFeedback RoomGroupFeedback { get; private set; }

        private EPdtLevelTypes _type;
        private string IncrementAmount { get; set; }
        private bool UseAbsoluteValue { get; set; }
        private string LevelControlPointTag { get { return InstanceTag1; } }

        CTimer _volumeUpRepeatTimer;
        CTimer _volumeDownRepeatTimer;
        CTimer _volumeUpRepeatDelayTimer;
        CTimer _volumeDownRepeatDelayTimer;

        CTimer _pollTimer;

        bool _volDownPressTracker;
        bool _volUpPressTracker;

        /// <summary>
        /// Subscription identifier for Room Combiner
        /// </summary>
        public string LevelCustomName { get; private set; }

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
                return _levelIsSubscribed;       
            }
            protected set { }
        }

        private bool AutomaticUnmuteOnVolumeUp { get; set; }

        private bool _levelIsSubscribed;

        /// <summary>
        /// Constructor for Tesira Dsp Room Combiner Component
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="config">configuration object</param>
        /// <param name="parent">Parent Object</param>
		public TesiraDspRoomCombiner(string key, TesiraRoomCombinerBlockConfig config, TesiraDsp parent)
            : base(config.RoomCombinerInstanceTag, "", config.RoomIndex, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Initialize(config);
        }

        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="config">Configuration Object</param>
		private void Initialize(TesiraRoomCombinerBlockConfig config)
        {
#if SERIES4
            this.LogVerbose(string.Format("Adding RoomCombiner '{0}'", Key));
#else
            Debug.Console(2, this, "Adding RoomCombiner '{0}'", Key);
#endif

            IsSubscribed = false;

            _type = config.IsMic ? EPdtLevelTypes.Microphone : EPdtLevelTypes.Speaker;

            UseAbsoluteValue = config.UseAbsoluteValue;
            Enabled = config.Enabled;
            Permissions = config.Permissions;
            IncrementAmount = config.IncrementAmount;
            AutomaticUnmuteOnVolumeUp = config.UnmuteOnVolChange;
            _volumeUpRepeatTimer = new CTimer(o => VolumeUpRepeat(), Timeout.Infinite);
            _volumeDownRepeatTimer = new CTimer(o => VolumeDownRepeat(), Timeout.Infinite);
            _volumeUpRepeatDelayTimer = new CTimer(o => VolumeUpRepeatDelay(), Timeout.Infinite);
            _volumeDownRepeatDelayTimer = new CTimer(o => VolumeDownRepeatDelay(), Timeout.Infinite);

            _pollTimer = new CTimer(o => DoPoll(), Timeout.Infinite);

            MuteFeedback = new BoolFeedback(Key + "-MuteFeedback", () => OutIsMuted);
            VisibleFeedback = new BoolFeedback(Key + "-VisibleFeedback", () => Enabled);

            RoomGroupFeedback = new IntFeedback(Key + "-RoomGroupFeedback", () => RoomGroup);
            VolumeLevelFeedback = new IntFeedback(Key + "-LevelFeedback", () => OutVolumeLevel);
            TypeFeedback = new IntFeedback(Key + "-TypeFeedback", () => (ushort)_type);
            PermissionsFeedback = new IntFeedback(Key + "-PermissionsFeedback", () => Permissions);

            Feedbacks.Add(MuteFeedback);
            Feedbacks.Add(VolumeLevelFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(VisibleFeedback);
            Feedbacks.Add(TypeFeedback);
            Feedbacks.Add(PermissionsFeedback);

            Parent.Feedbacks.AddRange(Feedbacks);
        }


        private void VolumeUpRepeat()
        {
            if (_volUpPressTracker)
                VolumeUp(true);
        }
        private void VolumeDownRepeat()
        {
            if (_volDownPressTracker)
                VolumeDown(true);
        }

        private void VolumeUpRepeatDelay()
        {
            _volUpPressTracker = true;
            VolumeUp(true);
        }
        private void VolumeDownRepeatDelay()
        {
            _volDownPressTracker = true;
            VolumeDown(true);
        }

        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe()
        {
            //Subsribe to Level
            LevelCustomName = string.Format("{0}__roomCombiner{1}", InstanceTag1, Index1);
            AddCustomName(LevelCustomName);
            SendFullCommand("get", "levelOutMin", null, 1);
            SendFullCommand("get", "group", null, 1);
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {

            _levelIsSubscribed = false;
            LevelCustomName = string.Format("{0}__roomCombiner{1}", InstanceTag1, Index1);
            SendUnSubscriptionCommand(LevelCustomName, "levelOut", 1);
        }

        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="data"></param>
        public override void ParseSubscriptionMessage(string customName, string data)
        {
            if (customName != LevelCustomName) return;
            var value = Double.Parse(data);

            OutVolumeLevel = UseAbsoluteValue ? (ushort)value : (ushort) value.Scale(MinLevel, MaxLevel, 0, 65535, this);

            _levelIsSubscribed = true;

            _pollTimer.Reset(30000);
        }

        const string ParsePattern = "[^ ]* (.*)";
        private readonly static Regex ParseRegex = new Regex(ParsePattern);


        /// <summary>
        /// Parses a non subscription response
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command</param>
        /// <param name="message">The message to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
#if SERIES4
                this.LogVerbose(string.Format("Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode));
#else
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
#endif
                // Parse an "+OK" message

                var match = ParseRegex.Match(message);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

#if SERIES4
                this.LogDebug(string.Format("Response: '{0}' Value: '{1}'", attributeCode, value));
#else
                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);
#endif

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;
                switch (attributeCode)
                {
                    case "levelOutMin" :
                    {
                        MinLevel = Double.Parse(value);
#if SERIES4
                        this.LogDebug(string.Format("MinLevel is '{0}'", MinLevel));
#else
                        Debug.Console(1, this, "MinLevel is '{0}'", MinLevel);
#endif
                        break;
                    }
                    case "levelOutMax" :
                    {
                        MaxLevel = Double.Parse(value);
#if SERIES4
                        this.LogDebug(string.Format("MaxLevel is '{0}'", MaxLevel));
#else
                        Debug.Console(1, this, "MaxLevel is '{0}'", MaxLevel);
#endif
                        break;
                    }
                    case "muteOut" :
                    {
                        OutIsMuted = bool.Parse(value);
#if SERIES4
                        this.LogDebug(string.Format("MuteState is '{0}'", value));
#else
                        Debug.Console(1, this, "MuteState is '{0}'", value);
#endif
                        _pollTimer.Reset(30000);
                        break;
                    }

                    case "group":
                    {
                        RoomGroup = int.Parse(value);
#if SERIES4
                        this.LogDebug(string.Format("Room Group is '{0}'", value));
#else
                        Debug.Console(1, this, "Room Group is '{0}'", value);
#endif
                        _pollTimer.Reset(30000);
                        break;
                    }
                    default:
                    {
#if SERIES4
                        this.LogVerbose(string.Format("Response does not match expected attribute codes: '{0}'", message));
#else
                        Debug.Console(2, "Response does not match expected attribute codes: '{0}'", message);
#endif
                        break;
                    }
                }
            }
            catch (Exception e)
            {
#if SERIES4
                this.LogVerbose(string.Format("Unable to parse message: '{0}'\n{1}", message, e));
#else
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
#endif
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
#if SERIES4
            this.LogDebug(string.Format("volume: {0}", level));
#else
            Debug.Console(1, this, "volume: {0}", level);
#endif
            // Unmute volume if new level is higher than existing
            if (level > _outVolumeLevel && AutomaticUnmuteOnVolumeUp)
                if (_outIsMuted)
                    MuteOff();
            var newLevel = (double) level;

            var volumeLevel = UseAbsoluteValue ? level : newLevel.Scale(0, 65535, MinLevel, MaxLevel, this);

            SendFullCommand("set", "levelOut", string.Format("{0:0.000000}", volumeLevel), 1);
        }

        /// <summary>
        /// Set the room group to the specified value
        /// </summary>
        /// <param name="group"></param>
        public void SetRoomGroup(ushort group)
        {
#if SERIES4
            this.LogDebug(string.Format("group: {0}", group));
#else
            Debug.Console(1, this, "group: {0}", group);
#endif
            SendFullCommand("set", "group", Convert.ToString(group), 1);
        }

        /// <summary>
        /// Polls all data for this control
        /// </summary>
        public override void DoPoll()
        {
            GetVolume();
            GetMute();
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
        /// Polls minimum level of fader component
        /// </summary>
        public void GetMinLevel()
        {
            SendFullCommand("get", "minLevel", null, 1);
        }

        /// <summary>
        /// poll maximum level of fader component
        /// </summary>
        public void GetMaxLevel()
        {
            SendFullCommand("get", "maxLevel", null, 1);
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
        /// <param name="press"></param>
        public void VolumeDown(bool press)
        {
#if SERIES4
            this.LogVerbose(string.Format("VolumeDown Sent for {0}", LevelControlPointTag));
#else
            Debug.Console(2, "VolumeDown Sent for {0}", LevelControlPointTag);
#endif
            if (press)
            {
                if (_volDownPressTracker)
                {
                    _volumeDownRepeatTimer.Reset(100);
                    SendFullCommand("decrement", "levelOut", IncrementAmount, 1);
                }
                else if (!_volDownPressTracker)
                {
                    _volumeDownRepeatDelayTimer.Reset(750);
                    SendFullCommand("decrement", "levelOut", IncrementAmount, 1);
                }

            }
            if (!press)
            {
                _volDownPressTracker = false;
                _volumeDownRepeatTimer.Stop();
                _volumeDownRepeatDelayTimer.Stop();
            }
        }

        /// <summary>
        /// Increments volume level
        /// </summary>
        /// <param name="press"></param>
        public void VolumeUp(bool press)
        {
#if SERIES4
            this.LogVerbose(string.Format("VolumeUp Sent for {0}", LevelControlPointTag));
#else
            Debug.Console(2, "VolumeUp Sent for {0}", LevelControlPointTag);
#endif

            if (press)
            {
                if (_volUpPressTracker)
                {
                    _volumeUpRepeatTimer.Reset(100);
                    SendFullCommand("increment", "levelOut", IncrementAmount, 1);
                }
                else if (!_volUpPressTracker)
                {
                    _volumeUpRepeatDelayTimer.Reset(750);
                    SendFullCommand("increment", "levelOut", IncrementAmount, 1);
                    if (AutomaticUnmuteOnVolumeUp)
                    {
                        if (_outIsMuted)
                        {
                            MuteOff();
                        }
                    }
                }
            }
            if (!press)
            {
                _volUpPressTracker = false;
                _volumeUpRepeatTimer.Stop();
                _volumeUpRepeatDelayTimer.Stop();
            }
        }




        public enum EPdtLevelTypes
        {
            Speaker = 0,
            Microphone = 1
        }


        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraRoomCombinerJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraRoomCombinerJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

#if SERIES4
            this.LogVerbose(string.Format("Tesira Room Combiner {0} connect", Key));
#else
            Debug.Console(2, "Tesira Room Combiner {0} connect", Key);
#endif

            var genericChannel = this as IBasicVolumeWithFeedback;
            if (!Enabled) return;

#if SERIES4
            this.LogVerbose(string.Format("TesiraChannel {0} Is Enabled", Key));
#else
            Debug.Console(2, this, "TesiraChannel {0} Is Enabled", Key);
#endif

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);
            VisibleFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Visible.JoinNumber]);
            TypeFeedback.LinkInputSig(trilist.UShortInput[joinMap.Type.JoinNumber]);
            PermissionsFeedback.LinkInputSig(trilist.UShortInput[joinMap.Permissions.JoinNumber]);
            RoomGroupFeedback.LinkInputSig(trilist.UShortInput[joinMap.Group.JoinNumber]);

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

            trilist.SetUShortSigAction(joinMap.Group.JoinNumber, u => { if (u > 0) { SetRoomGroup(u); } });

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