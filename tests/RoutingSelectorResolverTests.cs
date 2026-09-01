using FluentAssertions;
using PepperDash.Essentials.Core;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests
{
    /// <summary>
    /// Pure-logic tests for <see cref="RoutingSelectorResolver"/> — the translation from a named
    /// slot key (what mobile control's matrix routing sends back) to the input index the DSP
    /// commands take. The Tesira routing blocks key their ports by the input's label while the
    /// Selector holds the index, so this translation is what makes a route from the matrix page
    /// reach the right input. Runs off-processor: the routing port types are Crestron-free.
    /// </summary>
    public class RoutingSelectorResolverTests
    {
        private sealed class StubDevice : IRoutingInputs, IRoutingOutputs
        {
            public string Key { get; }
            public RoutingPortCollection<RoutingInputPort> InputPorts { get; } = new RoutingPortCollection<RoutingInputPort>();
            public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; } = new RoutingPortCollection<RoutingOutputPort>();
            public StubDevice(string key) => Key = key;
        }

        private static readonly StubDevice Device = new StubDevice("dsp");

        /// <summary>Mirrors how the routing blocks build ports: key = label, selector = index.</summary>
        private static RoutingInputPort Input(string label, uint index) =>
            new RoutingInputPort(label, eRoutingSignalType.Audio, eRoutingPortConnectionType.BackplaneOnly, index, Device);

        [Fact]
        public void Slot_key_resolves_to_the_inputs_index()
        {
            var ports = new[] { Input("Laptop", 1u), Input("Wireless Presenter", 3u) };

            RoutingSelectorResolver.TryResolveIndex("Wireless Presenter", ports, out var index).Should().BeTrue();
            index.Should().Be(3u);
        }

        [Fact]
        public void Existing_callers_selector_still_resolves()
        {
            var ports = new[] { Input("Laptop", 1u) };

            // Essentials routing passes the port's Selector directly; it must survive untouched.
            RoutingSelectorResolver.TryResolveIndex(2u, ports, out var index).Should().BeTrue();
            index.Should().Be(2u);
        }

        [Fact]
        public void Clear_route_sentinel_resolves_to_zero()
        {
            var ports = new[] { Input("Laptop", 1u) };

            RoutingSelectorResolver.TryResolveIndex((uint)0, ports, out var index).Should().BeTrue();
            index.Should().Be(0u);
        }

        [Fact]
        public void Unknown_label_is_rejected_rather_than_sent_to_the_dsp()
        {
            var ports = new[] { Input("Laptop", 1u) };

            // The bug this guards: an unresolved label used to be sent verbatim as the index.
            RoutingSelectorResolver.TryResolveIndex("Not A Real Input", ports, out var index).Should().BeFalse();
            index.Should().Be(0u);
        }

        [Fact]
        public void Null_selector_is_rejected()
        {
            var ports = new[] { Input("Laptop", 1u) };

            RoutingSelectorResolver.TryResolveIndex(null, ports, out _).Should().BeFalse();
        }

        [Fact]
        public void Untyped_resolve_passes_a_non_string_through()
        {
            var ports = new[] { Input("Laptop", 1u) };

            RoutingSelectorResolver.ResolveSelector(5u, ports).Should().Be(5u);
        }
    }
}
