using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;

namespace Tesira_DSP_EPI {
    public class TesiraDspSwitcher : TesiraDspControlPoint, IRoutingWithFeedback, IKeyed {
        private int _sourceIndex { get; set; }

        public string SelectorCustomName { get; private set; }

        private int Source { get; set; }
        private int Destination { get; set; }
        private int SourceIndex {
            get {
                return _sourceIndex;
            }
            set {
                _sourceIndex = value;
                //Fire an Update
            }
        }

        public IntFeedback SourceIndexFeedback { get; private set; }

        public TesiraDspSwitcher(string key, TesiraSwitcherControlBlockConfig config, TesiraDsp parent)
            : base(config.switcherInstanceTag, String.Empty, config.index1, 0, parent) {

            Initialize(key, config);

        }

        public void Initialize(string key, TesiraSwitcherControlBlockConfig config) {
            Key = string.Format("{0}--{1}", Parent.Key, key);

            DeviceManager.AddDevice(this);

            Debug.Console(2, this, "Adding SourceSelector '{0}'", Key);

            IsSubscribed = false;

            Label = config.label;

            SourceIndexFeedback = new IntFeedback(() => _sourceIndex);

            Enabled = config.enabled;
        }

        public override void Subscribe() {
            SelectorCustomName = string.Format("{0}~Selector{1}", this.InstanceTag1, this.Index1);

            SendSubscriptionCommand(SelectorCustomName, "sourceSelection", 250, 1);
        }

        public void ParseSubscriptionMessage(string customName, string value) {

            // Check for valid subscription response

            if (customName == SelectorCustomName) {


                SourceIndex = int.Parse(value);

                SourceIndexFeedback.FireUpdate();
                IsSubscribed = true;
            }

        }

        public override void ParseGetMessage(string attributeCode, string message) {
            try {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message
                string pattern = "[^ ]* (.*)";

                Match match = Regex.Match(message, pattern);

                if (match.Success) {

                    string value = match.Groups[1].Value;

                    Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                    if (message.IndexOf("+OK") > -1) {
                        if (attributeCode == "sourceSelection") {
                            SourceIndex = int.Parse(value);

                            SourceIndexFeedback.FireUpdate();
                        }
                    }
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

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType) {
            if (signalType == eRoutingSignalType.Audio) {
                if (Destination == 0)
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
    }
}
