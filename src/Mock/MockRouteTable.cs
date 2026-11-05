#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// The mock router's route state: which input each configured output is routed to. Input 0 is
    /// "None" (cleared). Every output starts cleared. Thread-safe.
    /// </summary>
    public class MockRouteTable
    {
        private readonly object routesLock = new object();
        private readonly Dictionary<uint, uint> routes = new Dictionary<uint, uint>();
        private readonly HashSet<uint> inputs;

        public MockRouteTable(IEnumerable<uint> outputs, IEnumerable<uint> inputs)
        {
            foreach (var output in outputs) routes[output] = 0;
            this.inputs = new HashSet<uint>(inputs) { 0 };
        }

        /// <summary>Configured outputs, lowest first.</summary>
        public IReadOnlyList<uint> Outputs
        {
            get { lock (routesLock) return routes.Keys.OrderBy(o => o).ToList(); }
        }

        public bool HasOutput(uint output)
        {
            lock (routesLock) return routes.ContainsKey(output);
        }

        public bool HasInput(uint input) => inputs.Contains(input);

        /// <summary>The input routed to an output. 0 = cleared, or an unconfigured output.</summary>
        public uint Get(uint output)
        {
            lock (routesLock) return routes.TryGetValue(output, out var input) ? input : 0;
        }

        /// <summary>A copy of every route, output to input.</summary>
        public IReadOnlyDictionary<uint, uint> Snapshot()
        {
            lock (routesLock) return new Dictionary<uint, uint>(routes);
        }

        /// <summary>
        /// Routes an input to an output. Returns <see cref="RouteResult.Changed"/> or
        /// <see cref="RouteResult.Unchanged"/>, or why it was refused.
        /// </summary>
        public RouteResult Route(uint output, uint input)
        {
            if (!inputs.Contains(input)) return RouteResult.UnknownInput;
            lock (routesLock)
            {
                if (!routes.TryGetValue(output, out var current)) return RouteResult.UnknownOutput;
                if (current == input) return RouteResult.Unchanged;
                routes[output] = input;
                return RouteResult.Changed;
            }
        }
    }

    public enum RouteResult
    {
        Changed,
        Unchanged,
        UnknownOutput,
        UnknownInput,
    }
}
