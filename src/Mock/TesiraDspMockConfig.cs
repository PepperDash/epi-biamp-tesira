using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// Config properties for the <see cref="TesiraDspMock"/> device.
    /// Deserialized from the <c>properties</c> object of a config entry with
    /// <c>type = "tesiradspmock"</c>.
    /// </summary>
    /// <remarks>
    /// Keep this shape intentionally generic — no protocol details, no hardware
    /// concepts. The mock is a stand-in for any consumer wiring the frontend
    /// against the standard Essentials DSP interfaces.
    /// </remarks>
    public class TesiraDspMockPropertiesConfig
    {
        /// <summary>
        /// Preset entries exposed via <see cref="PepperDash.Essentials.Core.IDspPresets.Presets"/>.
        /// Recall + save both no-op on the mock (they log and return success) — sufficient to
        /// prove the frontend → messenger → interface pipeline end-to-end.
        /// </summary>
        [JsonProperty("presets")]
        public Dictionary<string, TesiraDspMockPresetConfig> Presets { get; set; }
            = new Dictionary<string, TesiraDspMockPresetConfig>();

        /// <summary>
        /// Fader entries — each becomes a child device implementing
        /// <see cref="PepperDash.Essentials.Core.IBasicVolumeWithFeedback"/>.
        /// Registered with key <c>{parentKey}--{faderKey}</c> to match the Tesira EPI
        /// convention so consumers already wired for the real device can point at the mock
        /// by changing only the parent <c>type</c>.
        /// </summary>
        [JsonProperty("faderControlBlocks")]
        public Dictionary<string, TesiraDspMockFaderConfig> FaderControlBlocks { get; set; }
            = new Dictionary<string, TesiraDspMockFaderConfig>();

        /// <summary>
        /// Optional source-selector blocks — each becomes a child device implementing
        /// <see cref="PepperDash.Essentials.Core.DeviceTypeInterfaces.IHasInputs{T}"/>.
        /// Keyed by block key; child device registered as <c>{parentKey}--{blockKey}</c>
        /// to match the Tesira EPI <c>switcherControlBlocks</c> naming convention.
        /// </summary>
        [JsonProperty("switcherControlBlocks")]
        public Dictionary<string, TesiraDspMockSourceSelectorConfig> SwitcherControlBlocks { get; set; }
    }

    /// <summary>Preset entry (label only — mock does not hit hardware).</summary>
    public class TesiraDspMockPresetConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }
    }

    /// <summary>Fader entry with optional seed state.</summary>
    public class TesiraDspMockFaderConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <summary>Initial level as a percent 0-100. Defaults to 50.</summary>
        [JsonProperty("initialLevelPercent")]
        public ushort InitialLevelPercent { get; set; } = 50;

        /// <summary>Initial mute state. Defaults to false.</summary>
        [JsonProperty("initialMute")]
        public bool InitialMute { get; set; }
    }

    /// <summary>Source-selector entry — sources are keyed and labelled generically.</summary>
    public class TesiraDspMockSourceSelectorConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <summary>Initial selection (key into <see cref="Sources"/>). Optional.</summary>
        [JsonProperty("initialSource")]
        public string InitialSource { get; set; }

        [JsonProperty("sources")]
        public Dictionary<string, TesiraDspMockSourceConfig> Sources { get; set; }
            = new Dictionary<string, TesiraDspMockSourceConfig>();
    }

    /// <summary>Source entry (label only).</summary>
    public class TesiraDspMockSourceConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }
    }
}
