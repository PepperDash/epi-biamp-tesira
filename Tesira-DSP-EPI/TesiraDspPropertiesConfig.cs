using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI {
    public class TesiraDspPropertiesConfig {
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }

        public EssentialsControlPropertiesConfig Control { get; set; }

        /// <summary>
        /// These are key-value pairs, string id, string type.  
        /// Valid types are level and mute.
        /// Need to include the index values somehow
        /// </summary>

        public Dictionary<uint, TesiraLevelControlBlockConfig> levelControlBlocks { get; set; }
        public Dictionary<uint, TesiraDialerControlBlockConfig> dialerControlBlocks { get; set; }
        public Dictionary<uint, TesiraSwitcherControlBlockConfig> switcherControlBlocks { get; set; }
        public Dictionary<uint, TesiraDspPresets> presets { get; set; }
        public Dictionary<uint, TesiraStateControlBlockConfig> stateControlBlocks { get; set; }
        // public Dictionary<uint, BiampTesiraForteDialerControlBlockConfig> DialerControlBlocks {get; set;}
        public Dictionary<uint, TesiraMeterBlockConfig> meterControlBlocks { get; set; }
        public Dictionary<uint, TesiraMatrixMixerBlockConfig> matrixMixerControlBlocks { get; set; }
        public Dictionary<uint, TesiraRoomCombinerBlockConfig> roomCombinerControlBlocks { get; set; }
    }

    public class TesiraLevelControlBlockConfig {
        public bool enabled { get; set; }
        public string label { get; set; }
        public string levelInstanceTag { get; set; }
        public string muteInstanceTag { get; set; }
        public int index1 { get; set; }
        public int index2 { get; set; }
        public bool hasMute { get; set; }
        public bool hasLevel { get; set; }
        public bool isMic { get; set; }
        public bool useAbsoluteValue { get; set; }
        public bool unmuteOnVolChange { get; set; }
        public string incrementAmount { get; set; }
        public int permissions { get; set; }
    }


    public class TesiraDialerControlBlockConfig {
        public bool enabled { get; set; }
        public bool isVoip { get; set; }
        public string label { get; set; }
        public string displayNumber { get; set; }

        public string dialerInstanceTag { get; set; }
        public string controlStatusInstanceTag { get; set; }
        public int index { get; set; }
        public int callAppearance { get; set; }

        public bool clearOnHangup { get; set; }
        public bool appendDtmf { get; set; }

    }

    public class TesiraSwitcherControlBlockConfig {
        public bool enabled { get; set; }
        public string label { get; set; }
		public string type { get; set; } 
        public string switcherInstanceTag { get; set; }
        public int index1 { get; set; }
    }

    public class TesiraStateControlBlockConfig {
        public bool enabled { get; set; }
        public string label { get; set; }

        public string stateInstanceTag { get; set; }
        public int index { get; set; }
    }

    public class TesiraDspPresets {
        private string _label;
        public string label {
            get {
                return this._label;

            }
            set {
                this._label = value;
                LabelFeedback.FireUpdate();
            }
        }
        public string preset { get; set; }
        public int number { get; set; }
        public StringFeedback LabelFeedback;
        public TesiraDspPresets() {
            LabelFeedback = new StringFeedback(() => { return label; });
        }
    }

    public class TesiraMeterBlockConfig
    {
        public bool enabled { get; set; }
        public string label { get; set; }

        public string meterInstanceTag { get; set; }
        public int index { get; set; }
    }

    public class TesiraMatrixMixerBlockConfig
    {
        public bool enabled { get; set; }
        public string label { get; set; }

        public string matrixInstanceTag { get; set; }
        public int index1 { get; set; }
        public int index2 { get; set; }
    }

    public class TesiraRoomCombinerBlockConfig
    {
        public bool enabled { get; set; }
        public string label { get; set; }

        public string roomCombinerInstanceTag { get; set; }
        public int roomIndex { get; set; }
        public bool preferredRoom { get; set; }

        public bool hasMute { get; set; }
        public bool hasLevel { get; set; }
        public bool useAbsoluteValue { get; set; }
        public bool unmuteOnVolChange { get; set; }
        public string incrementAmount { get; set; }
        public int permissions { get; set; }
    }
}