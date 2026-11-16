#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// One input or output of a mock selector: its number on the block, its port label, and, for
    /// inputs, the config key the selectable-item list uses.
    /// </summary>
    public sealed class MockSelectorPort
    {
        public MockSelectorPort(uint index, string label, string itemKey = null)
        {
            Index = index;
            Label = label;
            ItemKey = itemKey ?? index.ToString();
        }

        public uint Index { get; }
        public string Label { get; }
        public string ItemKey { get; }
    }

    /// <summary>
    /// The ports a <see cref="TesiraDspMockSourceSelector"/> builds, worked out the way the real
    /// <see cref="TesiraDspSourceSelector"/> and <see cref="TesiraDspSwitcher"/> build theirs, so the
    /// mock routes like them. Each port's key is its label and its selector is its number.
    /// </summary>
    public sealed class MockSelectorLayout
    {
        public const string NoneLabel = "None";

        /// <summary>The real source selector's only output port.</summary>
        public const string SourceSelectorOutputLabel = "output";

        private MockSelectorLayout(List<MockSelectorPort> inputs, List<MockSelectorPort> outputs, uint initialInput)
        {
            Inputs = inputs;
            Outputs = outputs;
            InitialInput = initialInput;
        }

        /// <summary>Input ports, in the order the real block adds them. Includes "None" (0) when the real block has it.</summary>
        public IReadOnlyList<MockSelectorPort> Inputs { get; }

        /// <summary>Output ports. Route feedback uses the first, as on the real blocks.</summary>
        public IReadOnlyList<MockSelectorPort> Outputs { get; }

        /// <summary>The input selected at startup. 0 = none.</summary>
        public uint InitialInput { get; }

        /// <summary>The label for an input number, as the real blocks report the routed source name.</summary>
        public string LabelFor(uint input)
        {
            var port = Inputs.FirstOrDefault(i => i.Index == input);
            if (port != null) return port.Label;
            return input == 0 ? NoneLabel : "Input " + input;
        }

        /// <summary>
        /// As <see cref="TesiraDspSourceSelector"/>: the configured <c>sourceSelectorInputs</c>, then
        /// "None" at 0, and one output, "output", at 1. Starts on None, as the real block does until
        /// the DSP reports its selection. A configured input 0 is ignored: 0 is always None.
        /// </summary>
        public static MockSelectorLayout ForSourceSelector(TesiraSourceSelectorControlBlockConfig config)
        {
            var inputs = new List<MockSelectorPort>();
            foreach (var input in config?.SourceSelectorInputs ?? new Dictionary<uint, RoutingPort>())
            {
                if (input.Key == 0) continue;
                inputs.Add(new MockSelectorPort(input.Key, LabelOr(input.Value?.Label, "Input " + input.Key)));
            }
            inputs.Add(new MockSelectorPort(0, NoneLabel));

            var outputs = new List<MockSelectorPort> { new MockSelectorPort(1, SourceSelectorOutputLabel) };
            return new MockSelectorLayout(inputs, outputs, 0);
        }

        /// <summary>
        /// As <see cref="TesiraDspSwitcher"/>: one input per <c>sources</c> (or real-format
        /// <c>switcherInputs</c>) entry and one output per <c>switcherOutputs</c> entry, with no
        /// "None" port, so a cleared switcher reports no route. Numeric keys keep their number; other
        /// keys (the mock's <c>sources</c> format, e.g. "source1") are numbered 1, 2, ... in config
        /// order. <c>initialSource</c> names the starting input by its key.
        /// </summary>
        public static MockSelectorLayout ForSwitcher(TesiraDspMockSourceSelectorConfig config)
        {
            var inputs = Number(config?.EffectiveSources);
            var outputs = Number(config?.SwitcherOutputs);

            var initial = 0u;
            if (!string.IsNullOrEmpty(config?.InitialSource))
                initial = inputs.FirstOrDefault(i => i.ItemKey == config.InitialSource)?.Index ?? 0;

            return new MockSelectorLayout(inputs, outputs, initial);
        }

        private static List<MockSelectorPort> Number(Dictionary<string, TesiraDspMockSourceConfig> entries)
        {
            var ports = new List<MockSelectorPort>();
            if (entries == null) return ports;

            var numeric = entries.Keys.All(k => uint.TryParse(k, out var n) && n > 0);
            var next = 1u;
            foreach (var entry in entries)
            {
                var index = numeric ? uint.Parse(entry.Key) : next++;
                ports.Add(new MockSelectorPort(index, LabelOr(entry.Value?.Label, entry.Key), entry.Key));
            }
            return ports;
        }

        private static string LabelOr(string label, string fallback) =>
            string.IsNullOrEmpty(label) ? fallback : label;
    }
}
