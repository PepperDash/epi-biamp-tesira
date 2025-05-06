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

#if SERIES4
using PepperDash.Core.Logging;
#endif

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


        /// <summary>
        /// Subscription Identifier for Switcher
        /// </summary>
        public string SelectorCustomName { get; private set; }

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
        public TesiraDspSwitcher(string key, TesiraSwitcherControlBlockConfig config, TesiraDsp parent)
            : base(config.SwitcherInstanceTag, String.Empty, config.Index1, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            SwitcherInputs = new Dictionary<uint, string>();

            ShowRoutedString = config.ShowRoutedStringFeedback;
            foreach (var input in config.SwitcherInputs)
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

        private void Initialize(TesiraSwitcherControlBlockConfig config) {
            Type = "";
            //DeviceManager.AddDevice(this);

#if SERIES4
            this.LogVerbose(string.Format("Adding SourceSelector '{0}'", Key));
#else
            Debug.Console(2, this, "Adding SourceSelector '{0}'", Key);
#endif

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
        public override void Subscribe()
        {
            if (Type == "router")
            {
                IsSubscribed = true;
                if (_pollTimer != null)
                {
                    _pollTimer.Reset(PollIntervalMs);
                }
                return;
            }
            SelectorCustomName = (string.Format("{0}__Selector{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);
            AddCustomName(SelectorCustomName);
            SendSubscriptionCommand(SelectorCustomName, "sourceSelection", 250, 1);
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            IsSubscribed = false;

			SelectorCustomName = (string.Format("{0}__Selector{1}", InstanceTag1, Index1)).Replace(" ", string.Empty);

            SendUnSubscriptionCommand(SelectorCustomName, "sourceSelection", 1);
        }

        /// <summary>
        /// Parse subscription-related responses
        /// </summary>
        /// <param name="customName">Subscription Identifier</param>
        /// <param name="value">Response to parse</param>
        public override void ParseSubscriptionMessage(string customName, string value) {

            // Check for valid subscription response

            if (customName != SelectorCustomName) return;
            SourceIndex = int.Parse(value);

            IsSubscribed = true;
        }

        const string ParsePattern = "[^ ]* (.*)";
        private readonly static Regex ParseRegex = new Regex(ParsePattern);


        /// <summary>
        /// parse non-subscription-related responses
        /// </summary>
        /// <param name="attributeCode">Attribute Code Identifier</param>
        /// <param name="message">Response to parse</param>
        public override void ParseGetMessage(string attributeCode, string message) {
            try {
#if SERIES4
                this.LogVerbose(string.Format("Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode));
#else
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
#endif
                // Parse an "+OK" message

                var match = ParseRegex.Match(message);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

#if SERIES4
                this.LogDebug(string.Format("Response: '{0}' Value: '{1}'", attributeCode, value));
#else
                Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);
#endif

                if (message.Contains("-ERR address not found"))
                {
#if SERIES4
                    this.LogVerbose(string.Format("Biamp Error Address not found: '{0}'\n", InstanceTag1));
#else
                    Debug.ConsoleWithLog(2, this, "Baimp Error Address not found: '{0}'\n", InstanceTag1);
#endif
                    return;
                }

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;
                switch (attributeCode)
                {
                    case "sourceSelection":
                        SourceIndex = int.Parse(value);
                        break;
                    case "input":
                        SourceIndex = int.Parse(value);
                        break;
                }
            }
            catch (Exception e) {
#if SERIES4
                this.LogVerbose(string.Format("Unable to parse message: '{0}'\n{1}", message, e));
#else
                Debug.Console(2, this, "Unable to parse message: '{0}'\n{1}", message, e);
#endif
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

#if SERIES4
                this.LogVerbose(string.Format("{0} {1}", input.Key, new String('-', 50)));
                this.LogVerbose(string.Format(@"
                  input.ParentDevice: {0}
                  input.Selector: {1}
                  input.Selector(Convert.ToUnit16): {2}
                  input.Port: {3}
                  input.ConnectionType: {4}
                  input.Type: {5}", 
                  input.ParentDevice, input.Selector, index, input.Port, input.ConnectionType, input.Type));
                this.LogVerbose(string.Format("{0}", new String('-', 50)));
#else
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
#endif

            }
            SourceNamesFeedback.FireUpdate();
        }

        public override void DoPoll()
        {
            if(Type == "router"){
                SendFullCommand("get", "input", String.Empty, 1);
                _pollTimer.Reset(PollIntervalMs);
                return;
            }
            SendFullCommand("get", "sourceSelection", String.Empty, 1);
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

#if SERIES4
            this.LogVerbose(string.Format("Tesira Switcher {0} is Enabled", Key));
#else
            Debug.Console(2, this, "Tesira Switcher {0} is Enabled", Key);
#endif

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
