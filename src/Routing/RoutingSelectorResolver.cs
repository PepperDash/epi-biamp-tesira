using System;
using System.Collections.Generic;
using System.Linq;
using PepperDash.Essentials.Core;
// The plugin's config namespace also defines a RoutingPort, and it wins name resolution from
// this child namespace - alias Core's so these signatures are unambiguous.
using CorePort = PepperDash.Essentials.Core.RoutingPort;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing
{
    /// <summary>
    /// Translates a routing selector that arrived as a named slot key back into the value this
    /// device's <c>ExecuteSwitch</c> expects.
    ///
    /// Slots published through <see cref="RoutingPortNamedSlots"/> are keyed by the routing port's
    /// own key, so mobile control's matrix routing sends that key back as the selector - a string -
    /// rather than the port's <see cref="CorePort.Selector"/>. The Tesira routing blocks build
    /// their ports with the input's label as the key and its index as the Selector, so without this
    /// translation a route from the matrix page arrives as a label where an index is expected.
    /// </summary>
    public static class RoutingSelectorResolver
    {
        /// <summary>
        /// Maps a selector that arrived as a port key to that port's Selector, and passes anything
        /// else through unchanged. An unmatched key is returned as-is so the caller can report it.
        /// </summary>
        public static object ResolveSelector(object selector, IEnumerable<CorePort> ports)
        {
            if (!(selector is string key) || ports == null)
                return selector;

            var port = ports.FirstOrDefault(p => p != null && p.Key == key);

            return port != null ? port.Selector : selector;
        }

        /// <summary>
        /// Resolves a selector (port key or Selector value) to the numeric index the DSP commands
        /// take. False when the selector matches no port and is not itself numeric - the caller
        /// logs and drops rather than sending a malformed command to the DSP.
        /// </summary>
        public static bool TryResolveIndex(object selector, IEnumerable<CorePort> ports, out uint index)
        {
            index = 0;

            var resolved = ResolveSelector(selector, ports);

            if (resolved == null)
                return false;

            try
            {
                index = Convert.ToUInt32(resolved);
                return true;
            }
            catch (FormatException) { return false; }
            catch (InvalidCastException) { return false; }
            catch (OverflowException) { return false; }
        }
    }
}
