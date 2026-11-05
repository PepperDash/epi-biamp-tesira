using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using IRoutingWithFeedback = Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces.IRoutingWithFeedback;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// In-memory router block, a stand-in for <see cref="TesiraDspRouter"/>. Takes the same
    /// <c>routerControlBlocks</c> config and builds the same ports: input 0 "None" plus the
    /// configured inputs, and one output port per configured output (including a legacy
    /// <c>routerOutput</c>). Each output has its own route feedback; a route to input 0 clears it.
    /// Every output starts cleared.
    /// </summary>
    /// <remarks>
    /// A route sent through <see cref="ExecuteSwitch"/> applies at once, as if the DSP confirmed it.
    /// To test feedback for a route changed inside the DSP (for example by a preset), call
    /// <see cref="SimulateRoute"/> from the console.
    /// </remarks>
    public class TesiraDspMockRouter : EssentialsDevice, IRoutingWithFeedback, IHasNamedRoutingSlots, IHasFeedback
    {
        /// <summary>Every route feedback, for <c>devfb</c> on the console.</summary>
        public FeedbackCollection<Feedback> Feedbacks { get; } = new FeedbackCollection<Feedback>();

        private readonly MockRouteTable routes;

        // Serializes each route change together with its publication, so overlapping calls can't
        // publish an older route after a newer one. A route requested from inside a callback (same
        // thread, so the re-entrant lock lets it in) is queued and applied after the current one.
        private readonly object routeChangeLock = new object();
        private readonly Queue<KeyValuePair<uint, uint>> pendingRoutes = new Queue<KeyValuePair<uint, uint>>();
        private bool applyingRoutes;
        private readonly Dictionary<uint, string> inputLabels = new Dictionary<uint, string>();
        private readonly RoutingPortNamedSlots namedSlots;

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; } = new RoutingPortCollection<RoutingInputPort>();
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; } = new RoutingPortCollection<RoutingOutputPort>();

        IReadOnlyDictionary<string, IRoutingSlotInfo> IHasNamedRoutingSlots.InputSlots => namedSlots.InputSlots;
        IReadOnlyDictionary<string, IRoutingOutputSlotInfo> IHasNamedRoutingSlots.OutputSlots => namedSlots.OutputSlots;

        /// <summary>The input routed to the first (lowest) output, as on the real router.</summary>
        public IntFeedback SourceIndexFeedback { get; }

        /// <summary>Label of the input routed to the first output.</summary>
        public StringFeedback RoutedSourceNameFeedback { get; }

        /// <summary>Input routed to each configured output, keyed by output number. 0 = cleared.</summary>
        public Dictionary<uint, IntFeedback> OutputRouteFeedbacks { get; } = new Dictionary<uint, IntFeedback>();

        /// <summary>Label of the input routed to each configured output, keyed by output number.</summary>
        public Dictionary<uint, StringFeedback> OutputRouteNameFeedbacks { get; } = new Dictionary<uint, StringFeedback>();

        public List<RouteSwitchDescriptor> CurrentRoutes { get; private set; } = new List<RouteSwitchDescriptor>();

        public event RouteChangedEventHandler RouteChanged;

        private uint FirstOutput => routes.Outputs.Count > 0 ? routes.Outputs[0] : 1;

        public TesiraDspMockRouter(string key, string name, TesiraRouterControlBlockConfig config)
            : base(key, name)
        {
            config = config ?? new TesiraRouterControlBlockConfig();

            inputLabels[0] = "None";
            foreach (var input in config.RouterInputs ?? new Dictionary<uint, RoutingPort>())
            {
                if (input.Key == 0) continue; // 0 is always "None"
                inputLabels[input.Key] = string.IsNullOrEmpty(input.Value?.Label) ? $"Input {input.Key}" : input.Value.Label;
            }

            var outputLabels = ResolveOutputs(config);
            routes = new MockRouteTable(outputLabels.Keys, inputLabels.Keys);

            foreach (var input in inputLabels.OrderBy(i => i.Key))
                InputPorts.Add(new RoutingInputPort(input.Value, eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.BackplaneOnly, input.Key, this));

            foreach (var output in outputLabels.OrderBy(o => o.Key))
            {
                OutputPorts.Add(new RoutingOutputPort(output.Value, eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.BackplaneOnly, output.Key, this));

                var n = output.Key;
                OutputRouteFeedbacks[n] = new IntFeedback($"{Key}-Output{n}RouteFeedback", () => (int)routes.Get(n));
                OutputRouteNameFeedbacks[n] = new StringFeedback($"{Key}-Output{n}RouteNameFeedback", () => InputLabel(routes.Get(n)));
            }

            namedSlots = new RoutingPortNamedSlots(InputPorts, OutputPorts);

            SourceIndexFeedback = new IntFeedback(Key + "-SourceIndexFeedback", () => (int)routes.Get(FirstOutput));
            RoutedSourceNameFeedback = new StringFeedback(Key + "-RoutedSourceNameFeedback", () => InputLabel(routes.Get(FirstOutput)));

            Feedbacks.Add(SourceIndexFeedback);
            Feedbacks.Add(RoutedSourceNameFeedback);
            foreach (var n in OutputRouteFeedbacks.Keys.OrderBy(o => o))
            {
                Feedbacks.Add(OutputRouteFeedbacks[n]);
                Feedbacks.Add(OutputRouteNameFeedbacks[n]);
            }

            // Every output starts routed to None: publish that through the routing views too, so
            // consumers see it before the first route change.
            CurrentRoutes = BuildCurrentRoutes();
            var none = FindPort(InputPorts, 0);
            foreach (var outputPort in OutputPorts)
                namedSlots.HandleRouteChange(outputPort, none, eRoutingSignalType.Audio);
        }

        /// <summary>
        /// The outputs, as <see cref="TesiraDspRouter"/> builds them: <c>routerOutputs</c> (output 0 is
        /// ignored), plus a legacy <c>routerOutput</c> at <c>index1</c>, or after the highest output.
        /// </summary>
        private Dictionary<uint, string> ResolveOutputs(TesiraRouterControlBlockConfig config)
        {
            var outputs = new Dictionary<uint, string>();
            foreach (var output in config.RouterOutputs ?? new Dictionary<uint, RoutingPort>())
            {
                if (output.Key == 0)
                {
                    this.LogWarning("Router output 0 ignored: Tesira outputs count from 1");
                    continue;
                }
                outputs[output.Key] = string.IsNullOrEmpty(output.Value?.Label) ? $"Output {output.Key}" : output.Value.Label;
            }

            if (config.RouterOutput != null)
            {
                var highest = outputs.Count == 0 ? 0u : outputs.Keys.Max();
                var legacy = config.Index1 > 0 ? (uint)config.Index1 : highest + 1;
                if (outputs.ContainsKey(legacy))
                    this.LogWarning("routerOutput ignored: output {output} is already in routerOutputs", legacy);
                else
                    outputs[legacy] = string.IsNullOrEmpty(config.RouterOutput.Label) ? $"Output {legacy}" : config.RouterOutput.Label;
            }

            return outputs;
        }

        private string InputLabel(uint input) =>
            inputLabels.TryGetValue(input, out var label) ? label : $"Input {input}";

        protected override bool CustomActivate()
        {
            this.LogVerbose("Activated with {inputs} input(s) and outputs {outputs}",
                inputLabels.Count - 1, string.Join(", ", routes.Outputs));
            return base.CustomActivate();
        }

        // ── IRouting ─────────────────────────────────────────────────────────

        /// <summary>
        /// Routes an input to an output. The selectors may be port selectors (numbers) or, from mobile
        /// control's matrix routing, named slot keys, as on the real router. Input 0 clears the output.
        /// </summary>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            if (signalType != eRoutingSignalType.Audio)
            {
                this.LogWarning("Signal Type {signalType} is not supported. Only Audio routing is supported.", signalType);
                return;
            }

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

            this.LogInformation("MOCK route: output {output} to input {input}", output, input);
            ApplyRoute(output, input);
        }

        /// <summary>Clears the route to an output by routing input 0 (None).</summary>
        public void ClearRoute(object outputSelector, eRoutingSignalType signalType)
        {
            ExecuteSwitch((uint)0, outputSelector, signalType);
        }

        /// <summary>
        /// Bench control: simulates a route changed inside the DSP (for example by a preset), so the
        /// route feedback can be tested without a switch request. From the console:
        /// <c>devjson {"deviceKey":"dsp-1--Router1","methodName":"SimulateRoute","params":[2,3]}</c>
        /// routes input 3 to output 2. Input 0 clears the output.
        /// </summary>
        public void SimulateRoute(uint output, uint input)
        {
            this.LogInformation("MOCK DSP-side route: output {output} to input {input}", output, input);
            ApplyRoute(output, input);
        }

        /// <summary>The input routed to an output. 0 = cleared, or an unconfigured output.</summary>
        public uint GetOutputRoute(uint output) => routes.Get(output);

        /// <summary>
        /// Bench control: logs every output's route. From the console:
        /// <c>devjson {"deviceKey":"dsp-1--Router1","methodName":"ReportRoutes"}</c>.
        /// </summary>
        public void ReportRoutes()
        {
            foreach (var route in routes.Snapshot().OrderBy(r => r.Key))
            {
                var outputLabel = FindPort(OutputPorts, route.Key)?.Key ?? $"Output {route.Key}";
                this.LogInformation("MOCK output {output} ({outputLabel}): input {input} ({inputLabel})",
                    route.Key, outputLabel, route.Value, InputLabel(route.Value));
            }
        }

        /// <summary>
        /// Publishes every route feedback even if its value hasn't changed (bootstrap, see
        /// <see cref="TesiraDspMock"/>). A plain FireUpdate() skips an unchanged value, and a route
        /// index of 0 never differs from a new feedback's initial value, so it would never publish.
        /// </summary>
        public void FireUpdate()
        {
            foreach (var feedback in Feedbacks) ForceFire(feedback);
        }

        // Essentials keeps the last published value in a private field; reset it to a value the
        // feedback can't produce so FireUpdate() sees a change (as TesiraDspMockFader does).
        private static readonly FieldInfo IntValueField =
            typeof(IntFeedback).GetField("_IntValue", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StringValueField =
            typeof(StringFeedback).GetField("_StringValue", BindingFlags.NonPublic | BindingFlags.Instance);
        private bool forceFireWarned;

        private void ForceFire(Feedback feedback)
        {
            var field = feedback is IntFeedback ? IntValueField : feedback is StringFeedback ? StringValueField : null;
            if (field != null)
                field.SetValue(feedback, feedback is IntFeedback ? (object)int.MinValue : "\u0000");
            else if (!forceFireWarned)
            {
                forceFireWarned = true;
                this.LogWarning("Can't force-publish {feedback}: Essentials' feedback internals changed; unchanged values won't re-publish", feedback.Key);
            }
            feedback.FireUpdate();
        }

        private void ApplyRoute(uint output, uint input)
        {
            lock (routeChangeLock)
            {
                pendingRoutes.Enqueue(new KeyValuePair<uint, uint>(output, input));
                if (applyingRoutes) return; // requested by a callback below: applied after the current route

                applyingRoutes = true;
                try
                {
                    while (pendingRoutes.Count > 0)
                    {
                        var next = pendingRoutes.Dequeue();
                        ApplyAndPublish(next.Key, next.Value);
                    }
                }
                finally
                {
                    applyingRoutes = false;
                    pendingRoutes.Clear(); // only non-empty if a subscriber threw
                }
            }
        }

        // Call with routeChangeLock held.
        private void ApplyAndPublish(uint output, uint input)
        {
            switch (routes.Route(output, input))
            {
                case RouteResult.UnknownOutput:
                    this.LogWarning("Route ignored: output {output} is not configured", output);
                    return;
                case RouteResult.UnknownInput:
                    this.LogWarning("Route ignored: input {input} is not configured", input);
                    return;
                case RouteResult.Unchanged:
                    return;
            }

            CurrentRoutes = BuildCurrentRoutes();

            OutputRouteFeedbacks[output].FireUpdate();
            OutputRouteNameFeedbacks[output].FireUpdate();
            if (output == FirstOutput)
            {
                SourceIndexFeedback.FireUpdate();
                RoutedSourceNameFeedback.FireUpdate();
            }

            var outputPort = FindPort(OutputPorts, output);
            var inputPort = FindPort(InputPorts, input);
            namedSlots.HandleRouteChange(outputPort, inputPort, eRoutingSignalType.Audio);
            if (outputPort != null && inputPort != null)
                RouteChanged?.Invoke(this, new RouteSwitchDescriptor(outputPort, inputPort));
        }

        // A cleared output (input 0) is reported with the "None" port, as on the real router.
        private List<RouteSwitchDescriptor> BuildCurrentRoutes()
        {
            var current = new List<RouteSwitchDescriptor>();
            foreach (var route in routes.Snapshot())
            {
                var outputPort = FindPort(OutputPorts, route.Key);
                var inputPort = FindPort(InputPorts, route.Value);
                if (outputPort != null && inputPort != null)
                    current.Add(new RouteSwitchDescriptor(outputPort, inputPort));
            }
            return current;
        }

        private static T FindPort<T>(IEnumerable<T> ports, uint selector) where T : PepperDash.Essentials.Core.RoutingPort =>
            ports.FirstOrDefault(p => p.Selector != null && Convert.ToUInt32(p.Selector) == selector);
    }
}
