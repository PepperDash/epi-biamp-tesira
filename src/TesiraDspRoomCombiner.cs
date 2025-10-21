using System;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;


namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDspRoomCombiner : TesiraDspControlPoint, IBasicVolumeWithFeedback, IVolumeComponent
    {
        private bool outIsMuted;
        protected bool OutIsMuted
        {
            get
            {
                return outIsMuted;
            }
            set
            {
                outIsMuted = value;
                MuteFeedback.FireUpdate();
            }
        }

        private int outVolumeLevel;
        protected int OutVolumeLevel
        {
            get
            {
                return outVolumeLevel;
            }
            set
            {
                outVolumeLevel = value;
                VolumeLevelFeedback.FireUpdate();
            }
        }

        private int roomGroup;
        protected int RoomGroup
        {
            get
            {
                return roomGroup;
            }
            set
            {
                roomGroup = value;
                RoomGroupFeedback.FireUpdate();
            }
        }

        private const string keyFormatter = "{0}--{1}";
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

        private EPdtLevelTypes type;
        private string IncrementAmount { get; set; }
        private bool UseAbsoluteValue { get; set; }
        private string LevelControlPointTag { get { return InstanceTag1; } }

        CTimer volumeUpRepeatTimer;
        CTimer volumeDownRepeatTimer;
        CTimer volumeUpRepeatDelayTimer;
        CTimer volumeDownRepeatDelayTimer;

        CTimer pollTimer;

        bool volDownPressTracker;
        bool volUpPressTracker;

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
                return levelIsSubscribed;
            }
            protected set { }
        }

        private bool AutomaticUnmuteOnVolumeUp { get; set; }

        private bool levelIsSubscribed;

        /// <summary>
        /// Constructor for Tesira Dsp Room Combiner Component
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="config">configuration object</param>
        /// <param name="parent">Parent Object</param>
		public TesiraDspRoomCombiner(string key, TesiraRoomCombinerBlockConfig config, TesiraDsp parent)
            : base(config.RoomCombinerInstanceTag, "", config.RoomIndex, 0, parent, string.Format(keyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Initialize(config);
        }

        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="config">Configuration Object</param>
		private void Initialize(TesiraRoomCombinerBlockConfig config)
        {

            this.LogVerbose("Adding RoomCombiner {key}", Key);

            IsSubscribed = false;

            type = config.IsMic ? EPdtLevelTypes.Microphone : EPdtLevelTypes.Speaker;

            UseAbsoluteValue = config.UseAbsoluteValue;
            Enabled = config.Enabled;
            Permissions = config.Permissions;
            IncrementAmount = config.IncrementAmount;
            AutomaticUnmuteOnVolumeUp = config.UnmuteOnVolChange;
            volumeUpRepeatTimer = new CTimer(o => VolumeUpRepeat(), Timeout.Infinite);
            volumeDownRepeatTimer = new CTimer(o => VolumeDownRepeat(), Timeout.Infinite);
            volumeUpRepeatDelayTimer = new CTimer(o => VolumeUpRepeatDelay(), Timeout.Infinite);
            volumeDownRepeatDelayTimer = new CTimer(o => VolumeDownRepeatDelay(), Timeout.Infinite);

            pollTimer = new CTimer(o => DoPoll(), Timeout.Infinite);



            MuteFeedback = new BoolFeedback(Key + "-MuteFeedback", () => OutIsMuted);
            VisibleFeedback = new BoolFeedback(Key + "-VisibleFeedback", () => Enabled);

            RoomGroupFeedback = new IntFeedback(Key + "-RoomGroupFeedback", () => RoomGroup);
            VolumeLevelFeedback = new IntFeedback(Key + "-LevelFeedback", () => OutVolumeLevel);
            TypeFeedback = new IntFeedback(Key + "-TypeFeedback", () => (ushort)type);
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
            if (volUpPressTracker)
                VolumeUp(true);
        }
        private void VolumeDownRepeat()
        {
            if (volDownPressTracker)
                VolumeDown(true);
        }

        private void VolumeUpRepeatDelay()
        {
            volUpPressTracker = true;
            VolumeUp(true);
        }
        private void VolumeDownRepeatDelay()
        {
            volDownPressTracker = true;
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

            levelIsSubscribed = false;
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
            var value = double.Parse(data);

            OutVolumeLevel = UseAbsoluteValue ? (ushort)value : (ushort)value.Scale(MinLevel, MaxLevel, 0, 65535, this);

            levelIsSubscribed = true;

            pollTimer.Reset(30000);
        }

        const string parsePattern = "[^ ]* (.*)";
        private readonly static Regex parseRegex = new Regex(parsePattern);


        /// <summary>
        /// Parses a non subscription response
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command</param>
        /// <param name="message">The message to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                this.LogVerbose("Parsing Message: {message} attributeCode: {attributeCode}", message, attributeCode);
                // Parse an "+OK" message

                var match = parseRegex.Match(message);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                this.LogDebug("Response: {attributeCode} Value: {value}", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;
                switch (attributeCode)
                {
                    case "levelOutMin":
                        {
                            MinLevel = double.Parse(value);
                            this.LogDebug("MinLevel: {MinLevel}", MinLevel);
                            break;
                        }
                    case "levelOutMax":
                        {
                            MaxLevel = double.Parse(value);
                            this.LogDebug("MaxLevel: {MaxLevel}", MaxLevel);
                            break;
                        }
                    case "muteOut":
                        {
                            OutIsMuted = bool.Parse(value);
                            this.LogDebug("MuteState is {value}", value);
                            pollTimer.Reset(30000);
                            break;
                        }

                    case "group":
                        {
                            RoomGroup = int.Parse(value);
                            this.LogDebug("Room Group: {RoomGroup}", RoomGroup);
                            pollTimer.Reset(30000);
                            break;
                        }
                    default:
                        {
                            this.LogDebug("Response does not match expected attribute codes: '{message}'", message);
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                this.LogError("Unable to parse {message}: {exception}", message, e.Message);
                this.LogDebug(e, "Stack Trace: ");
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
            this.LogDebug("volume: {level}", level);
            // Unmute volume if new level is higher than existing
            if (level > outVolumeLevel && AutomaticUnmuteOnVolumeUp)
                if (outIsMuted)
                    MuteOff();
            var newLevel = (double)level;

            var volumeLevel = UseAbsoluteValue ? level : newLevel.Scale(0, 65535, MinLevel, MaxLevel, this);

            SendFullCommand("set", "levelOut", string.Format("{0:0.000000}", volumeLevel), 1);
        }

        /// <summary>
        /// Set the room group to the specified value
        /// </summary>
        /// <param name="group"></param>
        public void SetRoomGroup(ushort group)
        {
            this.LogDebug("group: {group}", group);
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
            SendFullCommand("get", "levelOut", string.Empty, 1);
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
            SendFullCommand("get", "muteOut", string.Empty, 1);
        }

        /// <summary>
        /// Polls the current room group
        /// </summary>
        public void GetRoomGroup()
        {
            SendFullCommand("get", "group", string.Empty, 1);
        }

        /// <summary>
        /// Toggles mute status
        /// </summary>
        public void MuteToggle()
        {
            SendFullCommand("toggle", "muteOut", string.Empty, 1);
        }

        /// <summary>
        /// Decrements volume level
        /// </summary>
        /// <param name="press"></param>
        public void VolumeDown(bool press)
        {
            this.LogDebug("VolumeDown Sent for {LevelControlPointTag}", LevelControlPointTag);
            if (press)
            {
                if (volDownPressTracker)
                {
                    volumeDownRepeatTimer.Reset(100);
                    SendFullCommand("decrement", "levelOut", IncrementAmount, 1);
                }
                else if (!volDownPressTracker)
                {
                    volumeDownRepeatDelayTimer.Reset(750);
                    SendFullCommand("decrement", "levelOut", IncrementAmount, 1);
                }

            }
            if (!press)
            {
                volDownPressTracker = false;
                volumeDownRepeatTimer.Stop();
                volumeDownRepeatDelayTimer.Stop();
            }
        }

        /// <summary>
        /// Increments volume level
        /// </summary>
        /// <param name="press"></param>
        public void VolumeUp(bool press)
        {
            this.LogDebug("VolumeUp Sent for {LevelControlPointTag}", LevelControlPointTag);

            if (press)
            {
                if (volUpPressTracker)
                {
                    volumeUpRepeatTimer.Reset(100);
                    SendFullCommand("increment", "levelOut", IncrementAmount, 1);
                }
                else if (!volUpPressTracker)
                {
                    volumeUpRepeatDelayTimer.Reset(750);
                    SendFullCommand("increment", "levelOut", IncrementAmount, 1);
                    if (AutomaticUnmuteOnVolumeUp)
                    {
                        if (outIsMuted)
                        {
                            MuteOff();
                        }
                    }
                }
            }
            if (!press)
            {
                volUpPressTracker = false;
                volumeUpRepeatTimer.Stop();
                volumeUpRepeatDelayTimer.Stop();
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

            this.LogVerbose("Tesira Room Combiner {0} connect", Key);

            var genericChannel = this as IBasicVolumeWithFeedback;
            if (!Enabled) return;

            this.LogDebug("TesiraChannel {0} Is Enabled", Key);

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