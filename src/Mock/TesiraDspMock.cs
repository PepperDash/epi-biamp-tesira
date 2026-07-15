using System.Collections.Generic;
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
    public class TesiraDspMock : EssentialsDevice, IHasDspPresetSave
    {
        private const string ChildKeyFormat = "{0}--{1}";

        // IHasDspPresetSave -> IDspPresets requires a Presets dictionary; populated at ctor.
        public Dictionary<string, IKeyName> Presets { get; }
            = new Dictionary<string, IKeyName>();

        public TesiraDspMock(DeviceConfig dc) : base(dc.Key, dc.Name)
        {
            var props = dc.Properties?.ToObject<TesiraDspMockPropertiesConfig>()
                        ?? new TesiraDspMockPropertiesConfig();

            RegisterFaders(props.Faders);
            RegisterSourceSelector(props.SourceSelector);
            PopulatePresets(props.Presets);

            this.LogInformation(
                "TesiraDspMock '{key}' built: {faderCount} fader(s), {presetCount} preset(s), sourceSelector={hasSourceSelector}",
                Key,
                props.Faders?.Count ?? 0,
                props.Presets?.Count ?? 0,
                props.SourceSelector != null);
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
            }
        }

        private void RegisterSourceSelector(TesiraDspMockSourceSelectorConfig cfg)
        {
            if (cfg == null || cfg.Sources == null || cfg.Sources.Count == 0) return;

            var childKey = string.Format(ChildKeyFormat, Key, cfg.Key ?? "source-selector");
            var name = string.IsNullOrEmpty(cfg.Label) ? "Source Selector" : cfg.Label;

            var labels = new Dictionary<string, string>();
            foreach (var s in cfg.Sources)
            {
                labels[s.Key] = string.IsNullOrEmpty(s.Value?.Label) ? s.Key : s.Value.Label;
            }

            var selector = new TesiraDspMockSourceSelector(childKey, name, labels, cfg.InitialSource);
            DeviceManager.AddDevice(selector);
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
    }
}
