using System;
using System.Collections.Generic;
using System.Linq;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// In-memory source selector — implements <see cref="IHasInputs{T}"/> keyed by string.
    /// Exposes a collection of <see cref="ISelectableItem"/> that consumers (e.g. React
    /// <c>useIHasSelectableItems</c>) can list and select. Selecting an item flips
    /// <see cref="ISelectableItem.IsSelected"/> on the new + previous items and fires
    /// <see cref="ISelectableItems{TKey,TValue}.CurrentItemChanged"/>.
    /// </summary>
    public class TesiraDspMockSourceSelector : EssentialsDevice, IHasInputs<string>
    {
        public ISelectableItems<string> Inputs { get; }

        public TesiraDspMockSourceSelector(
            string key,
            string name,
            IReadOnlyDictionary<string, string> sourceLabelsByKey,
            string initialSourceKey)
            : base(key, name)
        {
            var inputs = new MockSourceInputs();
            var items = new Dictionary<string, ISelectableItem>();
            foreach (var kvp in sourceLabelsByKey)
            {
                items[kvp.Key] = new MockSource(inputs, kvp.Key, kvp.Value);
            }
            inputs.Items = items;

            if (!string.IsNullOrEmpty(initialSourceKey) && items.ContainsKey(initialSourceKey))
            {
                inputs.CurrentItem = initialSourceKey;
                if (items[initialSourceKey] is MockSource seeded)
                {
                    seeded.SetSelectedInternal(true);
                }
            }

            Inputs = inputs;
        }

        public override bool CustomActivate()
        {
            this.LogVerbose(
                "Activated with {count} source(s): {keys}",
                Inputs.Items.Count,
                string.Join(", ", Inputs.Items.Keys));
            return base.CustomActivate();
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
            private readonly MockSourceInputs _parent;
            private bool _isSelected;

            public event EventHandler ItemUpdated;

            public string Key { get; }
            public string Name { get; }

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

            public MockSource(MockSourceInputs parent, string key, string name)
            {
                _parent = parent;
                Key = key;
                Name = name;
            }

            public void Select()
            {
                if (_parent.CurrentItem == Key) return;

                // Deselect anything else that was selected — the frontend expects
                // exactly-one-selected semantics from a source selector.
                foreach (var pair in _parent.Items)
                {
                    if (pair.Value is MockSource ms && ms.Key != Key && ms.IsSelected)
                    {
                        ms.SetSelectedInternal(false);
                    }
                }

                SetSelectedInternal(true);
                _parent.CurrentItem = Key;
            }

            internal void SetSelectedInternal(bool selected)
            {
                IsSelected = selected;
            }
        }

        /// <summary>
        /// Re-raises <see cref="ISelectableItems{TKey,TValue}.ItemsUpdated"/> so the Essentials
        /// auto-messenger re-sends the full source-selector state over WebSocket.
        ///
        /// Called by <see cref="TesiraDspMock"/>'s periodic refresh timer to recover from the
        /// DirectServer race condition where <c>PostInitialState()</c> fires before the WebSocket
        /// transport is assigned on RMC4 (TLS cert generation can take ~5 s on first boot).
        /// </summary>
        public void FireUpdate()
        {
            if (Inputs is MockSourceInputs inner)
                inner.FireUpdate();
        }
    }
}
