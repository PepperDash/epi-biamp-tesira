using System;
using System.Collections.Generic;
using System.Linq;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using IRoutingWithFeedback = Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces.IRoutingWithFeedback;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// In-memory source selector or switcher block, a stand-in for <see cref="TesiraDspSourceSelector"/>
    /// (<c>sourceSelectorControlBlocks</c>) and <see cref="TesiraDspSwitcher"/>
    /// (<c>switcherControlBlocks</c>). It builds the same ports from the same
    /// <see cref="SelectorPortLayout"/> and routes like them: one selection for the block, set by
    /// <see cref="ExecuteSwitch"/> whatever the output, cleared by <see cref="ClearRoute"/> (input 0),
    /// and reported through <see cref="CurrentRoutes"/>, <see cref="RouteChanged"/> and the named
    /// routing slots. A selection the config doesn't list is accepted, as the DSP accepts it, and
    /// reported as "Input N" with no route.
    /// </summary>
    /// <remarks>
    /// It also lists its inputs through <see cref="IHasInputs{T}"/> for front ends that select from a
    /// list; selecting an item routes it, and a route updates the list, so both views agree.
    /// A selection applies at once, as if the DSP confirmed it. To test feedback for a selection made
    /// inside the DSP (for example by a preset), call <see cref="SimulateSelection"/> from the console.
    /// </remarks>
    public class TesiraDspMockSourceSelector : EssentialsDevice, IRoutingWithFeedback, IHasNamedRoutingSlots,
        IHasFeedback, IHasInputs<string>
    {
        private readonly SelectorPortLayout layout;
        private readonly RoutingPortNamedSlots namedSlots;
        private readonly MockSourceInputs items;

        // Selections are applied one at a time, in order, with no lock held while they're published.
        private readonly MockChangeQueue<uint> selections;

        // Guards the selected input, which is read from any thread. Never held while publishing.
        private readonly object stateLock = new object();
        private uint selectedInput;
        private bool forceFireWarned;

        /// <summary>Every feedback, for <c>devfb</c> on the console.</summary>
        public FeedbackCollection<Feedback> Feedbacks { get; } = new FeedbackCollection<Feedback>();

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; } = new RoutingPortCollection<RoutingInputPort>();
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; } = new RoutingPortCollection<RoutingOutputPort>();

        IReadOnlyDictionary<string, IRoutingSlotInfo> IHasNamedRoutingSlots.InputSlots => namedSlots.InputSlots;
        IReadOnlyDictionary<string, IRoutingOutputSlotInfo> IHasNamedRoutingSlots.OutputSlots => namedSlots.OutputSlots;

        /// <summary>The selected input. 0 = none.</summary>
        public IntFeedback SourceIndexFeedback { get; }

        /// <summary>Label of the selected input ("None" when cleared, "Input N" when not configured).</summary>
        public StringFeedback RoutedSourceNameFeedback { get; }

        public List<RouteSwitchDescriptor> CurrentRoutes { get; private set; } = new List<RouteSwitchDescriptor>();

        public event RouteChangedEventHandler RouteChanged;

        public ISelectableItems<string> Inputs => items;

        public TesiraDspMockSourceSelector(string key, string name, SelectorPortLayout layout)
            : base(key, name)
        {
            this.layout = layout;
            selections = new MockChangeQueue<uint>(ApplySelection);

            foreach (var problem in layout.Problems)
                this.LogWarning("Config: {problem}", problem);

            foreach (var input in layout.Inputs)
                InputPorts.Add(new RoutingInputPort(input.Label, eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.BackplaneOnly, input.Index, this));

            foreach (var output in layout.Outputs)
                OutputPorts.Add(new RoutingOutputPort(output.Label, eRoutingSignalType.Audio,
                    eRoutingPortConnectionType.BackplaneOnly, output.Index, this));

            namedSlots = new RoutingPortNamedSlots(InputPorts, OutputPorts);

            SourceIndexFeedback = new IntFeedback(Key + "-SourceIndexFeedback", () => (int)SelectedInput);
            RoutedSourceNameFeedback = new StringFeedback(Key + "-RoutedSourceNameFeedback", () => layout.LabelFor(SelectedInput));
            Feedbacks.Add(SourceIndexFeedback);
            Feedbacks.Add(RoutedSourceNameFeedback);

            items = new MockSourceInputs();
            var itemsByKey = new Dictionary<string, ISelectableItem>();
            foreach (var input in layout.Inputs.Where(i => i.Index != 0))
                itemsByKey[input.ItemKey] = new MockSource(input.ItemKey, input.Label, input.Index, Select);
            items.Items = itemsByKey;

            selectedInput = layout.InitialInput;

            // Publish the starting selection through every view before the first change.
            SyncItems();
            CurrentRoutes = BuildCurrentRoutes();
            namedSlots.HandleRouteChange(OutputPorts.FirstOrDefault(), MockFeedback.FindPort(InputPorts, SelectedInput), eRoutingSignalType.Audio);
        }

        /// <summary>The selected input. 0 = none.</summary>
        public uint SelectedInput
        {
            get { lock (stateLock) return selectedInput; }
        }

        protected override bool CustomActivate()
        {
            this.LogVerbose("Activated with inputs {inputs} and outputs {outputs}",
                string.Join(", ", InputPorts.Select(p => p.Key)), string.Join(", ", OutputPorts.Select(p => p.Key)));
            if (OutputPorts.Count == 0)
                this.LogVerbose("No outputs configured: routes can't end on this block, as on the real switcher");
            return base.CustomActivate();
        }

        // ── IRouting ─────────────────────────────────────────────────────────

        /// <summary>
        /// Selects an input. The selector may be a port selector (number) or, from mobile control's
        /// matrix routing, a named slot key, as on the real blocks. The output is ignored: the block
        /// has one selection. Input 0 clears it. As on the real blocks, a source selector acts on any
        /// signal type that includes audio, a switcher only on exactly audio.
        /// </summary>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            if (!layout.AcceptsSignalType(signalType))
            {
                this.LogWarning("Signal Type {signalType} is not supported by this {kind}; the real block ignores it too.",
                    signalType, layout.Kind);
                return;
            }

            if (!RoutingSelectorResolver.TryResolveInputIndex(inputSelector, InputPorts, out var input))
            {
                this.LogWarning("Unable to execute switch: {inputSelector} is not a known input", inputSelector);
                return;
            }

            this.LogInformation("MOCK selection: input {input} ({label})", input, layout.LabelFor(input));
            Select(input);
        }

        /// <summary>Clears the selection by selecting input 0 (None), with the same signal type rule as a switch.</summary>
        public void ClearRoute(object outputSelector, eRoutingSignalType signalType)
        {
            ExecuteSwitch((uint)0, outputSelector, signalType);
        }

        /// <summary>
        /// Bench control: simulates a selection made inside the DSP (for example by a preset), so the
        /// feedback can be tested without a switch request. From the console:
        /// <c>devjson {"deviceKey":"dsp-1--selector1","methodName":"SimulateSelection","params":[2]}</c>
        /// selects input 2. Input 0 clears it. An input the config doesn't list is reported as "Input N".
        /// </summary>
        public void SimulateSelection(uint input)
        {
            this.LogInformation("MOCK DSP-side selection: input {input} ({label})", input, layout.LabelFor(input));
            Select(input);
        }

        /// <summary>
        /// Re-publishes every feedback and the input list, even if nothing changed (bootstrap, see
        /// <see cref="TesiraDspMock"/>).
        /// </summary>
        public void FireUpdate()
        {
            foreach (var feedback in Feedbacks)
            {
                if (!MockFeedback.ForceFire(feedback) && !forceFireWarned)
                {
                    forceFireWarned = true;
                    this.LogWarning("Can't force-publish {feedback}: Essentials' feedback internals changed; unchanged values won't re-publish", feedback.Key);
                }
            }
            items.FireUpdate();
        }

        private void Select(uint input) => selections.Post(input);

        // Runs on the thread that is applying selections, one at a time, with no lock held while publishing.
        private void ApplySelection(uint input)
        {
            lock (stateLock)
            {
                if (selectedInput == input) return;
                selectedInput = input;
            }

            CurrentRoutes = BuildCurrentRoutes();

            // As the real blocks: route feedback on the first output, and RouteChanged with the current
            // route, which is null when nothing routable is selected (a cleared switcher, or an input the
            // config doesn't list). A subscriber that throws doesn't stop the others.
            var outputPort = OutputPorts.FirstOrDefault();
            var inputPort = MockFeedback.FindPort(InputPorts, input);
            Publish("source feedback", input, () =>
            {
                SourceIndexFeedback.FireUpdate();
                RoutedSourceNameFeedback.FireUpdate();
            });
            Publish("input list", input, SyncItems);
            Publish("named slots", input, () => namedSlots.HandleRouteChange(outputPort, inputPort, eRoutingSignalType.Audio));
            Publish("RouteChanged", input, () => RouteChanged?.Invoke(this, CurrentRoutes.FirstOrDefault()));
        }

        private void Publish(string what, uint input, Action publish)
        {
            try
            {
                publish();
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Error publishing {what} for selection of input {input}", what, input);
            }
        }

        private List<RouteSwitchDescriptor> BuildCurrentRoutes()
        {
            var current = new List<RouteSwitchDescriptor>();
            var outputPort = OutputPorts.FirstOrDefault();
            var inputPort = MockFeedback.FindPort(InputPorts, SelectedInput);
            if (outputPort != null && inputPort != null)
                current.Add(new RouteSwitchDescriptor(outputPort, inputPort));
            return current;
        }

        private void SyncItems()
        {
            var selected = SelectedInput;
            string currentKey = null;
            foreach (var item in items.Items.Values.OfType<MockSource>())
            {
                item.SetSelectedInternal(item.Index == selected);
                if (item.Index == selected) currentKey = item.Key;
            }
            items.CurrentItem = currentKey;
        }

        // ── inner types ──────────────────────────────────────────────────────

        private sealed class MockSourceInputs : ISelectableItems<string>
        {
            private Dictionary<string, ISelectableItem> _items = new Dictionary<string, ISelectableItem>();
            private string _currentItem;

            public event EventHandler ItemsUpdated;
            public event EventHandler CurrentItemChanged;

            public Dictionary<string, ISelectableItem> Items
            {
                get { return _items; }
                set
                {
                    if (_items == value) return;
                    _items = value ?? new Dictionary<string, ISelectableItem>();
                    ItemsUpdated?.Invoke(this, EventArgs.Empty);
                }
            }

            public string CurrentItem
            {
                get { return _currentItem; }
                set
                {
                    if (_currentItem == value) return;
                    _currentItem = value;
                    CurrentItemChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            /// <summary>
            /// Forces <see cref="ItemsUpdated"/> to fire so any subscribed auto-messenger
            /// re-sends the full source-selector state over WebSocket.
            /// </summary>
            public void FireUpdate()
            {
                ItemsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class MockSource : ISelectableItem
        {
            private readonly Action<uint> select;
            private bool _isSelected;

            public event EventHandler ItemUpdated;

            public string Key { get; }
            public string Name { get; }
            public uint Index { get; }

            public bool IsSelected
            {
                get { return _isSelected; }
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    ItemUpdated?.Invoke(this, EventArgs.Empty);
                }
            }

            public MockSource(string key, string name, uint index, Action<uint> select)
            {
                Key = key;
                Name = name;
                Index = index;
                this.select = select;
            }

            /// <summary>Selects this input on the block, which updates every item.</summary>
            public void Select()
            {
                select(Index);
            }

            internal void SetSelectedInternal(bool selected)
            {
                IsSelected = selected;
            }
        }
    }
}
