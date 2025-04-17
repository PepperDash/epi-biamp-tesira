using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI {
    public class TesiraDspPropertiesConfig
    {
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }

        public EssentialsControlPropertiesConfig Control { get; set; }

        /// <summary>
        /// These are key-value pairs, uint id, string type.  
        /// Valid types are level and mute.
        /// Need to include the index values somehow
        /// </summary>

        [JsonProperty("faderControlBlocks")]
        public Dictionary<string, TesiraFaderControlBlockConfig> FaderControlBlocks { get; set; }

        [JsonProperty("dialerControlBlocks")]
        public Dictionary<string, TesiraDialerControlBlockConfig> DialerControlBlocks { get; set; }

        [JsonProperty("switcherControlBlocks")]
        public Dictionary<string, TesiraSwitcherControlBlockConfig> SwitcherControlBlocks { get; set; }

        [JsonProperty("routerControlBlocks")]
        public Dictionary<string, TesiraRouterControlBlockConfig> RouterControlBlocks { get; set; }

        [JsonProperty("sourceSelectorControlBlocks")]
        public Dictionary<string, TesiraSourceSelectorControlBlockConfig> SourceSelectorControlBlocks { get; set; }

        [JsonProperty("presets")]
        public Dictionary<string, TesiraDspPresets> Presets { get; set; }

        [JsonProperty("stateControlBlocks")]
        public Dictionary<string, TesiraStateControlBlockConfig> StateControlBlocks { get; set; }

        [JsonProperty("meterControlBlocks")]
        public Dictionary<string, TesiraMeterBlockConfig> MeterControlBlocks { get; set; }

        [JsonProperty("crosspointStateControlBlocks")]
        public Dictionary<string, TesiraCrosspointStateBlockConfig> CrosspointStateControlBlocks { get; set; }

        [JsonProperty("roomCombinerControlBlocks")]
        public Dictionary<string, TesiraRoomCombinerBlockConfig> RoomCombinerControlBlocks { get; set; }

        [JsonProperty("tesiraExpanderBlocks")]
        public Dictionary<string, TesiraExpanderBlockConfig> ExpanderBlocks { get; set; }
        
        [JsonProperty("resubscribeString")]
        public string ResubscribeString { get; set; }
    }

    public class TesiraExpanderBlockConfig
    {
        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }
    }

    public class TesiraFaderControlBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("levelInstanceTag")]
        public string LevelInstanceTag { get; set; }

        [JsonProperty("muteInstanceTag")]
        public string MuteInstanceTag { get; set; }

        [JsonProperty("index1")]
        public int Index1 { get; set; }

        [JsonProperty("index2")]
        public int Index2 { get; set; }

        [JsonProperty("hasMute")]
        public bool HasMute { get; set; }

        [JsonProperty("hasLevel")]
        public bool HasLevel { get; set; }

        [JsonProperty("isMic")]
        public bool IsMic { get; set; }

        [JsonProperty("useAbsoluteValue")]
        public bool UseAbsoluteValue { get; set; }

        [JsonProperty("unmuteOnVolChange")]
        public bool UnmuteOnVolChange { get; set; }

        [JsonProperty("incrementAmount")]
        public string IncrementAmount { get; set; }

        [JsonProperty("permissions")]
        public int Permissions { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }
    }


    public class TesiraDialerControlBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("isVoip")]
        public bool IsVoip { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("displayNumber")]
        public string DisplayNumber { get; set; }

        [JsonProperty("dialerInstanceTag")]
        public string DialerInstanceTag { get; set; }

        [JsonProperty("controlStatusInstanceTag")]
        public string ControlStatusInstanceTag { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("callAppearance")]
        public int CallAppearance { get; set; }

        [JsonProperty("clearOnHangup")]
        public bool ClearOnHangup { get; set; }

        [JsonProperty("appendDtmf")]
        public bool AppendDtmf { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }
    }

    public class TesiraSwitcherControlBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("switcherInstanceTag")]
        public string SwitcherInstanceTag { get; set; }

        [JsonProperty("index1")]
        public int Index1 { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }

        [JsonProperty("switcherInputs")]
        public Dictionary<uint, RoutingPort> SwitcherInputs { get; set; }

        [JsonProperty("switcherOutputs")]
        public Dictionary<uint, RoutingPort> SwitcherOutputs { get; set; }

        [JsonProperty("showRoutedStringFeedback")]
        public bool ShowRoutedStringFeedback { get; set; }

        [JsonProperty("pollIntervalMs")]
        public long? PollIntervalMs { get; set; }
    }
    public class TesiraRouterControlBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("routerInstanceTag")]
        public string RouterInstanceTag { get; set; }

        [JsonProperty("index1")]
        public int Index1 { get; set; }

        [JsonProperty("routerInputs")]
        public Dictionary<uint, RoutingPort> RouterInputs { get; set; }

        [JsonProperty("routerOutput")]
        public RoutingPort RouterOutput { get; set; }

        [JsonProperty("showRoutedStringFeedback")]
        public bool ShowRoutedStringFeedback { get; set; }

        [JsonProperty("pollIntervalMs")]
        public long? PollIntervalMs { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }

    }

    public class TesiraSourceSelectorControlBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("sourceSelectorInstanceTag")]
        public string SourceSelectorInstanceTag { get; set; }

        [JsonProperty("sourceSelectorInputs")]
        public Dictionary<uint, RoutingPort> SourceSelectorInputs { get; set; }

        [JsonProperty("sourceSelectorOutput")]
        public RoutingPort SourceSelectorOutput { get; set; }

        [JsonProperty("showSelectedStringFeedback")]
        public bool ShowSelectedStringFeedback { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }

    }

    public class TesiraStateControlBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("stateInstanceTag")]
        public string StateInstanceTag { get; set; }

        [JsonProperty("subscriptionInstanceTag")]
        public string SubscriptionInstanceTag { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }
    }

    public class TesiraDspPresets
    {
        private string _label;

        [JsonProperty("label")]
        public string Label
        {
            get { return _label; }
            set
            {
                _label = value;
                LabelFeedback.FireUpdate();
            }
        }

        [JsonProperty("presetName")]
        public string PresetName { get; set; }

        [JsonProperty("presetId")]
        public int PresetId { get; set; }

        [JsonProperty("presetIndex")]
        public int PresetIndex { get; set; }

        public StringFeedback LabelFeedback;

        public TesiraDspPresets()
        {
            LabelFeedback = new StringFeedback(() => Label);
        }
    }

    public class TesiraMeterBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("meterInstanceTag")]
        public string MeterInstanceTag { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("meterData")]
        public MeterMetadata MeterData { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }
    }



    public class MeterMetadata
    {
        [JsonProperty("meterMinimum")]
        public double MeterMimimum { get; set; }

        [JsonProperty("meterMaximum")]
        public double MeterMaxiumum { get; set; }

        [JsonProperty("defaultPollTime")]
        public int DefaultPollTime { get; set; }
    }

    public class TesiraCrosspointStateBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("matrixInstanceTag")]
        public string MatrixInstanceTag { get; set; }

        [JsonProperty("index1")]
        public int Index1 { get; set; }

        [JsonProperty("index2")]
        public int Index2 { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }

        [JsonProperty("pollEnable")]
        public bool PollEnable { get; set; }

        [JsonProperty("pollTimeMs")]
        public long  PollTimeMs { get; set; }


    }

    public class TesiraRoomCombinerBlockConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("roomCombinerInstanceTag")]
        public string RoomCombinerInstanceTag { get; set; }

        [JsonProperty("roomIndex")]
        public int RoomIndex { get; set; }

        [JsonProperty("isMic")]
        public bool IsMic { get; set; }

        [JsonProperty("useAbsoluteValue")]
        public bool UseAbsoluteValue { get; set; }

        [JsonProperty("umuteOnVolChange")]
        public bool UnmuteOnVolChange { get; set; }

        [JsonProperty("incerementAmount")]
        public string IncrementAmount { get; set; }

        [JsonProperty("permissions")]
        public int Permissions { get; set; }

        [JsonProperty("bridgeIndex")]
        public uint? BridgeIndex { get; set; }
    }

    public class RoutingPort
    {
        [JsonProperty("label")]
        public string Label { get; set; }
    }
}