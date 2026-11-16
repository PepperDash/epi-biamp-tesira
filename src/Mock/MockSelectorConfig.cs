#nullable disable
using System.Collections.Generic;
using System.Linq;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// Turns a mock switcher config (<see cref="TesiraDspMockSourceSelectorConfig"/>) into the shared
    /// <see cref="SelectorPortLayout"/>. The mock's keys are strings: the real format's numbers
    /// (<c>switcherInputs</c> / <c>switcherOutputs</c>) or, in the mock's own <c>sources</c> format, any
    /// name (e.g. "source1").
    /// </summary>
    public static class MockSelectorConfig
    {
        /// <summary>The switcher layout, with any numbering problems listed in its <see cref="SelectorPortLayout.Problems"/>.</summary>
        public static SelectorPortLayout ForSwitcher(TesiraDspMockSourceSelectorConfig config)
        {
            var problems = new List<string>();
            var mockFormat = config?.Sources?.Count > 0;

            // The mock's own format may leave out labels (the key is used); the real format can't.
            var inputs = Number(config?.EffectiveSources, mockFormat, problems);
            var outputs = Number(config?.SwitcherOutputs, false, problems);

            return SelectorPortLayout.ForSwitcher(inputs, outputs, config?.InitialSource, problems);
        }

        /// <summary>
        /// Numbers string-keyed entries. A numeric key keeps its number (so "2" is input 2, as in the real
        /// config); any other key gets the lowest number no numeric key uses, in config order. Leading
        /// zeros parse ("01" is 1), so "01" next to "1" is a duplicate the layout skips.
        /// </summary>
        public static List<SelectorPort> Number(Dictionary<string, TesiraDspMockSourceConfig> entries, bool labelFallsBackToKey,
            List<string> problems)
        {
            var ports = new List<SelectorPort>();
            if (entries == null) return ports;

            var used = new HashSet<uint>(entries.Keys
                .Select(k => uint.TryParse(k, out var n) ? (uint?)n : null)
                .Where(n => n.HasValue)
                .Select(n => n.Value));

            var next = 1u;
            foreach (var entry in entries)
            {
                uint index;
                if (!uint.TryParse(entry.Key, out index))
                {
                    while (used.Contains(next)) next++;
                    index = next;
                    used.Add(index);
                }

                var label = entry.Value?.Label;
                if (string.IsNullOrEmpty(label) && labelFallsBackToKey) label = entry.Key;

                ports.Add(new SelectorPort(index, label, entry.Key));
            }

            return ports;
        }
    }
}
