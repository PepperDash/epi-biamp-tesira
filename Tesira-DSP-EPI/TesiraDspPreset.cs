using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;

namespace Tesira_DSP_EPI
{
    public class TesiraDspPresetDevice : TesiraDspControlPoint, IHasDspPresets
    {
        private const string KeyFormatter = "{0}--{1}";

        public readonly Dictionary<int, StringFeedback> PresetName;
        public readonly Dictionary<int, BoolFeedback> PresetPresent;


        public TesiraDspPresetDevice(TesiraDsp parent, List<IDspPreset> presets)
            : base("", "", 0, 0, parent, String.Format(KeyFormatter, parent.Key, "Presets"), "Presets", 0)
        {
            Presets = presets;

            PresetName = new Dictionary<int, StringFeedback>();
            PresetPresent = new Dictionary<int, BoolFeedback>();
            PresetName.Clear();
            PresetPresent.Clear();
            foreach (var preset in Presets.OfType<TesiraPreset>())
            {
                var p = preset;
                PresetName.Add(p.Index, new StringFeedback(() => p.Name));
                PresetPresent.Add(p.Index, new BoolFeedback(() => true));
            }

        }


        #region IHasDspPresets Members

        public List<IDspPreset> Presets { get; private set; }


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

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));


            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, Parent.RunPreset);

            foreach (var preset in Presets)
            {
                var p = preset as TesiraPreset;
                if (p == null) continue;
                var runPresetIndex = p.PresetData.PresetIndex;
                var presetIndex = runPresetIndex;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex)].StringValue = p.PresetData.Label;
                trilist.SetSigTrueAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex), () => RunPresetNumber((ushort)presetIndex));
                PresetName[presetIndex].LinkInputSig(trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex - 1)]);
                PresetPresent[presetIndex].LinkInputSig(trilist.BooleanInput[(uint)(presetJoinMap.PresetValidFeedback.JoinNumber + presetIndex - 1)]);
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
            Debug.Console(2, this, "Attempting to run preset {0}", n);

            foreach (var preset in Presets.OfType<TesiraPreset>().Where(preset => preset.Index == n))
            {
                Debug.Console(2, this, "Found a matching Preset - {0}", preset.PresetData.PresetId);
                RecallPreset(preset);
            }

        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="name">Preset Name</param>
        public void RunPreset(string name)
        {
            Debug.Console(2, this, "Running Preset By Name - {0}", name);
            //Parent.SendLine(string.Format("DEVICE recallPresetByName \"{0}\"", name));
            Parent.CommandQueue.AddCommandToQueue(string.Format("DEVICE recallPresetByName \"{0}\"", name));
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPresetByName \"{0}\"", name));
        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="id">Preset Id</param>
        public void RunPreset(int id)
        {
            Debug.Console(2, this, "Running Preset By ID - {0}", id);
            //Parent.SendLine(string.Format("DEVICE recallPreset {0}", id));
            Parent.CommandQueue.AddCommandToQueue(string.Format("DEVICE recallPreset {0}", id));
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPreset {0}", id));
        }

        public void RecallPreset(IDspPreset preset)
        {
            Debug.Console(2, this, "Running preset {0}", preset.Name);
            var tesiraPreset = preset as TesiraPreset;
            if (tesiraPreset == null) return;



            if (!String.IsNullOrEmpty(tesiraPreset.PresetName))
            {
                RunPreset(tesiraPreset.PresetData.PresetName);
            }
            else
            {
                RunPreset(tesiraPreset.PresetData.PresetId);
            }
        }

        #endregion

    }

    public class TesiraPreset : TesiraDspPresets, IDspPreset
    {
        public string Name { get; private set; }
        public int Index { get; private set; }

        public TesiraDspPresets PresetData { get; private set; }

        public TesiraPreset(TesiraDspPresets data)
        {
            PresetData = data;
            Name = data.Label;
            Index = data.PresetIndex;
        }
    }

    
}