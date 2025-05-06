using System;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;

#if SERIES4
using PepperDash.Core.Logging;
#endif

namespace Tesira_DSP_EPI
{
    //Mixer1 get crosspointLevelState 1 1
    //Mixer1 set crosspointLevelState 1 1 true
    //Mixer1 toggle crosspointLevelState 1 1

    public class TesiraDspCrosspointState : TesiraDspControlPoint
    {
        public string AttributeCode = "crosspointLevelState";

        private const string KeyFormatter = "{0}--{1}";


        bool _state;

        private CTimer _pollTimer;

        private readonly bool _pollEnable;

        private readonly long _pollTime;

        /// <summary>
        /// Boolean Feedback for Component State
        /// </summary>
        public BoolFeedback CrosspointStateFeedback { get; set; }

        /// <summary>
        /// Constructor for Tesira DSP Matrix Mixer Component 
        /// </summary>
        /// <param name="key">Unique Key for Component</param>
        /// <param name="config">Config Object for Component</param>
        /// <param name="parent">Parent object of Component</param>
        public TesiraDspCrosspointState(string key, TesiraCrosspointStateBlockConfig config, TesiraDsp parent)
            : base(
                config.MatrixInstanceTag, string.Empty, config.Index1, config.Index2, parent,
                string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Label = config.Label;
            Enabled = config.Enabled;

            CrosspointStateFeedback = new BoolFeedback(Key + "-CrosspointStateFeedback", () => _state);

            Feedbacks.Add(CrosspointStateFeedback);
            Feedbacks.Add(NameFeedback);
            parent.Feedbacks.AddRange(Feedbacks);

            if (!config.Enabled) return;
            DeviceManager.AddDevice(this);
            _pollEnable = config.PollEnable;
            if (_pollEnable)
            {
                _pollTime = config.PollTimeMs < 10000 ? 10000 : config.PollTimeMs;
            }

        }

        /// <summary>
        /// Get Initial Values for Control.  Control does not subscribe.
        /// </summary>
        public override void Subscribe()
        {
            GetState();

            if (!_pollEnable) return;
            if (_pollTimer == null)
                _pollTimer = new CTimer(o => GetState(), null, _pollTime, _pollTime);

#if SERIES4
            this.LogVerbose($"Subscribe called on {Key}");
#else
            Debug.Console(2, this, "Subscribe called on {0}", Key);
#endif
        }

        public override void Unsubscribe()
        {
#if SERIES4
            this.LogVerbose(string.Format("Unsubscribe called on {0}", Key));
#else
            Debug.Console(2, this, "Unsubscribe called on {0}", Key);
#endif
            _pollTimer = null;
        }

        /// <summary>
        /// Poll Component State Value
        /// </summary>
        public void GetState()
        {
#if SERIES4
            this.LogVerbose(string.Format("GetState sent to {0}", Key));
#else
            Debug.Console(2, this, "GetState sent to {0}", Key);
#endif
            SendFullCommand("get", AttributeCode, String.Empty, 1);
        }

        /// <summary>
        /// Set Component State to On
        /// </summary>
        public void StateOn()
        {
#if SERIES4
            this.LogVerbose(string.Format("StateOn sent to {0}", Key));
#else
            Debug.Console(2, this, "StateOn sent to {0}", Key);
#endif
            SendFullCommand("set", AttributeCode, "true", 1);
            GetState();
        }

        /// <summary>
        /// Set Component State to Off
        /// </summary>
        public void StateOff()
        {
#if SERIES4
            this.LogVerbose(string.Format("StateOff sent to {0}", Key));
#else
            Debug.Console(2, this, "StateOff sent to {0}", Key);
#endif
            SendFullCommand("set", AttributeCode, "false", 1);
            GetState();
        }

        /// <summary>
        /// Toggle Component State
        /// </summary>
        public void StateToggle()
        {
#if SERIES4
            this.LogVerbose(string.Format("StateToggle sent to {0}", Key));
#else
            Debug.Console(2, this, "StateToggle sent to {0}", Key);
#endif
            SendFullCommand("toggle", AttributeCode, String.Empty, 1);
            GetState();
        }

        const string ParsePattern = "[^ ]* (.*)";
        private readonly static Regex ParseRegex = new Regex(ParsePattern);


        /// <summary>
        /// Parse non-subscription data
        /// </summary>
        /// <param name="attributeCode">Attribute code of Component</param>
        /// <param name="message">Data to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
#if SERIES4
                this.LogDebug(string.Format("xPoint Response: '{0}' Message: '{1}'", attributeCode, message));
#else
                Debug.Console(1, this, "xPoint Response: '{0}' Value: '{1}'", attributeCode, message);
#endif

                if (message.Contains("StandardMixerInterface"))
                {
                    AttributeCode = string.Format("crosspoint {0} {1}", Index1, Index2);
#if SERIES4
                    Debug.LogVerbose("StandardMixerInterface: {0}", AttributeCode);
#else
                    Debug.Console(2, this, "StandardMixerInterface: {0}", AttributeCode);
#endif
                    GetState();
                    return;
                }

                if (message.Equals("+OK", StringComparison.OrdinalIgnoreCase)) { return; }

                if (!attributeCode.Equals(AttributeCode, StringComparison.InvariantCultureIgnoreCase)) return;

                var match = ParseRegex.Match(message);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

#if SERIES4
                this.LogVerbose(string.Format("New Value: {0}", value));
#else
                Debug.Console(2, this, "New Value: {0}", value);
#endif

                _state = bool.Parse(value);
                CrosspointStateFeedback.FireUpdate();
            }

            catch (Exception e)
            {
#if SERIES4
                this.LogInformation(string.Format("Unable to parse response: {0}", e.Message));
#else
                Debug.Console(0, this, "Unable to parse response: {0}", e.Message);
#endif
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraCrosspointStateJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraCrosspointStateJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

#if SERIES4
            Debug.LogDebug("Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.LogVerbose("Tesira Crosspoint State {0} connect", Key);
#else
            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(2, "Tesira Crosspoint State {0} connect", Key);
#endif

            if (!Enabled) return;

#if SERIES4
            Debug.LogVerbose("Adding Crosspoint State ControlPoint {0} | JoinStart:{1}", Key, joinMap.Label.JoinNumber);
#else
            Debug.Console(2, this, "Adding Crosspoint State ControlPoint {0} | JoinStart:{1}", Key, joinMap.Label.JoinNumber);
#endif

            CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Toggle.JoinNumber]);
            CrosspointStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.On.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.Toggle.JoinNumber, StateToggle);
            trilist.SetSigTrueAction(joinMap.On.JoinNumber, StateOn);
            trilist.SetSigTrueAction(joinMap.Off.JoinNumber, StateOff);

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