using System.Collections.Generic;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// Factory for <see cref="TesiraDspMock"/>. Registered under the type name
    /// <c>tesiradspmock</c> so consumers can swap the real Tesira EPI for the mock by
    /// changing only the config <c>type</c> field.
    /// </summary>
    public class TesiraDspMockFactory : EssentialsPluginDeviceFactory<TesiraDspMock>
    {
        public TesiraDspMockFactory()
        {
            MinimumEssentialsFrameworkVersion = "2.38.0";
            TypeNames = new List<string> { "tesiradspmock" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.LogDebug("Factory building TesiraDspMock '{key}'", dc.Key);
            return new TesiraDspMock(dc);
        }
    }

    /// <summary>
    /// Standalone in-memory stand-in for <see cref="TesiraDsp"/>. Implements the
    /// contracts a Mobile-Control room-plugin cares about — level/mute per fader,
    /// selectable inputs on the source selector, and DSP presets (recall + save) —
    /// with zero hardware, zero comm, zero subscription machinery.
    /// </summary>
    /// <remarks>
    /// Wire up like a real Tesira EPI, but with <c>type: "tesiradspmock"</c>. Child
    /// device keys follow the same <c>{parentKey}--{childKey}</c> convention as the
    /// real EPI, so room-plugin configs that reference child fader / source-selector
    /// keys are drop-in — only the parent DSP device swaps.
    ///
    /// Intended for bench-testing frontend/backend round-trips when no physical DSP
    /// is available. Not for production use.
    /// </remarks>
    public class TesiraDspMock : EssentialsDevice, IHasDspPresetSave, ICommunicationMonitor
    {
        private const string ChildKeyFormat = "{0}--{1}";

        // IHasDspPresetSave -> IDspPresets requires a Presets dictionary; populated at ctor.
        public Dictionary<string, IKeyName> Presets { get; }
            = new Dictionary<string, IKeyName>();

        // ICommunicationMonitor — always-online since the mock has no real hardware.
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        // Child faders tracked so CustomActivate() can schedule periodic feedback re-fires.
        private readonly List<TesiraDspMockFader> _faders = new List<TesiraDspMockFader>();
        // Child source selectors tracked alongside faders for periodic state re-fire.
        private readonly List<TesiraDspMockSourceSelector> _selectors = new List<TesiraDspMockSourceSelector>();
        // Held to prevent GC of the repeating timer.
        private CTimer _feedbackRefreshTimer;

        public TesiraDspMock(DeviceConfig dc) : base(dc.Key, dc.Name)
        {
            var props = dc.Properties?.ToObject<TesiraDspMockPropertiesConfig>()
                        ?? new TesiraDspMockPropertiesConfig();

            RegisterFaders(props.FaderControlBlocks);
            RegisterSwitcherControlBlocks(props.SwitcherControlBlocks);
            PopulatePresets(props.Presets);

            CommunicationMonitor = new AlwaysOnMonitor(this);

            this.LogInformation(
                "TesiraDspMock '{key}' built: {faderCount} fader(s), {presetCount} preset(s), switcherCount={switcherCount}",
                Key,
                props.FaderControlBlocks?.Count ?? 0,
                props.Presets?.Count ?? 0,
                props.SwitcherControlBlocks?.Count ?? 0);
        }

        // ── IDspPresets / IHasDspPresetSave ──────────────────────────────────

        public void RecallPreset(string key)
        {
            if (!Presets.ContainsKey(key))
            {
                this.LogWarning("RecallPreset — unknown preset key '{presetKey}'", key);
                return;
            }
            this.LogInformation("RecallPreset (mock) — key '{presetKey}'", key);
            // No-op: mock has no hardware to route.
        }

        public void SavePreset(string key)
        {
            if (!Presets.ContainsKey(key))
            {
                this.LogWarning("SavePreset — unknown preset key '{presetKey}'", key);
                return;
            }
            this.LogInformation("SavePreset (mock) — key '{presetKey}'", key);
            // No-op: mock has no hardware state to persist.
        }

        // ── Setup helpers ────────────────────────────────────────────────────

        public override bool CustomActivate()
        {
            // Re-fire all fader feedbacks every 2 seconds.
            // Recovers from Essentials DirectServer race condition where PostInitialState()
            // fires before the WebSocket transport is assigned on the new-flow client connect.
            // On RMC4 bench setups with empty serverCertificateFile, TLS cert generation can
            // take ~5s, keeping the WebSocket null for that entire window. A 2s period ensures
            // state reaches the frontend within 2s of the cert completing and WS being assigned.
            CommunicationMonitor.Start();

            _feedbackRefreshTimer = new CTimer(_ =>
            {
                foreach (var fader in _faders)
                {
                    fader.VolumeLevelFeedback.FireUpdate();
                    fader.MuteFeedback.FireUpdate();
                }
                foreach (var selector in _selectors)
                {
                    selector.FireUpdate();
                }
            }, null, 2000, 2000);

            return base.CustomActivate();
        }

        private void RegisterFaders(Dictionary<string, TesiraDspMockFaderConfig> faderConfigs)
        {
            if (faderConfigs == null) return;
            foreach (var kvp in faderConfigs)
            {
                var childKey = string.Format(ChildKeyFormat, Key, kvp.Key);
                var name = string.IsNullOrEmpty(kvp.Value?.Label) ? kvp.Key : kvp.Value.Label;
                var fader = new TesiraDspMockFader(
                    childKey,
                    name,
                    kvp.Value?.InitialLevelPercent ?? 50,
                    kvp.Value?.InitialMute ?? false);

                DeviceManager.AddDevice(fader);
                _faders.Add(fader);
            }
        }

        private void RegisterSwitcherControlBlocks(Dictionary<string, TesiraDspMockSourceSelectorConfig> blocks)
        {
            if (blocks == null) return;
            foreach (var kvp in blocks)
            {
                var childKey = string.Format(ChildKeyFormat, Key, kvp.Key);
                var cfg = kvp.Value;
                var name = string.IsNullOrEmpty(cfg?.Label) ? kvp.Key : cfg.Label;

                var labels = new Dictionary<string, string>();
                var sources = cfg?.EffectiveSources;
                if (sources != null)
                {
                    foreach (var s in sources)
                        labels[s.Key] = string.IsNullOrEmpty(s.Value?.Label) ? s.Key : s.Value.Label;
                }

                var selector = new TesiraDspMockSourceSelector(childKey, name, labels, cfg?.InitialSource);
                DeviceManager.AddDevice(selector);
                _selectors.Add(selector);
            }
        }

        private void PopulatePresets(Dictionary<string, TesiraDspMockPresetConfig> presetConfigs)
        {
            if (presetConfigs == null) return;
            foreach (var kvp in presetConfigs)
            {
                var name = string.IsNullOrEmpty(kvp.Value?.Label) ? kvp.Key : kvp.Value.Label;
                Presets[kvp.Key] = new MockPreset(kvp.Key, name);
            }
        }

        // ── Preset entry (IKeyName is all that IDspPresets.Presets requires) ─

        private sealed class MockPreset : IKeyName
        {
            public string Key { get; }
            public string Name { get; }
            public MockPreset(string key, string name) { Key = key; Name = name; }
        }

        // ── Communication monitor — always online (no real hardware) ─────────

        /// <summary>
        /// Minimal <see cref="StatusMonitorBase"/> implementation that permanently
        /// reports <see cref="MonitorStatus.IsOk"/>. The mock has no comm port to
        /// poll, so there is nothing to monitor — it is always "online".
        /// </summary>
        private sealed class AlwaysOnMonitor : StatusMonitorBase
        {
            public AlwaysOnMonitor(IKeyed parent)
                // StatusMonitorBase requires warningTime < errorTime and both >= 5000 ms.
                // Values are never used because Start/Stop are no-ops.
                : base(parent, 5000, 10000)
            {
                // Drive status to IsOk immediately so IsOnlineFeedback is true.
                Status = MonitorStatus.IsOk;
            }

            public override void Start() { /* no-op — no poll needed */ }
            public override void Stop()  { /* no-op — no poll needed */ }
        }
    }
}
