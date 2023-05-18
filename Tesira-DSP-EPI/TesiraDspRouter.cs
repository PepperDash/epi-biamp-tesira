using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using Tesira_DSP_EPI.Extensions;
using IRoutingWithFeedback = Tesira_DSP_EPI.Interfaces.IRoutingWithFeedback;

namespace Tesira_DSP_EPI {
    public class TesiraDspRouter : TesiraDspControlPoint, IRoutingWithFeedback
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
        /// XSig of all Feedback Names
        /// </summary>
        public StringFeedback SourceNamesFeedback { get; private set; }

        public StringFeedback RoutedSourceNameFeedback { get; private set; }

        public Dictionary<uint, string> SwitcherInputs { get; private set; }

        private readonly CTimer _pollTimer;
        
        private string SourceNamesXsig { get; set; }

        private string RoutedSourceName { get; set; }

        private bool ShowRoutedString { get; set; }

        public readonly long PollIntervalMs;

        private string Type { get; set; } 
        private int Destination { get; set; }
        private int SourceIndex {
            get {
                return _sourceIndex;
            }
            set {
                _sourceIndex = value;
                RoutedSourceName = SwitcherInputs[(uint)value] ?? "None";
                RoutedSourceNameFeedback.FireUpdate();
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
        public TesiraDspRouter(string key, TesiraRouterControlBlockConfig config, TesiraDsp parent)
            : base(config.RouterInstanceTag, String.Empty, config.Index1, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            SwitcherInputs = new Dictionary<uint, string>();

            ShowRoutedString = config.ShowRoutedStringFeedback;
            foreach (var input in config.RouterInputs)
            {
                SwitcherInputs.Add(input.Key, input.Value.Label);
            }
            SwitcherInputs.Add(0, "None");

            PollIntervalMs = config.PollIntervalMs ?? 90000;

            //if (Type == "router")
            //{
            _pollTimer = new CTimer(o => DoPoll(), Timeout.Infinite);   //can only be assigned in constructor
            //}

            RoutedSourceNameFeedback = new StringFeedback(Key + "-RoutedSourceNameFeedback", () => RoutedSourceName);
            SourceIndexFeedback = new IntFeedback(Key + "-SourceIndexFeedback", () => SourceIndex);
            SourceNamesFeedback = new StringFeedback(Key + "-SourceNamesFeedback", () => SourceNamesXsig);

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            Feedbacks.Add(SourceIndexFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(SourceNamesFeedback);
            Feedbacks.Add(RoutedSourceNameFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

            Initialize(config);

        }

        private void Initialize(TesiraRouterControlBlockConfig config)
        {
			Type = "";
            //DeviceManager.AddDevice(this);

            Debug.Console(2, this, "Adding SourceSelector '{0}'", Key);

            IsSubscribed = false;

            Label = config.Label;

            Enabled = config.Enabled;

            if (config.RouterInputs != null)
            {
                foreach (
                    var input in
                        from input in config.RouterInputs
                        let inputPort = input.Value
                        let inputPortKey = input.Key
                        select input)
                {
                    InputPorts.Add(new RoutingInputPort(input.Value.Label, eRoutingSignalType.Audio,
                        eRoutingPortConnectionType.BackplaneOnly, input.Key, this));
                }
            }
            if (config.RouterOutput == null) return;
            var output = config.RouterOutput;
            OutputPorts.Add(new RoutingOutputPort(output.Label, eRoutingSignalType.Audio,
                eRoutingPortConnectionType.BackplaneOnly, 1, this));
        }

        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe()
        {
            DoPoll();
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            _pollTimer.Stop();
        }

        /// <summary>
        /// Parse subscription-related responses
        /// </summary>
        /// <param name="customName">Subscription Identifier</param>
        /// <param name="value">Response to parse</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
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

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;

                SourceIndex = int.Parse(value);
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

        public void GetSourceNames()
        {
            if (ShowRoutedString) return;
            SourceNamesXsig = XSigHelper.ClearData();
            SourceNamesFeedback.FireUpdate();
            SourceNamesXsig = String.Empty;

            foreach (var port in InputPorts)
            {

                var input = port;                                
                var index = Convert.ToUInt16(input.Selector);
                SourceNamesXsig += XSigHelper.CreateByteString(index, input.Key);

                Debug.Console(2, this, "{0} {1}", input.Key, new String('-', 50));
                Debug.Console(2, this, @"    
                  input.ParentDevice: {0}
                  input.Selector: {1}
                  input.Selector(Convert.ToUnit16): {2}
                  input.Port: {3}
                  input.ConnectionType: {4}
                  input.Type: {5}", 
                  input.ParentDevice, input.Selector, index, input.Port, input.ConnectionType, input.Type);
                              Debug.Console(2, this, "{0}", new String('-', 50));

            }
            SourceNamesFeedback.FireUpdate();
        }

        public override void DoPoll()
        {
            SendFullCommand("get", "input", String.Empty, 1);
            _pollTimer.Reset(PollIntervalMs);
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
                DoPoll();

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
            trilist.SetSigTrueAction(joinMap.Poll.JoinNumber, DoPoll);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);
            if (ShowRoutedString) RoutedSourceNameFeedback.LinkInputSig(trilist.StringInput[joinMap.RouteOrSource.JoinNumber]);

            GetSourceNames();

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

                GetSourceNames();

            };
        }

    }
}
