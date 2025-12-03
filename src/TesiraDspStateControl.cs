using System;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDspStateControl : TesiraDspControlPoint, IPrivacy, IStateFeedback, IHasStateControlWithFeedback
    {
        private bool state;
        private readonly int tagForSubscription;

        private const string keyFormatter = "{0}--{1}";

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
            : base(config.StateInstanceTag, config.SubscriptionInstanceTag, config.Index, 0, parent, string.Format(keyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            StateFeedback = new BoolFeedback(Key + "-StateFeedback", () => state);
            PrivacyModeIsOnFeedback = new BoolFeedback(Key + "-PrivacyFeedback", () => state);

            //Look for second instance tag for subscription on state, if not defined just try to subscribe to the state object (instance tag 1)
            tagForSubscription = string.IsNullOrEmpty(config.SubscriptionInstanceTag) ? 1 : 2;
            StateCustomName = string.Format("{0}__state{1}", config.StateInstanceTag, Index1);

            Feedbacks.Add(StateFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(PrivacyModeIsOnFeedback);
            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);
        }

        private void Initialize(TesiraStateControlBlockConfig config)
        {
            this.LogVerbose("Adding StateControl {key}", Key);
            IsSubscribed = false;
            Enabled = config.Enabled;
        }

        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe()
        {
            this.LogVerbose("StateCustomName = {customName}", StateCustomName);

            AddCustomName(StateCustomName);
            SendSubscriptionCommand(StateCustomName, "state", 250, tagForSubscription);

            GetState();
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            IsSubscribed = false;
            this.LogVerbose("StateCustomName = {customName}", StateCustomName);
            SendUnSubscriptionCommand(StateCustomName, "state", tagForSubscription);
        }

        /// <summary>
        /// Parses subscription-related responses
        /// </summary>
        /// <param name="customName">Subscription identifier</param>
        /// <param name="value">response data to be parsed</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {

            // Check for valid subscription response

            if (customName != StateCustomName) return;
            state = bool.Parse(value);
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

        private const string parsePattern = "[^ ]* (.*)";
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
                this.LogVerbose("Parsing Message: {message}. attributeCode: {attributeCode}", message, attributeCode);
                // Parse an "+OK" message

                var match = parseRegex.Match(message);

                if (!match.Success) return;

                var value = match.Groups[1].Value;

                this.LogDebug("Response: {attributeCode} Value: {value}", attributeCode, value);

                if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;

                if (attributeCode != "state") return;

                state = bool.Parse(value);
                FireFeedbacks();
                IsSubscribed = true;
            }
            catch (Exception e)
            {
                this.LogError("Unable to parse {message}: {exception}", message, e.Message);
                this.LogDebug(e, "Stack Trace: ");
            }
        }

        /// <summary>
        /// Poll state status
        /// </summary>
        public void GetState()
        {
            SendFullCommand("get", "state", string.Empty, 1);
        }

        /// <summary>
        /// Set State On
        /// </summary>
        public void StateOn()
        {
            this.LogVerbose("StateOn sent to {key}", Key);
            SendFullCommand("set", "state", "true", 1);
            GetState();
        }

        /// <summary>
        /// Set State off
        /// </summary>
        public void StateOff()
        {
            SendFullCommand("set", "state", "false", 1);
            GetState();
        }

        /// <summary>
        /// Toggle State value
        /// </summary>
        public void StateToggle()
        {
            if (state)
            {
                SendFullCommand("set", "state", "false", 1);
            }
            else if (!state)
            {
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

            bridge?.AddJoinMap(Key, joinMap);

            if (!Enabled) return;

            this.LogVerbose("Tesira State {key} is Enabled", Key);

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