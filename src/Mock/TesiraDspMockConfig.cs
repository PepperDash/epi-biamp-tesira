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

        /// <summary>
        /// Longest volume hold for every fader without its own <c>volumeHoldTimeoutMs</c>. Defaults 10000,
        /// as the real driver's.
        /// </summary>
        [JsonProperty("volumeHoldTimeoutMs")]
        public int VolumeHoldTimeoutMs { get; set; } = 10000;

        /// <summary>Optional device info the mock reports, as the real driver reports the DSP's.</summary>
        [JsonProperty("deviceInfo")]
        public TesiraDspMockDeviceInfoConfig DeviceInfo { get; set; }
    }

    /// <summary>
    /// Optional <c>deviceInfo</c> block: what the mock reports through IDeviceInfoProvider. Every field
    /// is optional; see <see cref="MockDeviceInfoValues"/> for the defaults.
    /// </summary>
    public class TesiraDspMockDeviceInfoConfig
    {
        [JsonProperty("hostName")]
        public string HostName { get; set; }

        [JsonProperty("ipAddress")]
        public string IpAddress { get; set; }

        [JsonProperty("macAddress")]
        public string MacAddress { get; set; }

        [JsonProperty("serialNumber")]
        public string SerialNumber { get; set; }

        [JsonProperty("firmwareVersion")]
        public string FirmwareVersion { get; set; }
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

        /// <summary>
        /// The block has a level, as the real fader's <c>hasLevel</c>. Defaults to true. False makes a
        /// mute-only block: its level stays 0 and level changes are ignored.
        /// </summary>
        [JsonProperty("hasLevel")]
        public bool HasLevel { get; set; } = true;

        /// <summary>
        /// The block has a mute, as the real fader's <c>hasMute</c>. Defaults to true. False makes a
        /// level-only block: it stays unmuted and mute changes are ignored.
        /// </summary>
        [JsonProperty("hasMute")]
        public bool HasMute { get; set; } = true;

        /// <summary>
        /// dB per volume step, as the real fader's <c>incrementAmount</c> (e.g. "2.0"). Unset keeps the
        /// mock's legacy ~1% step.
        /// </summary>
        [JsonProperty("incrementAmount")]
        public string IncrementAmount { get; set; }

        /// <summary>The fader's range in dB, which the real fader reads from the DSP. Defaults -100.</summary>
        [JsonProperty("minLevel")]
        public double MinLevel { get; set; } = MockFaderModel.DefaultMinLevel;

        /// <summary>The fader's range in dB, which the real fader reads from the DSP. Defaults 12.</summary>
        [JsonProperty("maxLevel")]
        public double MaxLevel { get; set; } = MockFaderModel.DefaultMaxLevel;

        /// <summary>Unmute on a volume-up press or a higher level, as the real <c>unmuteOnVolChange</c>.</summary>
        [JsonProperty("unmuteOnVolChange")]
        public bool UnmuteOnVolChange { get; set; }

        /// <summary>Interval between steps while volume up/down is held. Defaults 100, as the real fader.</summary>
        [JsonProperty("volumeRepeatRateMs")]
        public int VolumeRepeatRateMs { get; set; } = 100;

        /// <summary>
        /// Longest hold before the repeat stops; 0 or less disables it. Null (or omitted) inherits the
        /// device's <c>volumeHoldTimeoutMs</c>, as the real fader does.
        /// </summary>
        [JsonProperty("volumeHoldTimeoutMs")]
        public int? VolumeHoldTimeoutMs { get; set; }
    }

    /// <summary>Source-selector entry — sources are keyed and labelled generically.</summary>
    public class TesiraDspMockSourceSelectorConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <summary>Initial selection (key into <see cref="EffectiveSources"/>). Optional.</summary>
        [JsonProperty("initialSource")]
        public string InitialSource { get; set; }

        /// <summary>Mock-format source list.</summary>
        [JsonProperty("sources")]
        public Dictionary<string, TesiraDspMockSourceConfig> Sources { get; set; }

        /// <summary>
        /// Real Tesira-format input list, deserialized from <c>switcherInputs</c>.
        /// Used as a fallback when <see cref="Sources"/> is absent, making the mock
        /// drop-in compatible with real Tesira EPI configs without any config edits.
        /// </summary>
        [JsonProperty("switcherInputs")]
        public Dictionary<string, TesiraDspMockSourceConfig> SwitcherInputs { get; set; }

        /// <summary>
        /// Resolved source list — <see cref="Sources"/> if present and non-empty,
        /// otherwise <see cref="SwitcherInputs"/>.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, TesiraDspMockSourceConfig> EffectiveSources =>
            Sources?.Count > 0 ? Sources : SwitcherInputs;
    }

    /// <summary>Source entry (label only).</summary>
    public class TesiraDspMockSourceConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }
    }
}
