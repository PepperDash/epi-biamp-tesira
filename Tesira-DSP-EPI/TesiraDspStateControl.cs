using System;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;

namespace Tesira_DSP_EPI {
    public class TesiraDspStateControl : TesiraDspControlPoint {
        bool _state;

        private const string KeyFormatter = "{0}--{1}";


        public BoolFeedback StateFeedback { get; set; }

        public string StateCustomName { get; set; }

		public TesiraDspStateControl(string key, TesiraStateControlBlockConfig config, TesiraDsp parent)
            : base(config.StateInstanceTag, String.Empty, config.Index, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Debug.Console(2, this, "New State Instance Tag = {0}", config.StateInstanceTag);
            Debug.Console(2, this, "Starting State {0} Initialize", key);

            StateFeedback = new BoolFeedback(Key + "-StateFeedback", () => _state);

            Feedbacks.Add(StateFeedback);
            Feedbacks.Add(NameFeedback);
            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);

        }


        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="key">key of the control</param>
        /// <param name="label">friendly name of the control</param>
        /// <param name="hasMute">defines if the control has a mute</param>
        /// <param name="hasLevel">defines if the control has a level</param>
		public void Initialize(TesiraStateControlBlockConfig config)
		{
            /*
            if (config.enabled)
            {
                DeviceManager.AddDevice(this);
            }
             */

            Debug.Console(2, this, "Adding StateControl '{0}'", Key);

            IsSubscribed = false;


            Enabled = config.Enabled;

            //Subscribe();
        }

        public override void Subscribe() {
            StateCustomName = string.Format("{0}~state{1}", InstanceTag1, Index1);
            Debug.Console(2, this, "StateCustomName = {0}", StateCustomName);
            SendSubscriptionCommand(StateCustomName, "state", 250, 1);

            GetState();
        }

        public override void Unsubscribe()
        {
            StateCustomName = string.Format("{0}~state{1}", InstanceTag1, Index1);
            Debug.Console(2, this, "StateCustomName = {0}", StateCustomName);
            SendUnSubscriptionCommand(StateCustomName, "state", 1);
        }

        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        public void ParseSubscriptionMessage(string customName, string value) {

            // Check for valid subscription response

            if (customName == StateCustomName) {
                _state = bool.Parse(value);
                StateFeedback.FireUpdate();
                IsSubscribed = true;
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
                var pattern = "[^ ]* (.*)";

                var match = Regex.Match(message, pattern);

                if (!match.Success) return;

                var value = match.Groups[1].Value;

                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;

                if (attributeCode != "state") return;

                _state = bool.Parse(value);
                StateFeedback.FireUpdate();
                IsSubscribed = true;
            }
            catch (Exception e) {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
            }
        }

        public void GetState() {
            Debug.Console(2, this, "GetState sent to {0}", Key);
            SendFullCommand("get", "state", String.Empty, 1);
        }

        public void StateOn() {
            Debug.Console(2, this, "StateOn sent to {0}", Key);
            SendFullCommand("set", "state", "true", 1);
            GetState();
        }

        public void StateOff() {
            Debug.Console(2, this, "StateOff sent to {0}", Key);
            SendFullCommand("set", "state", "false", 1);
            GetState();
        }

        public void StateToggle() {
            Debug.Console(2, this, "StateToggle sent to {0}", Key);
            if (_state) {
                SendFullCommand("set", "state", "false", 1);
            }
            else if (!_state) {
                SendFullCommand("set", "state", "true", 1);
            }
            GetState();
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraStateJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraStateJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            if (!Enabled) return;

            Debug.Console(2, this, "Tesira State {0} is Enabled", Key);

            StateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Toggle.JoinNumber]);
            StateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.On.JoinNumber]);
            StateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.Off.JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);

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