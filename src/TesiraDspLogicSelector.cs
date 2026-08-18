using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using IRoutingWithFeedback = Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces.IRoutingWithFeedback;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Tesira DSP Logic Selector component - provides switcher functionality using
    /// interlocked boolean state channels (Logic Selector blocks in Biamp Tesira).
    /// </summary>
    public class TesiraDspLogicSelector : TesiraDspControlPoint, IRoutingWithFeedback
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

        public Dictionary<uint, string> LogicSelectorInputs { get; private set; }

        private string SourceNamesXsig { get; set; }

        private string RoutedSourceName { get; set; }

        private bool ShowRoutedString { get; set; }

        /// <summary>
        /// Maps channel index to subscription custom name
        /// </summary>
        private readonly Dictionary<uint, string> _channelCustomNames = new Dictionary<uint, string>();

        /// <summary>
        /// Maps subscription custom name to channel index (reverse lookup)
        /// </summary>
        private readonly Dictionary<string, uint> _customNameToChannelIndex = new Dictionary<string, uint>();

        private int SourceIndex
        {
            get
            {
                return _sourceIndex;
            }
            set
            {
                _sourceIndex = value;
                string name;
                RoutedSourceName = value >= 0 && LogicSelectorInputs.TryGetValue((uint)value, out name) ? name : "None";
                RoutedSourceNameFeedback.FireUpdate();
                SourceIndexFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Feedback for the currently selected source index
        /// </summary>
        public IntFeedback SourceIndexFeedback { get; private set; }

        /// <summary>
        /// Constructor for Tesira DSP Logic Selector Component
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="config">Logic Selector Config Object</param>
        /// <param name="parent">Parent DSP Object</param>
        public TesiraDspLogicSelector(string key, TesiraLogicSelectorControlBlockConfig config, TesiraDsp parent)
            : base(config.LogicSelectorInstanceTag, String.Empty, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            LogicSelectorInputs = new Dictionary<uint, string>();

            ShowRoutedString = config.ShowSelectedStringFeedback;
            foreach (var input in config.LogicSelectorInputs)
            {
                LogicSelectorInputs.Add(input.Key, input.Value.Label);
            }
            LogicSelectorInputs.Add(0, "None");

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

        private void Initialize(TesiraLogicSelectorControlBlockConfig config)
        {
            this.LogVerbose("Adding LogicSelector {key}", Key);
            IsSubscribed = false;

            Label = config.Label;
            Enabled = config.Enabled;

            foreach (var input in LogicSelectorInputs)
            {
                if (input.Key == 0) continue;
                this.LogVerbose("Adding Input Port {key} with Selector {value}", input.Value, input.Key);
                InputPorts.Add(new RoutingInputPort(input.Value, eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.BackplaneOnly, input.Key, this));
            }

            OutputPorts.Add(new RoutingOutputPort("output", eRoutingSignalType.Audio,
                eRoutingPortConnectionType.BackplaneOnly, 1, this));
        }

        /// <summary>
        /// Subscribe to all input channel states
        /// </summary>
        public override void Subscribe()
        {
            _channelCustomNames.Clear();
            _customNameToChannelIndex.Clear();

            foreach (var input in LogicSelectorInputs)
            {
                if (input.Key == 0) continue;

                var channelIndex = input.Key;
                var customName = string.Format("{0}__LogicSel{1}", InstanceTag1, channelIndex)
                    .Replace(" ", string.Empty);

                _channelCustomNames[channelIndex] = customName;
                _customNameToChannelIndex[customName] = channelIndex;
                AddCustomName(customName);

                // Format: "InstanceTag" subscribe state <channelIndex> customName responseRate
                var cmd = string.Format("\"{0}\" subscribe state {1} {2} {3}",
                    InstanceTag1, channelIndex, customName, 250);
                Parent.CommandQueue.EnqueueCommand(cmd, priority: (int)CommandPriority.Critical);
            }
        }

        /// <summary>
        /// Unsubscribe from all input channel states
        /// </summary>
        public override void Unsubscribe()
        {
            IsSubscribed = false;

            foreach (var kvp in _channelCustomNames)
            {
                var channelIndex = kvp.Key;
                var customName = kvp.Value;

                // Format: "InstanceTag" unsubscribe state <channelIndex> customName
                var cmd = string.Format("\"{0}\" unsubscribe state {1} {2}",
                    InstanceTag1, channelIndex, customName);
                Parent.CommandQueue.EnqueueCommand(cmd, priority: (int)CommandPriority.Critical);
            }
        }

        /// <summary>
        /// Parse subscription-related responses from Logic Selector channels
        /// </summary>
        /// <param name="customName">Subscription identifier</param>
        /// <param name="value">Response value ("true" or "false")</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
            uint channelIndex;
            if (!_customNameToChannelIndex.TryGetValue(customName, out channelIndex)) return;

            bool stateValue;
            if (!bool.TryParse(value, out stateValue)) return;

            this.LogVerbose("Logic Selector subscription: channel {channelIndex} = {value}", channelIndex, stateValue);

            if (stateValue)
            {
                SourceIndex = (int)channelIndex;
                IsSubscribed = true;
            }
        }

        private const string ParsePattern = "[^ ]* (.*)";
        private static readonly Regex ParseRegex = new Regex(ParsePattern);

        /// <summary>
        /// Parse non-subscription-related responses
        /// </summary>
        /// <param name="attributeCode">Attribute Code Identifier</param>
        /// <param name="message">Response to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                this.LogVerbose("Parsing Message - '{message}' : attributeCode={attributeCode}", message, attributeCode);

                var match = ParseRegex.Match(message);
                if (!match.Success) return;

                var value = match.Groups[1].Value;

                this.LogDebug("Response: '{attributeCode}' Value: '{value}'", attributeCode, value);

                if (message.Contains("-ERR address not found"))
                {
                    this.LogError("Biamp Error Address not found: '{instanceTag}'", InstanceTag1);
                    return;
                }

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;

                if (attributeCode != "state") return;

                bool stateValue;
                if (!bool.TryParse(value, out stateValue)) return;

                // The get response doesn't include channel index here; polling handles each individually
            }
            catch (Exception e)
            {
                this.LogError("Error parsing message: '{message}': {exception}", message, e.Message);
                this.LogVerbose(e, "Exception", e);
            }
        }

        /// <summary>
        /// Set the selected source by channel index
        /// </summary>
        /// <param name="data">Channel index to select (1-based)</param>
        public void SetSource(int data)
        {
            ExecuteSwitch(data, 1, eRoutingSignalType.Audio);
        }

        /// <summary>
        /// Get XSig-encoded source names for the bridge
        /// </summary>
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

                this.LogVerbose("{inputPortKey} {separator}", input.Key, new string('-', 50));
            }
            SourceNamesFeedback.FireUpdate();
        }

        /// <summary>
        /// Poll all channel states
        /// </summary>
        public override void DoPoll()
        {
            foreach (var input in LogicSelectorInputs)
            {
                if (input.Key == 0) continue;

                var channelIndex = input.Key;
                var cmd = string.Format("\"{0}\" get state {1}", InstanceTag1, channelIndex);
                Parent.CommandQueue.EnqueueCommand(
                    new QueuedCommand(cmd, "state", this, priority: (int)CommandPriority.Normal));
            }
        }

        #region IRouting Members

        /// <summary>
        /// Execute Switch with Essentials MagicRouting
        /// </summary>
        /// <param name="inputSelector">Input channel index to select</param>
        /// <param name="outputSelector">Output selector (unused)</param>
        /// <param name="signalType">Signal Type to Route</param>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            if (!signalType.HasFlag(eRoutingSignalType.Audio) && !signalType.HasFlag(eRoutingSignalType.SecondaryAudio)) return;

            var channelIndex = Convert.ToInt32(inputSelector);
            // Format: "InstanceTag" set state <channelIndex> true
            var cmd = string.Format("\"{0}\" set state {1} true", InstanceTag1, channelIndex);
            Parent.CommandQueue.EnqueueCommand(
                new QueuedCommand(cmd, "state", this, priority: (int)CommandPriority.Critical));
        }

        /// <summary>
        /// Execute Numeric Switch with Essentials Magic Routing
        /// </summary>
        /// <param name="inputSelector">Numeric channel index to select</param>
        /// <param name="outputSelector">Numeric Output Selector (unused)</param>
        /// <param name="signalType">Signal Type to Route</param>
        public void ExecuteNumericSwitch(ushort inputSelector, ushort outputSelector, eRoutingSignalType signalType)
        {
            if (!signalType.HasFlag(eRoutingSignalType.Audio)) return;

            var cmd = string.Format("\"{0}\" set state {1} true", InstanceTag1, inputSelector);
            Parent.CommandQueue.EnqueueCommand(
                new QueuedCommand(cmd, "state", this, priority: (int)CommandPriority.Critical));
        }

        #endregion

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraSwitcherJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraSwitcherJoinMapAdvancedStandalone>(joinMapSerialized);

            bridge?.AddJoinMap(Key, joinMap);

            if (!Enabled) return;

            this.LogVerbose("Tesira Logic Selector {key} is Enabled", Key);

            var s = this as IRoutingWithFeedback;
            s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[joinMap.Index.JoinNumber]);

            trilist.SetUShortSigAction(joinMap.Index.JoinNumber, u => SetSource(u));
            trilist.SetSigTrueAction(joinMap.Poll.JoinNumber, DoPoll);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);
            if (ShowRoutedString) RoutedSourceNameFeedback.LinkInputSig(trilist.StringInput[joinMap.RouteOrSource.JoinNumber]);
            else SourceNamesFeedback.LinkInputSig(trilist.StringInput[joinMap.RouteOrSource.JoinNumber]);

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
