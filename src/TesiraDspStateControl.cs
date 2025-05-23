﻿using System;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;

namespace Tesira_DSP_EPI {
    public class TesiraDspStateControl : TesiraDspControlPoint, IPrivacy {
        bool _state;
        int _tagForSubscription;

        private const string KeyFormatter = "{0}--{1}";

        /// <summary>
        /// Boolean Feedback for State Value
        /// </summary>
        public BoolFeedback StateFeedback { get; set; }

        /// <summary>
        /// State Subscription Identifier
        /// </summary>
        public string StateCustomName { get; set; }

        /// <summary>
        /// Constructor for StateControl Component
        /// </summary>
        /// <param name="key">Unique Key for Component</param>
        /// <param name="config">Config Object for Component</param>
        /// <param name="parent">Component Parent Object</param>
		public TesiraDspStateControl(string key, TesiraStateControlBlockConfig config, TesiraDsp parent)
            : base(config.StateInstanceTag, config.SubscriptionInstanceTag, config.Index, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Debug.Console(2, this, "New State Instance Tag = {0}", config.StateInstanceTag);
            Debug.Console(2, this, "Starting State {0} Initialize", key);

            StateFeedback = new BoolFeedback(Key + "-StateFeedback", () => _state);
            PrivacyModeIsOnFeedback = new BoolFeedback(Key + "-PrivacyFeedback", () => _state);

            //Look for second instance tag for subscription on state, if not defined just try to subscribe to the state object (instance tag 1)
            _tagForSubscription = string.IsNullOrEmpty(config.SubscriptionInstanceTag) ? 1 : 2;
            StateCustomName = string.Format("{0}__state{1}", config.StateInstanceTag, Index1);

            Feedbacks.Add(StateFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(PrivacyModeIsOnFeedback);
            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);
        }

		private void Initialize(TesiraStateControlBlockConfig config)
		{
            Debug.Console(2, this, "Adding StateControl '{0}'", Key);
            IsSubscribed = false;
            Enabled = config.Enabled;
        }

        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe() {
            Debug.Console(2, this, "StateCustomName = {0}", StateCustomName);
            AddCustomName(StateCustomName);
            SendSubscriptionCommand(StateCustomName, "state", 250, _tagForSubscription);

            GetState();
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            IsSubscribed = false;
            Debug.Console(2, this, "StateCustomName = {0}", StateCustomName);
            SendUnSubscriptionCommand(StateCustomName, "state", _tagForSubscription);
        }

        /// <summary>
        /// Parses subscription-related responses
        /// </summary>
        /// <param name="customName">Subscription identifier</param>
        /// <param name="value">response data to be parsed</param>
        public override void ParseSubscriptionMessage(string customName, string value) {

            // Check for valid subscription response

            if (customName != StateCustomName) return;
            _state = bool.Parse(value);
            FireFeedbacks();
            IsSubscribed = true;
        }

        private void FireFeedbacks()
        {
            foreach (var feedback in Feedbacks)
            {
                feedback.FireUpdate();
            }
        }

        const string ParsePattern = "[^ ]* (.*)";
        private readonly static Regex ParseRegex = new Regex(ParsePattern);

        /// <summary>
        /// Parses a non subscription response
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command</param>
        /// <param name="message">The message to parse</param>
        public override void ParseGetMessage(string attributeCode, string message) {
            try {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message

                var match = ParseRegex.Match(message);

                if (!match.Success) return;

                var value = match.Groups[1].Value;

                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;

                if (attributeCode != "state") return;

                _state = bool.Parse(value);
                FireFeedbacks();
                IsSubscribed = true;
            }
            catch (Exception e) {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
            }
        }

        /// <summary>
        /// Poll state status
        /// </summary>
        public void GetState() {
            Debug.Console(2, this, "GetState sent to {0}", Key);
            SendFullCommand("get", "state", String.Empty, 1);
        }

        /// <summary>
        /// Set State On
        /// </summary>
        public void StateOn() {
            Debug.Console(2, this, "StateOn sent to {0}", Key);
            SendFullCommand("set", "state", "true", 1);
            GetState();
        }

        /// <summary>
        /// Set State off
        /// </summary>
        public void StateOff() {
            Debug.Console(2, this, "StateOff sent to {0}", Key);
            SendFullCommand("set", "state", "false", 1);
            GetState();
        }

        /// <summary>
        /// Toggle State value
        /// </summary>
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


        #region IPrivacy Members

        public BoolFeedback PrivacyModeIsOnFeedback { get; set; }

        public void PrivacyModeOff()
        {
            StateOff();
        }

        public void PrivacyModeOn()
        {
            StateOn();
        }

        public void PrivacyModeToggle()
        {
            StateToggle();
        }

        #endregion
    }
}