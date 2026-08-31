using System;
using System.Collections.Generic;
using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Routing
{
    /// <summary>
    /// Reusable <see cref="IHasNamedRoutingSlots"/> slot model built from a device's existing
    /// <see cref="RoutingInputPort"/>/<see cref="RoutingOutputPort"/> collections and its route
    /// feedback. Slots and per-signal-type route state are derived from the port collections plus
    /// route-change notifications the device already raises, so any port-based
    /// <see cref="IRoutingMidpointWithFeedback"/> can expose named slots without bespoke wiring.
    /// Pure Essentials.Core routing types, so it is unit-testable without a processor.
    /// (Mirrors PepperDash.Essentials.DM.Routing.RoutingPortNamedSlots; a future consolidation into
    /// Essentials.Core would remove the per-plugin copy.)
    /// </summary>
    public sealed class RoutingPortNamedSlots
    {
        private readonly IEnumerable<RoutingInputPort> _inputPorts;
        private readonly IEnumerable<RoutingOutputPort> _outputPorts;
        private readonly object _buildLock = new object();

        private Dictionary<string, IRoutingSlotInfo> _inputSlots;
        private Dictionary<string, IRoutingOutputSlotInfo> _outputSlots;
        private Dictionary<string, RoutingPortOutputSlot> _outputByPortKey;

        /// <summary>
        /// Captures the port collections. Slots are built lazily on first access (1-based slot
        /// numbers in enumeration order) so the device can construct this before its ports are fully
        /// populated - the build snapshots the final port state at first runtime use.
        /// </summary>
        public RoutingPortNamedSlots(
            IEnumerable<RoutingInputPort> inputPorts,
            IEnumerable<RoutingOutputPort> outputPorts)
        {
            _inputPorts = inputPorts;
            _outputPorts = outputPorts;
        }

        private void EnsureBuilt()
        {
            if (_inputSlots != null) return;

            lock (_buildLock)
            {
                if (_inputSlots != null) return;

                var inputSlots = new Dictionary<string, IRoutingSlotInfo>();
                var outputSlots = new Dictionary<string, IRoutingOutputSlotInfo>();
                var outputByPortKey = new Dictionary<string, RoutingPortOutputSlot>();

                if (_inputPorts != null)
                {
                    var i = 0;
                    foreach (var port in _inputPorts)
                    {
                        if (port == null) continue;
                        var slot = new RoutingPortInputSlot(port, ++i);
                        inputSlots[slot.Key] = slot;
                    }
                }

                if (_outputPorts != null)
                {
                    var o = 0;
                    foreach (var port in _outputPorts)
                    {
                        if (port == null) continue;
                        var slot = new RoutingPortOutputSlot(port, ++o);
                        outputSlots[slot.Key] = slot;
                        outputByPortKey[port.Key] = slot;
                    }
                }

                _outputByPortKey = outputByPortKey;
                _outputSlots = outputSlots;
                _inputSlots = inputSlots; // assigned last: publishes the built state to the fast-path check
            }
        }

        /// <summary>Named input slots, keyed by slot key.</summary>
        public IReadOnlyDictionary<string, IRoutingSlotInfo> InputSlots
        {
            get { EnsureBuilt(); return _inputSlots; }
        }

        /// <summary>Named output slots, keyed by slot key.</summary>
        public IReadOnlyDictionary<string, IRoutingOutputSlotInfo> OutputSlots
        {
            get { EnsureBuilt(); return _outputSlots; }
        }

        /// <summary>
        /// Applies a route-change notification to the matching output slot. A null
        /// <paramref name="inputPort"/> clears the route for that output/signal type. No-ops when the
        /// output is unknown.
        /// </summary>
        public void HandleRouteChange(
            RoutingOutputPort outputPort,
            RoutingInputPort inputPort,
            eRoutingSignalType signalType)
        {
            if (outputPort == null) return;
            EnsureBuilt();
            if (_outputByPortKey.TryGetValue(outputPort.Key, out var slot))
                slot.SetRoute(signalType, inputPort?.Key);
        }
    }

    /// <summary>
    /// <see cref="IRoutingSlotInfo"/> backed by a <see cref="RoutingInputPort"/>.
    /// </summary>
    public sealed class RoutingPortInputSlot : IRoutingSlotInfo
    {
        private readonly RoutingInputPort _port;

        public RoutingPortInputSlot(RoutingInputPort port, int slotNumber)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            SlotNumber = slotNumber;
        }

        public int SlotNumber { get; }
        public eRoutingSignalType SupportedSignalTypes => _port.Type;
        public string Key => _port.Key;
        public string Name => _port.Key;
    }

    /// <summary>
    /// <see cref="IRoutingOutputSlotInfo"/> backed by a <see cref="RoutingOutputPort"/>, tracking the
    /// currently routed input key per signal type.
    /// </summary>
    public sealed class RoutingPortOutputSlot : IRoutingOutputSlotInfo
    {
        private readonly RoutingOutputPort _port;
        private readonly Dictionary<eRoutingSignalType, string> _currentRouteInputKeys =
            new Dictionary<eRoutingSignalType, string>();

        public RoutingPortOutputSlot(RoutingOutputPort port, int slotNumber)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            SlotNumber = slotNumber;
        }

        public int SlotNumber { get; }
        public eRoutingSignalType SupportedSignalTypes => _port.Type;
        public string Key => _port.Key;
        public string Name => _port.Key;

        public IReadOnlyDictionary<eRoutingSignalType, string> CurrentRouteInputKeys => _currentRouteInputKeys;

        public event EventHandler OutputSlotChanged;

        /// <summary>
        /// Records the input routed to this output for a signal type (null/empty key clears it). A
        /// combined <see cref="eRoutingSignalType.AudioVideo"/> is expanded to its Audio and Video
        /// components so per-signal-type consumers resolve each independently. Raises
        /// <see cref="OutputSlotChanged"/> only when something actually changed.
        /// </summary>
        internal void SetRoute(eRoutingSignalType signalType, string inputKey)
        {
            var types = signalType == eRoutingSignalType.AudioVideo
                ? new[] { eRoutingSignalType.Audio, eRoutingSignalType.Video }
                : new[] { signalType };

            var changed = false;
            foreach (var type in types)
            {
                if (string.IsNullOrEmpty(inputKey))
                {
                    if (_currentRouteInputKeys.Remove(type)) changed = true;
                }
                else if (!_currentRouteInputKeys.TryGetValue(type, out var existing) || existing != inputKey)
                {
                    _currentRouteInputKeys[type] = inputKey;
                    changed = true;
                }
            }

            if (changed)
                OutputSlotChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
