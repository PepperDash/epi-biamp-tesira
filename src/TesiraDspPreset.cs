using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDspPresetDevice : TesiraDspControlPoint, IDspPresets

    {
        private const string keyFormatter = "{0}--{1}";

        public TesiraDspPresetDevice(TesiraDsp parent)
            : base("", "", 0, 0, parent, string.Format(keyFormatter, parent.Key, "Presets"), "Presets", 0)
        {
            Presets = parent.Presets;
        }


        #region IHasDspPresets Members

        public Dictionary<string, IKeyName> Presets { get; private set; }

        #endregion

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var presetJoinMap = new TesiraPresetJoinMapAdvancedStandalone(joinStart);


            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                presetJoinMap = JsonConvert.DeserializeObject<TesiraPresetJoinMapAdvancedStandalone>(joinMapSerialized);


            var presetJoinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(presetJoinMapSerialized))
            {
                presetJoinMap =
                    JsonConvert.DeserializeObject<TesiraPresetJoinMapAdvancedStandalone>(presetJoinMapSerialized);
            }

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, presetJoinMap);
            }

            this.LogDebug("Linking to Trilist {trilistId:X}", trilist.ID);


            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, Parent.RunPreset);
            trilist.SetUShortSigAction(presetJoinMap.PresetName.JoinNumber, Parent.RunPresetNumber);


            foreach (var preset in Presets)
            {
                var p = preset.Value as TesiraPreset;
                if (p == null) continue;
                var runPresetIndex = p.PresetIndex;
                var presetIndex = runPresetIndex;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex - 1)].StringValue = p.PresetName;
                trilist.SetSigTrueAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex - 1),
                    () => RecallPreset(p.Key));
            }


            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

            };

        }

        #region Presets

        public void RunPresetNumber(ushort n)
        {
            Parent.RunPresetNumber(n);

        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="name">Preset Name</param>
        public void RunPreset(string name)
        {
            this.LogVerbose("Running Preset By Name: {presetName}", name);
            Parent.RunPreset(name);
        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="id">Preset Id</param>
        public void RunPreset(int id)
        {
            this.LogVerbose("Running Preset By ID: {presetId}", id);
            Parent.RunPreset(id);
        }

        public void RecallPreset(string key)
        {
            Parent.RecallPreset(key);
        }

        #endregion

    }

    public class TesiraPreset : TesiraDspPresets, IKeyName
    {
        public string Key { get; private set; }
        public string Name => Label;
        public int Index => PresetIndex;

        public TesiraPreset(string key) : base()
        {
            Key = key;
        }
    }
}