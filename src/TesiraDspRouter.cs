using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing;
using IRoutingWithFeedback = Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces.IRoutingWithFeedback;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDspRouter : TesiraDspControlPoint, IRoutingWithFeedback, IHasNamedRoutingSlots, ISubscribedComponent
    {
        private int sourceIndex;

        private const string keyFormatter = "{0}--{1}";

        /// <summary>
        /// Collection of IRouting Input Ports
        /// </summary>
        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        /// <summary>
        /// Collection of IRouting Output Ports
        /// </summary>
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        // Named-slot view over InputPorts/OutputPorts for IHasNamedRoutingSlots, fed from the same
        // route feedback as CurrentRoutes.
        private RoutingPortNamedSlots _namedSlots;

        IReadOnlyDictionary<string, IRoutingSlotInfo> IHasNamedRoutingSlots.InputSlots =>
            _namedSlots?.InputSlots ?? new Dictionary<string, IRoutingSlotInfo>();
        IReadOnlyDictionary<string, IRoutingOutputSlotInfo> IHasNamedRoutingSlots.OutputSlots =>
            _namedSlots?.OutputSlots ?? new Dictionary<string, IRoutingOutputSlotInfo>();

        /// <summary>
        /// XSig of all Feedback Names
        /// </summary>
        public StringFeedback SourceNamesFeedback { get; private set; }

        public StringFeedback RoutedSourceNameFeedback { get; private set; }

        public Dictionary<uint, string> SwitcherInputs { get; private set; }

        public Dictionary<uint, string> SwitcherOutputs { get; private set; }

        private string SourceNamesXsig { get; set; }

        private string RoutedSourceName { get; set; }

        private bool ShowRoutedString { get; set; }

        /// <summary>Unused; never configured. Kept for API compatibility.</summary>
        public readonly long PollIntervalMs;

        /// <summary>
        /// Subscription Identifier for Switcher
        /// </summary>
        public string SelectorCustomName { get; private set; }

        private int Destination { get; set; }

        /// <summary>
        /// The route of <see cref="FirstOutput"/>: what the single-route joins (SourceIndex,
        /// RoutedSourceName) report.
        /// </summary>
        private int SourceIndex
        {
            get
            {
                return sourceIndex;
            }
            set
            {
                sourceIndex = value;
                RoutedSourceName = InputLabel((uint)value);
                RoutedSourceNameFeedback.FireUpdate();
                SourceIndexFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///  Feedback for Source Index: the input routed to the first configured output
        /// </summary>
        public IntFeedback SourceIndexFeedback { get; private set; }

        /// <summary>
        /// Input routed to each configured output, keyed by Tesira output number. 0 = cleared.
        /// </summary>
        public Dictionary<uint, IntFeedback> OutputRouteFeedbacks { get; private set; } = new Dictionary<uint, IntFeedback>();

        /// <summary>
        /// Label of the input routed to each configured output, keyed by Tesira output number.
        /// </summary>
        public Dictionary<uint, StringFeedback> OutputRouteNameFeedbacks { get; private set; } = new Dictionary<uint, StringFeedback>();

        // Tesira output number -> routed input. Written from receive threads, read by feedbacks.
        private readonly Dictionary<uint, uint> outputRoutes = new Dictionary<uint, uint>();
        private readonly object routesLock = new object();

        /// <summary>
        /// The lowest configured output number: the output the single-route joins and SetSource use.
        /// </summary>
        private uint FirstOutput => SwitcherOutputs.Count > 0 ? SwitcherOutputs.Keys.Min() : 1;

        /// <summary>
        /// Currently active routes on this device, one per output.
        /// </summary>
        public List<RouteSwitchDescriptor> CurrentRoutes { get; private set; } = new List<RouteSwitchDescriptor>();

        /// <summary>
        /// Raised when the active route on this device changes.
        /// </summary>
        public event RouteChangedEventHandler RouteChanged;

        /// <summary>
        /// The input currently routed to an output, as last reported by the DSP. 0 = cleared or unknown.
        /// </summary>
        public uint GetOutputRoute(uint output)
        {
            lock (routesLock)
            {
                return outputRoutes.TryGetValue(output, out var input) ? input : 0;
            }
        }

        private string InputLabel(uint input) =>
            SwitcherInputs.TryGetValue(input, out var label) && label != null ? label : (input == 0 ? "None" : $"Input {input}");

        private void UpdateOutputRoute(uint output, uint input)
        {
            if (!SwitcherOutputs.ContainsKey(output))
            {
                this.LogDebug("Route for unconfigured output {output} ignored", output);
                return;
            }

            this.LogDebug("Output {output} routed to input {input}", output, input);

            List<RouteSwitchDescriptor> routes;
            lock (routesLock)
            {
                outputRoutes[output] = input;
                routes = BuildCurrentRoutes();
            }
            CurrentRoutes = routes;

            if (OutputRouteFeedbacks.TryGetValue(output, out var routeFeedback)) routeFeedback.FireUpdate();
            if (OutputRouteNameFeedbacks.TryGetValue(output, out var nameFeedback)) nameFeedback.FireUpdate();
            if (output == FirstOutput) SourceIndex = (int)input;

            var outputPort = OutputPorts.FirstOrDefault(p => p.Selector != null && Convert.ToUInt32(p.Selector) == output);
            var inputPort = InputPorts.FirstOrDefault(p => p.Selector != null && Convert.ToUInt32(p.Selector) == input);
            _namedSlots?.HandleRouteChange(outputPort, inputPort, eRoutingSignalType.Audio);
            if (outputPort != null && inputPort != null)
                RouteChanged?.Invoke(this, new RouteSwitchDescriptor(outputPort, inputPort));
        }

        // Call with routesLock held.
        private List<RouteSwitchDescriptor> BuildCurrentRoutes()
        {
            var routes = new List<RouteSwitchDescriptor>();
            foreach (var route in outputRoutes)
            {
                var outputPort = OutputPorts.FirstOrDefault(p => p.Selector != null && Convert.ToUInt32(p.Selector) == route.Key);
                var inputPort = InputPorts.FirstOrDefault(p => p.Selector != null && Convert.ToUInt32(p.Selector) == route.Value);
                if (outputPort != null && inputPort != null)
                    routes.Add(new RouteSwitchDescriptor(outputPort, inputPort));
            }
            return routes;
        }

        /// <summary>
        /// Constructor for Tesira Dsp Switcher Component
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="config">Sqitcher Config Object</param>
        /// <param name="parent">Parent Object</param>
        public TesiraDspRouter(string key, TesiraRouterControlBlockConfig config, TesiraDsp parent)
            : base(config.RouterInstanceTag, string.Empty, config.Index1, 0, parent, string.Format(keyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            SwitcherInputs = new Dictionary<uint, string>();
            SwitcherOutputs = new Dictionary<uint, string>();

            ShowRoutedString = config.ShowRoutedStringFeedback;
            foreach (var input in config.RouterInputs ?? new Dictionary<uint, RoutingPort>())
            {
                SwitcherInputs[input.Key] = input.Value.Label;
            }
            SwitcherInputs[0] = "None";

            foreach (var output in config.RouterOutputs ?? new Dictionary<uint, RoutingPort>())
            {
                if (output.Key == 0)
                {
                    this.LogWarning("Router output 0 ignored: Tesira outputs count from 1");
                    continue;
                }
                SwitcherOutputs.Add(output.Key, output.Value.Label);
            }

            // Legacy single output: its number is index1 (when set), else the next one after routerOutputs.
            // It's an ordinary output from here on: routable, subscribed and reported.
            if (config.RouterOutput != null)
            {
                var legacyOutput = config.Index1 > 0 ? (uint)config.Index1 : (uint)((config.RouterOutputs?.Count ?? 0) + 1);
                if (SwitcherOutputs.ContainsKey(legacyOutput))
                    this.LogWarning("routerOutput ignored: output {output} is already in routerOutputs", legacyOutput);
                else
                    SwitcherOutputs.Add(legacyOutput, config.RouterOutput.Label);
            }

            RoutedSourceNameFeedback = new StringFeedback(Key + "-RoutedSourceNameFeedback", () => RoutedSourceName);
            SourceIndexFeedback = new IntFeedback(Key + "-SourceIndexFeedback", () => SourceIndex);
            SourceNamesFeedback = new StringFeedback(Key + "-SourceNamesFeedback", () => SourceNamesXsig);

            foreach (var output in SwitcherOutputs.Keys)
            {
                var n = output;
                OutputRouteFeedbacks[n] = new IntFeedback($"{Key}-Output{n}RouteFeedback", () => (int)GetOutputRoute(n));
                OutputRouteNameFeedbacks[n] = new StringFeedback($"{Key}-Output{n}RouteNameFeedback", () => InputLabel(GetOutputRoute(n)));
                Feedbacks.Add(OutputRouteFeedbacks[n]);
                Feedbacks.Add(OutputRouteNameFeedbacks[n]);
            }

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();
            _namedSlots = new RoutingPortNamedSlots(InputPorts, OutputPorts);

            Feedbacks.Add(SourceIndexFeedback);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(SourceNamesFeedback);
            Feedbacks.Add(RoutedSourceNameFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

            SelectorCustomName = $"{InstanceTag1}__Router";

            Initialize(config);

        }

        private void Initialize(TesiraRouterControlBlockConfig config)
        {
            this.LogVerbose("Adding Router {key}", Key);

            IsSubscribed = false;

            Label = config.Label;

            Enabled = config.Enabled;

            if (config.RouterInputs == null)
            {
                this.LogWarning("No Router Inputs defined");
                return;
            }

            InputPorts.Add(new RoutingInputPort("None", eRoutingSignalType.Audio, eRoutingPortConnectionType.BackplaneOnly, (uint)0, this));

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

            // SwitcherOutputs already includes a legacy routerOutput (see the constructor).
            foreach (var output in SwitcherOutputs)
            {
                var port = new RoutingOutputPort(output.Value, eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.BackplaneOnly, output.Key, this);
                OutputPorts.Add(port);
            }
        }

        /// <summary>
        /// Subscribe to component
        /// </summary>
        public override void Subscribe()
        {
            IsSubscribed = false;

            AddCustomName(SelectorCustomName);

            SendSubscriptionCommand(SelectorCustomName, "input", 250, 1);
        }

        public override void SendSubscriptionCommand(string customName, string attributeCode, int responseRate, int instanceTag)
        {
            // Subscription string format: InstanceTag subscribe attributeCode Index1 customName responseRate
            // Ex: "RoomLevel subscribe level 1 MyRoomLevel 500"
            if (string.IsNullOrEmpty(customName) || string.IsNullOrEmpty(attributeCode))
            {
                this.LogError("Error: CustomName or AttributeCode is null or empty. {customName}, {attributeCode}, {responseRate}, {instanceTag}", customName, attributeCode, responseRate, instanceTag);
                return;
            }

            // One subscription per output; each publishes the routed input as a single value
            // ("! "Router1__Router-3" 2"), starting with the current route when it's made.
            foreach (var output in SwitcherOutputs.Keys)
            {
                var subscriptionName = RouterProtocol.SubscriptionName(customName, output);

                AddCustomName(subscriptionName);

                var cmd = RouterProtocol.SubscribeCommand(InstanceTag1, output, subscriptionName, responseRate);

                // Tag with RequestingComponent so a timed-out watchdog subscribe check triggers recovery.
                Parent.CommandQueue.EnqueueCommand(new QueuedCommand(cmd, null, null, priority: (int)CommandPriority.Critical, requestingComponent: this));
                this.LogDebug("Sent Subscription Command: {cmd}", cmd);
            }
        }

        public override void SendUnSubscriptionCommand(string customName, string attributeCode, int instanceTag)
        {
            if (string.IsNullOrEmpty(customName) || string.IsNullOrEmpty(attributeCode))
            {
                this.LogError("Error: CustomName or AttributeCode is null or empty. {customName}, {attributeCode}, {instanceTag}", customName, attributeCode, instanceTag);
                return;
            }

            foreach (var output in SwitcherOutputs.Keys)
            {
                var cmd = RouterProtocol.UnsubscribeCommand(InstanceTag1, output, RouterProtocol.SubscriptionName(customName, output));
                Parent.CommandQueue.EnqueueCommand(cmd);
                this.LogDebug("Sent Unsubscription Command: {cmd}", cmd);
            }
        }

        /// <summary>
        /// Unsubscribe from component
        /// </summary>
        public override void Unsubscribe()
        {
            IsSubscribed = false;

            SendUnSubscriptionCommand(SelectorCustomName, "input", 1);
        }

        /// <summary>
        /// Parse subscription-related responses
        /// </summary>
        /// <param name="customName">Subscription Identifier</param>
        /// <param name="value">Response to parse</param>
        public override void ParseSubscriptionMessage(string customName, string value)
        {
            if (!RouterProtocol.TryParseSubscriptionOutput(customName, SelectorCustomName, out var output))
            {
                this.LogDebug("Unexpected subscription name {customName} for router {key}", customName, Key);
                return;
            }

            if (!RouterProtocol.TryParseInput(value, out var input))
            {
                this.LogWarning("Unable to parse route for output {output}: {value}", output, value);
                return;
            }

            IsSubscribed = true;
            UpdateOutputRoute(output, input);
        }

        /// <summary>
        /// parse non-subscription-related responses
        /// </summary>
        /// <param name="attributeCode">Attribute Code Identifier</param>
        /// <param name="message">Response to parse</param>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                this.LogVerbose("Parsing Message: {message}. attributeCode: {attributeCode}", message, attributeCode);
                // Parse an "+OK" message
                const string pattern = "[^ ]* (.*)";

                var match = Regex.Match(message, pattern);

                if (!match.Success) return;
                var value = match.Groups[1].Value;

                this.LogDebug("Response: '{response}' Value: '{value}'", attributeCode, value);

                if (message.Contains("-ERR address not found"))
                {
                    this.LogError("Biamp Error Address not found: {instanceTag}", InstanceTag1);
                    return;
                }

                if (message.IndexOf("+OK", StringComparison.Ordinal) <= -1) return;

                // Only per-output gets carry a route; a set's bare "+OK" has nothing to parse.
                if (!RouterProtocol.TryParseGetOutput(attributeCode, out var output)) return;

                if (!RouterProtocol.TryParseInput(message, out var input))
                {
                    this.LogWarning("Unable to parse route for output {output}: {message}", output, message);
                    return;
                }

                UpdateOutputRoute(output, input);
            }
            catch (Exception e)
            {
                this.LogError("Unable to parse {message}: {exception}", message, e.Message);
                this.LogDebug(e, "Stack Trace: ");
            }

        }

        /// <summary>
        /// Set Source to route
        /// </summary>
        /// <param name="data">Source to route</param>
        public void SetSource(int data)
        {
            if (data < 0)
            {
                this.LogWarning("Unable to route input {input}: inputs count from 0 (0 clears)", data);
                return;
            }

            RouteOutput(FirstOutput, (uint)data);
        }

        /// <summary>
        /// Routes an input to one output and reads the route back. Input 0 clears the output.
        /// </summary>
        /// <param name="output">Tesira output number (from routerOutputs)</param>
        /// <param name="input">Tesira input number (from routerInputs), or 0 to clear</param>
        public void RouteOutput(uint output, uint input)
        {
            if (!SwitcherOutputs.ContainsKey(output))
            {
                this.LogWarning("Unable to route: output {output} is not in routerOutputs", output);
                return;
            }

            if (!SwitcherInputs.ContainsKey(input))
            {
                this.LogWarning("Unable to route: input {input} is not in routerInputs", input);
                return;
            }

            Parent.CommandQueue.EnqueueCommand(new QueuedCommand(RouterProtocol.SetCommand(InstanceTag1, output, input), null, null, priority: (int)CommandPriority.Critical));

            // Changes inside one publish interval can collapse into a single publish, or none, so
            // read the route back rather than rely on the subscription alone.
            PollOutput(output);
        }

        private void PollOutput(uint output)
        {
            Parent.CommandQueue.EnqueueCommand(new QueuedCommand(RouterProtocol.GetCommand(InstanceTag1, output), RouterProtocol.GetAttributeCode(output), this, priority: (int)CommandPriority.Critical));
        }

        /// <summary>
        /// Future use - to set Destination
        /// </summary>
        /// <param name="data"></param>
        public void SetDestination(int data)
        {
            Destination = data;
        }

        public void GetSourceNames()
        {
            if (ShowRoutedString) return;
            SourceNamesXsig = XSigHelper.ClearData();
            SourceNamesFeedback.FireUpdate();
            SourceNamesXsig = string.Empty;

            foreach (var port in InputPorts)
            {
                var input = port;
                var index = Convert.ToUInt16(input.Selector);
                SourceNamesXsig += XSigHelper.CreateByteString(index, input.Key);
            }
            SourceNamesFeedback.FireUpdate();
        }

        /// <summary>
        /// Reads every configured output's route.
        /// </summary>
        public override void DoPoll()
        {
            foreach (var output in SwitcherOutputs.Keys)
            {
                PollOutput(output);
            }
        }

        #region IRouting Members

        /// <summary>
        /// Execute Switch with Essentials Routing
        /// </summary>
        /// <param name="inputSelector">Input Object Data</param>
        /// <param name="outputSelector">Output Object Data</param>
        /// <param name="signalType">Signal Type to Route</param>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            if (signalType != eRoutingSignalType.Audio)
            {
                this.LogWarning("Signal Type {signalType} is not supported. Only Audio routing is supported.", signalType);
                return;
            }

            // Selector may be the port's own Selector (the router index) or, from mobile control's
            // matrix routing, the named slot key - which for these ports is the label, not the
            // index. See RoutingSelectorResolver.
            if (!RoutingSelectorResolver.TryResolveInputIndex(inputSelector, InputPorts, out var input))
            {
                this.LogWarning("Unable to execute switch: {inputSelector} is not a known input", inputSelector);
                return;
            }

            if (!RoutingSelectorResolver.TryResolveIndex(outputSelector, OutputPorts, out var output))
            {
                this.LogWarning("Unable to execute switch: {outputSelector} is not a known output", outputSelector);
                return;
            }

            RouteOutput(output, input);
        }

        /// <summary>
        /// Clears the route to the specified output by selecting input 0 (None).
        /// </summary>
        /// <param name="outputSelector">Output Object Data</param>
        /// <param name="signalType">Signal Type to clear</param>
        public void ClearRoute(object outputSelector, eRoutingSignalType signalType)
        {
            ExecuteSwitch((uint)0, outputSelector, signalType);
        }

        #endregion


        /// <summary>
        /// Links each configured output to its own joins: output N's route (set and feedback) on
        /// routeJoin + N - 1, and its routed input label on nameJoin + N - 1. Outputs above
        /// maxOutputs are logged and not linked.
        /// </summary>
        internal void LinkOutputsToApi(BasicTriList trilist, uint routeJoin, uint nameJoin, uint maxOutputs)
        {
            foreach (var output in SwitcherOutputs.Keys)
            {
                if (output > maxOutputs)
                {
                    this.LogWarning("Router output {output} not bridged: the join map holds outputs 1-{max}", output, maxOutputs);
                    continue;
                }

                var n = output;
                var offset = n - 1;
                OutputRouteFeedbacks[n].LinkInputSig(trilist.UShortInput[routeJoin + offset]);
                OutputRouteNameFeedbacks[n].LinkInputSig(trilist.StringInput[nameJoin + offset]);
                trilist.SetUShortSigAction(routeJoin + offset, u => RouteOutput(n, u));
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraRouterJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraRouterJoinMapAdvancedStandalone>(joinMapSerialized);

            bridge?.AddJoinMap(Key, joinMap);

            if (!Enabled) return;

            this.LogVerbose("Tesira Router {0} is Enabled", Key);

            // Per output: route set and feedback on OutputRoute + N - 1, so output 1 shares the old
            // Index join.
            var routeJoin = joinMap.OutputRoute.JoinNumber;
            var maxOutputs = TesiraRouterJoinMapAdvancedStandalone.MaxOutputs;
            LinkOutputsToApi(trilist, routeJoin, joinMap.OutputRouteName.JoinNumber, maxOutputs);

            // Index (the first output) gets its own link only when it isn't some output's route join;
            // otherwise two outputs would share one analog.
            var indexJoin = joinMap.Index.JoinNumber;
            var indexOutput = SwitcherOutputs.Keys.Where(o => o <= maxOutputs && routeJoin + o - 1 == indexJoin).ToList();
            if (indexOutput.Count == 0)
            {
                var s = this as IRoutingWithFeedback;
                s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[indexJoin]);
                trilist.SetUShortSigAction(indexJoin, u => SetSource(u));
            }
            else if (indexOutput[0] != FirstOutput)
            {
                this.LogWarning("Index join {join} is output {output}'s route join, so it carries output {output}, not the first output {first}",
                    indexJoin, indexOutput[0], indexOutput[0], FirstOutput);
            }

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
