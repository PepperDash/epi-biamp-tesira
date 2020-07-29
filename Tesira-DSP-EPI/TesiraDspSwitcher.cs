using System;
using System.Globalization;
using Crestron.SimplSharpPro.DeviceSupport;
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


        public string SelectorCustomName { get; private set; }
		
        private int Source { get; set; }
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

        public IntFeedback SourceIndexFeedback { get; private set; }

        public TesiraDspSwitcher(string key, TesiraSwitcherControlBlockConfig config, TesiraDsp parent)
            : base(config.SwitcherInstanceTag, String.Empty, config.Index1, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            SourceIndexFeedback = new IntFeedback(Key + "-SourceIndexFeedback",() => _sourceIndex);

            Feedbacks.Add(SourceIndexFeedback);
            Feedbacks.Add(NameFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);

        }

        public void Initialize(TesiraSwitcherControlBlockConfig config) {
			Type = "";
            //DeviceManager.AddDevice(this);

            Debug.Console(2, this, "Adding SourceSelector '{0}'", Key);

            IsSubscribed = false;

            Label = config.Label;
			if (config.Type != null)
			{
				Type = config.Type;
			}
			else
			{
				
			}

            Enabled = config.Enabled;
        }

        public override void Subscribe() {
            SelectorCustomName = string.Format("{0}~Selector{1}", InstanceTag1, Index1);

            SendSubscriptionCommand(SelectorCustomName, "sourceSelection", 250, 1);
        }

        public override void Unsubscribe()
        {
            SelectorCustomName = string.Format("{0}~Selector{1}", InstanceTag1, Index1);

            SendUnSubscriptionCommand(SelectorCustomName, "sourceSelection", 1);
        }

        public void ParseSubscriptionMessage(string customName, string value) {

            // Check for valid subscription response

            if (customName != SelectorCustomName) return;
            SourceIndex = int.Parse(value);

            IsSubscribed = true;
        }

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
                    Source = int.Parse(value);
                }
            }
            catch (Exception e) {
                Debug.Console(2, this, "Unable to parse message: '{0}'\n{1}", message, e);
            }

        }


        public void SetSource(int data) {
            ExecuteSwitch(data, 0, eRoutingSignalType.Audio);
        }

        //future use
        public void SetDestination(int data) {
            Destination = data;
        }

        #region IRouting Members

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

        #region IRoutingInputs Members

        public RoutingPortCollection<RoutingInputPort> InputPorts {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region IRoutingOutputs Members

        public RoutingPortCollection<RoutingOutputPort> OutputPorts {
            get { throw new NotImplementedException(); }
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
