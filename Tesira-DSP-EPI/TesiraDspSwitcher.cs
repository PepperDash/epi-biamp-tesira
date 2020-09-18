using System;
using System.Globalization;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;

namespace Tesira_DSP_EPI {
    public class TesiraDspSwitcher : TesiraDspControlPoint, IRoutingWithFeedback
    {
        private int _sourceIndex;

        private const string KeyFormatter = "{0}--{1}";

        /// <summary>
        /// Collection of IRouting Input Ports
        /// </summary>
        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        /// <summary>
        /// Collection of IRouting Output Ports
        /// </summary>
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        /// <summary>
        /// Subscription Identifier for Switcher
        /// </summary>
        public string SelectorCustomName { get; private set; }

        private int _source;
		private string Type { get; set; } 
        private int Destination { get; set; }
        private int SourceIndex {
            get {
                return _sourceIndex;
            }
            set {
                _sourceIndex = value;
                SourceIndexFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IntFeedback SourceIndexFeedback { get; private set; }

        /// <summary>
        /// Constructor for Tesira Dsp Switcher Component
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="config">Sqitcher Config Object</param>
        /// <param name="parent">Parent Object</param>
        public TesiraDspSwitcher(string key, TesiraSwitcherControlBlockConfig config, TesiraDsp parent)
            : base(config.SwitcherInstanceTag, String.Empty, config.Index1, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            SourceIndexFeedback = new IntFeedback(Key + "-SourceIndexFeedback", () => SourceIndex);

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            Feedbacks.Add(SourceIndexFeedback);
            Feedbacks.Add(NameFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);

        }

        private void Initialize(TesiraSwitcherControlBlockConfig config) {
			Type = "";
            //DeviceManager.AddDevice(this);

            Debug.Console(2, this, "Adding SourceSelector '{0}'", Key);

            IsSubscribed = false;

            Label = config.Label;
			if (config.Type != null)
			{
				Type = config.Type;
			}

            Enabled = config.Enabled;

            if (config.SwitcherInputs != null)
            {
                foreach (
                    var input in
                        from input in config.SwitcherInputs
                        let inputPort = input.Value
                        let inputPortKey = input.Key
                        select input)
                {
                    InputPorts.Add(new RoutingInputPort(input.Value.Label, eRoutingSignalType.Audio,
                        eRoutingPortConnectionType.BackplaneOnly, input.Key, this));
                }
            }
            if (config.SwitcherOutputs == null) return;
            foreach (
                var output in
                    from output in config.SwitcherOutputs
                    let outputPort = output.Value
                    let outputPortKey = output.Key
                    select output)
            {
                OutputPorts.Add(new RoutingOutputPort(output.Value.Label, eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.BackplaneOnly, output.Key, this));
            }
        }

        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe() {
            SelectorCustomName = string.Format("{0}~Selector{1}", InstanceTag1, Index1);

            SendSubscriptionCommand(SelectorCustomName, "sourceSelection", 250, 1);
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            IsSubscribed = false;

            SelectorCustomName = string.Format("{0}~Selector{1}", InstanceTag1, Index1);

            SendUnSubscriptionCommand(SelectorCustomName, "sourceSelection", 1);
        }

        /// <summary>
        /// Parse subscription-related responses
        /// </summary>
        /// <param name="customName">Subscription Identifier</param>
        /// <param name="value">Response to parse</param>
        public void ParseSubscriptionMessage(string customName, string value) {

            // Check for valid subscription response

            if (customName != SelectorCustomName) return;
            SourceIndex = int.Parse(value);

            IsSubscribed = true;
        }

        /// <summary>
        /// parse non-subscription-related responses
        /// </summary>
        /// <param name="attributeCode">Attribute Code Identifier</param>
        /// <param name="message">Response to parse</param>
        public override void ParseGetMessage(string attributeCode, string message) {
            try {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message
                const string pattern = "[^ ]* (.*)";

                var match = Regex.Match(message, pattern);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                if (message.Contains("-ERR address not found"))
                {
                    Debug.ConsoleWithLog(2, this, "Baimp Error Address not found: '{0}'\n", InstanceTag1);
                    return;
                }

                if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) > -1) {
                    if (attributeCode == "sourceSelection") {
                        SourceIndex = int.Parse(value);
                    }
                }
                if (attributeCode == "input")
                {
                    _source = int.Parse(value);
                }
            }
            catch (Exception e) {
                Debug.Console(2, this, "Unable to parse message: '{0}'\n{1}", message, e);
            }

        }

        /// <summary>
        /// Set Source to route
        /// </summary>
        /// <param name="data">Source to route</param>
        public void SetSource(int data) {
            ExecuteSwitch(data, 0, eRoutingSignalType.Audio);
        }

        /// <summary>
        /// Future use - to set Destination
        /// </summary>
        /// <param name="data"></param>
        public void SetDestination(int data) {
            Destination = data;
        }

        #region IRouting Members

        /// <summary>
        /// Execute Switch with Essentials MagicRouting
        /// </summary>
        /// <param name="inputSelector">Input Object Data</param>
        /// <param name="outputSelector">Output Object Data</param>
        /// <param name="signalType">Signal Type to Route</param>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            if (signalType != eRoutingSignalType.Audio) return;
            if (Destination != 0) return;
            if (Type == "router")
            {
                SendFullCommand("set", "input", Convert.ToString(inputSelector), 1);
                SendFullCommand("get", "input", Index1.ToString(CultureInfo.InvariantCulture), 1);

            }
            else
            {
                SendFullCommand("set", "sourceSelection", Convert.ToString(inputSelector), 1);
            }
        }

        /// <summary>
        /// Execute Numeric Switch with Essentials Magic Routing
        /// </summary>
        /// <param name="inputSelector">Numeric Input Selector</param>
        /// <param name="outputSelector">Numeric Output Selector</param>
        /// <param name="signalType">Signal Type to Route</param>
        public void ExecuteNumericSwitch(ushort inputSelector, ushort outputSelector, eRoutingSignalType signalType)
        {
            if (signalType != eRoutingSignalType.Audio) return;
            if (Destination != 0) return;
            if (Type == "router")
            {
                SendFullCommand("set", "input", Convert.ToString(inputSelector), 1);
                SendFullCommand("get", "input", Index1.ToString(CultureInfo.InvariantCulture), 1);

            }
            else
            {
                SendFullCommand("set", "sourceSelection", Convert.ToString(inputSelector), 1);
            }
        }

        #endregion


        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraSwitcherJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraSwitcherJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            if (!Enabled) return;

            Debug.Console(2, this, "Tesira Switcher {0} is Enabled", Key);

            var s = this as IRoutingWithFeedback;
            s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[joinMap.Index.JoinNumber]);

            trilist.SetUShortSigAction(joinMap.Index.JoinNumber, u => SetSource(u));

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);

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
