using System;
using System.Text.RegularExpressions;
using System.Timers;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    //Mixer1 get crosspointLevelState 1 1
    //Mixer1 set crosspointLevelState 1 1 true
    //Mixer1 toggle crosspointLevelState 1 1

    public class TesiraDspCrosspointState : TesiraDspControlPoint
    {
        public string AttributeCode = "crosspointLevelState";

        private bool state;

        private System.Timers.Timer pollTimer;

        private readonly bool pollEnable;

        private readonly long pollTime;

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
                string.Format(TesiraDsp.KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            Label = config.Label;
            Enabled = config.Enabled;

            CrosspointStateFeedback = new BoolFeedback(Key + "-CrosspointStateFeedback", () => state);

            Feedbacks.Add(CrosspointStateFeedback);
            Feedbacks.Add(NameFeedback);
            parent.Feedbacks.AddRange(Feedbacks);

            if (!config.Enabled) return;
            DeviceManager.AddDevice(this);
            pollEnable = config.PollEnable;
            if (pollEnable)
            {
                pollTime = config.PollTimeMs < 10000 ? 10000 : config.PollTimeMs;
            }

        }

        /// <summary>
        /// Get Initial Values for Control.  Control does not subscribe.
        /// </summary>
        public override void Subscribe()
        {
            GetState();

            if (!pollEnable) return;
            if (pollTimer == null)
            {
                pollTimer = new System.Timers.Timer(pollTime);
                pollTimer.Elapsed += (sender, e) => GetState();
                pollTimer.AutoReset = true;
                pollTimer.Start();
            }
        }

        public override void Unsubscribe()
        {
            pollTimer = null;
        }

        /// <summary>
        /// Poll Component State Value
        /// </summary>
        public void GetState()
        {
            this.LogVerbose("GetState sent to {key}", Key);
            SendFullCommand("get", AttributeCode, string.Empty, 1);
        }

        /// <summary>
        /// Set Component State to On
        /// </summary>
        public void StateOn()
        {
            this.LogVerbose("StateOn sent to {key}", Key);
            SendFullCommand("set", AttributeCode, "true", 1);
            GetState();
        }

        /// <summary>
        /// Set Component State to Off
        /// </summary>
        public void StateOff()
        {
            this.LogVerbose("StateOff sent to {key}", Key);
            SendFullCommand("set", AttributeCode, "false", 1);
            GetState();
        }

        /// <summary>
        /// Toggle Component State
        /// </summary>
        public void StateToggle()
        {
            this.LogVerbose("StateToggle sent to {key}", Key);
            SendFullCommand("toggle", AttributeCode, String.Empty, 1);
            GetState();
        }

        private const string parsePattern = "[^ ]* (.*)";
        private readonly static Regex parseRegex = new Regex(parsePattern);


        /// <summary>
        /// Parse non-subscription data
        /// </summary>
        /// <param name="attributeCode">Attribute code of Component</param>
        /// <param name="message">Data to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                this.LogVerbose("Parsing Message: {message}. AttributeCode: {attributeCode}", message, attributeCode);
                // Parse an "+OK" message

                var match = parseRegex.Match(message);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                this.LogVerbose("xPoint Response: {attributeCode} Value: {value}", attributeCode, value);

                if (message.Contains("StandardMixerInterface"))
                {

                    AttributeCode = string.Format("crosspoint {0} {1}", Index1, Index2);
                    this.LogVerbose("StandardMixerInterface: {attributeCode}", AttributeCode);
                    GetState();
                    return;
                }

                if (message.Equals("+OK", StringComparison.OrdinalIgnoreCase)) { return; }

                if (!attributeCode.Equals(AttributeCode, StringComparison.InvariantCultureIgnoreCase)) return;
                state = bool.Parse(value);
                this.LogVerbose("New Value: {state}", state);
                CrosspointStateFeedback.FireUpdate();
            }

            catch (Exception e)
            {
                this.LogError("Unable to parse {message}: {exception}", message, e.Message);
                this.LogDebug(e, "Stack Trace: ");
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

            this.LogDebug("Linking to Trilist {trilistId:X}", trilist.ID);

            this.LogDebug("Tesira Crosspoint State {key} connect", Key);

            if (!Enabled) return;

            this.LogDebug("Adding Crosspoint State ControlPoint {key} | JoinStart:{joinStart}", Key, joinMap.Label.JoinNumber);
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