using System;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;

namespace Tesira_DSP_EPI
{
    //Mixer1 get crosspointLevelState 1 1
    //Mixer1 set crosspointLevelState 1 1 true
    //Mixer1 toggle crosspointLevelState 1 1

    public class TesiraDspCrosspointState : TesiraDspControlPoint
    {
        public static readonly string AttributeCode = "crosspointLevelState";

        private const string KeyFormatter = "{0}--{1}";

        private const string Pattern = "[^ ]* (.*)";


        bool _state;

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
            : base(config.MatrixInstanceTag, string.Empty, config.Index1, config.Index2, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Label = config.Label;
            Enabled = config.Enabled;

            CrosspointStateFeedback = new BoolFeedback(Key + "-CrosspointStateFeedback", () => _state);

            Feedbacks.Add(CrosspointStateFeedback);
            Feedbacks.Add(NameFeedback);
            parent.Feedbacks.AddRange(Feedbacks);

            if (config.Enabled)
            {
                DeviceManager.AddDevice(this);
            }
        }

        /// <summary>
        /// Get Initial Values for Control.  Control does not subscribe.
        /// </summary>
        public override void Subscribe()
        {
            GetState();
        }

        /// <summary>
        /// Poll Component State Value
        /// </summary>
        public void GetState()
        {
            Debug.Console(2, this, "GetState sent to {0}", Key);
            SendFullCommand("get", AttributeCode, String.Empty, 1);
        }

        /// <summary>
        /// Set Component State to On
        /// </summary>
        public void StateOn()
        {
            Debug.Console(2, this, "StateOn sent to {0}", Key);
            SendFullCommand("set", AttributeCode, "true", 1);
            GetState();
        }

        /// <summary>
        /// Set Component State to Off
        /// </summary>
        public void StateOff()
        {
            Debug.Console(2, this, "StateOff sent to {0}", Key);
            SendFullCommand("set", AttributeCode, "false", 1);
            GetState();
        }

        /// <summary>
        /// Toggle Component State
        /// </summary>
        public void StateToggle()
        {
            Debug.Console(2, this, "StateToggle sent to {0}", Key);
            SendFullCommand("toggle", AttributeCode, String.Empty, 1);
            GetState();
        }

        /// <summary>
        /// Parse non-subscription data
        /// </summary>
        /// <param name="attributeCode">Attribute code of Component</param>
        /// <param name="message">Data to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message,
                    attributeCode);
                // Parse an "+OK" message

                var match = Regex.Match(message, Pattern);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;
                if (!attributeCode.Equals(AttributeCode, StringComparison.InvariantCultureIgnoreCase)) return;
                _state = bool.Parse(value);
                Debug.Console(2, this, "New Value: {0}", _state);
                CrosspointStateFeedback.FireUpdate();
            }

            catch (Exception e)
            {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
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

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            Debug.Console(2, "Tesira Crosspoint State {0} connect", Key);

            if (!Enabled) return;

            Debug.Console(2, this, "Adding Crosspoint State ControlPoint {0} | JoinStart:{1}", Key, joinMap.Label.JoinNumber);
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