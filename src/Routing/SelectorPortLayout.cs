#nullable disable
using System.Collections.Generic;
using System.Linq;
using SignalType = PepperDash.Essentials.Core.eRoutingSignalType;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing
{
    /// <summary>Which kind of single-selection block a <see cref="SelectorPortLayout"/> describes.</summary>
    public enum SelectorBlockKind
    {
        /// <summary><c>sourceSelectorControlBlocks</c>: <see cref="TesiraDspSourceSelector"/>.</summary>
        SourceSelector,

        /// <summary><c>switcherControlBlocks</c>: <see cref="TesiraDspSwitcher"/>.</summary>
        Switcher,
    }

    /// <summary>
    /// One input or output port of a selector block: its number on the block (the port's selector),
    /// its label (the port's key), and, for the mock's input list, the config key it came from.
    /// </summary>
    public sealed class SelectorPort
    {
        public SelectorPort(uint index, string label, string itemKey = null)
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
    /// The ports a source selector or switcher block builds from its config. Shared by the real blocks
    /// (<see cref="TesiraDspSourceSelector"/>, <see cref="TesiraDspSwitcher"/>) and the mock, so they
    /// build the same ports. A port the config can't support (no label, the reserved number 0, or a
    /// number or label already used) is skipped and described in <see cref="Problems"/> for the block
    /// to log.
    /// </summary>
    public sealed class SelectorPortLayout
    {
        public const string NoneLabel = "None";

        /// <summary>The source selector's only output port.</summary>
        public const string SourceSelectorOutputLabel = "output";

        private SelectorPortLayout(SelectorBlockKind kind, List<SelectorPort> inputs, List<SelectorPort> outputs,
            List<string> problems, uint initialInput)
        {
            Kind = kind;
            Inputs = inputs;
            Outputs = outputs;
            Problems = problems;
            InitialInput = initialInput;
        }

        public SelectorBlockKind Kind { get; }

        /// <summary>Input ports, in config order. A source selector's end with "None" (0).</summary>
        public IReadOnlyList<SelectorPort> Inputs { get; }

        /// <summary>Output ports. Route feedback uses the first, as the real blocks do.</summary>
        public IReadOnlyList<SelectorPort> Outputs { get; }

        /// <summary>Config entries that were skipped, and why.</summary>
        public IReadOnlyList<string> Problems { get; }

        /// <summary>The input selected at startup (mock only). 0 = none.</summary>
        public uint InitialInput { get; }

        /// <summary>
        /// The routed-source name for an input number: its label, "None" for 0, or "Input N" for an
        /// input the config doesn't list (the DSP can still select it).
        /// </summary>
        public string LabelFor(uint input)
        {
            var port = Inputs.FirstOrDefault(i => i.Index == input);
            if (port != null) return port.Label;
            return input == 0 ? NoneLabel : "Input " + input;
        }

        /// <summary>
        /// Whether a switch with this signal type acts on the block: a source selector acts on any type
        /// that includes audio, a switcher only on exactly audio (as the real blocks do).
        /// </summary>
        public bool AcceptsSignalType(SignalType signalType) =>
            Kind == SelectorBlockKind.Switcher
                ? signalType == SignalType.Audio
                : signalType.HasFlag(SignalType.Audio);

        /// <summary>
        /// A source selector: the configured <c>sourceSelectorInputs</c>, then "None" at 0, and one
        /// output, "output", at 1.
        /// </summary>
        public static SelectorPortLayout ForSourceSelector(IDictionary<uint, RoutingPort> inputs)
        {
            var problems = new List<string>();
            var inputPorts = Validate(FromConfig(inputs), "input", true, problems);
            inputPorts.Add(new SelectorPort(0, NoneLabel));

            var outputPorts = new List<SelectorPort> { new SelectorPort(1, SourceSelectorOutputLabel) };
            return new SelectorPortLayout(SelectorBlockKind.SourceSelector, inputPorts, outputPorts, problems, 0);
        }

        /// <summary>
        /// A switcher from the real config format: one input per <c>switcherInputs</c> entry and one output
        /// per <c>switcherOutputs</c> entry, with no "None" port (0 still means none selected).
        /// </summary>
        public static SelectorPortLayout ForSwitcher(IDictionary<uint, RoutingPort> inputs, IDictionary<uint, RoutingPort> outputs) =>
            ForSwitcher(FromConfig(inputs), FromConfig(outputs), null, new List<string>());

        /// <summary>
        /// A switcher from already-numbered entries (the mock's config formats). <paramref name="initialItemKey"/>
        /// names the starting input by its <see cref="SelectorPort.ItemKey"/>. <paramref name="problems"/> may
        /// already hold problems found while numbering.
        /// </summary>
        public static SelectorPortLayout ForSwitcher(IEnumerable<SelectorPort> inputs, IEnumerable<SelectorPort> outputs,
            string initialItemKey, List<string> problems)
        {
            problems = problems ?? new List<string>();
            var inputPorts = Validate(inputs, "input", true, problems);
            var outputPorts = Validate(outputs, "output", false, problems);

            var initial = 0u;
            if (!string.IsNullOrEmpty(initialItemKey))
            {
                var port = inputPorts.FirstOrDefault(i => i.ItemKey == initialItemKey);
                if (port != null)
                    initial = port.Index;
                else
                    problems.Add($"initialSource '{initialItemKey}' is not one of the inputs; starting with none selected");
            }

            return new SelectorPortLayout(SelectorBlockKind.Switcher, inputPorts, outputPorts, problems, initial);
        }

        private static IEnumerable<SelectorPort> FromConfig(IDictionary<uint, RoutingPort> ports) =>
            ports?.Select(p => new SelectorPort(p.Key, p.Value?.Label)) ?? Enumerable.Empty<SelectorPort>();

        private static List<SelectorPort> Validate(IEnumerable<SelectorPort> ports, string what, bool zeroIsNone, List<string> problems)
        {
            var kept = new List<SelectorPort>();
            var numbers = new HashSet<uint>();
            var labels = new HashSet<string>();

            foreach (var port in ports ?? Enumerable.Empty<SelectorPort>())
            {
                if (zeroIsNone && port.Index == 0)
                {
                    problems.Add($"{what} 0 ('{port.Label}') skipped: 0 is reserved for None");
                    continue;
                }

                if (string.IsNullOrEmpty(port.Label))
                {
                    problems.Add($"{what} {port.Index} skipped: it has no label (the label is the port's key)");
                    continue;
                }

                if (!numbers.Add(port.Index))
                {
                    problems.Add($"{what} {port.Index} ('{port.Label}', config key '{port.ItemKey}') skipped: number {port.Index} is already used");
                    continue;
                }

                if (!labels.Add(port.Label))
                {
                    numbers.Remove(port.Index);
                    problems.Add($"{what} {port.Index} skipped: label '{port.Label}' is already used (labels are port keys)");
                    continue;
                }

                kept.Add(port);
            }

            return kept;
        }
    }
}
