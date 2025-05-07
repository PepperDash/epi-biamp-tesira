using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;

#if SERIES4
using PepperDash.Core.Logging;
#endif

namespace Tesira_DSP_EPI
{
    public class TesiraDspPresetDevice : TesiraDspControlPoint
#if SERIES4
        ,IDspPresets
#endif

    {
        private const string KeyFormatter = "{0}--{1}";



        public TesiraDspPresetDevice(TesiraDsp parent)
            : base("", "", 0, 0, parent, String.Format(KeyFormatter, parent.Key, "Presets"), "Presets", 0)
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

#if SERIES4
            this.LogDebug(string.Format("Linking to Trilist '{0}'", trilist.ID.ToString("X")));
#else
            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
#endif

            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, Parent.RunPreset);
            trilist.SetUShortSigAction(presetJoinMap.PresetName.JoinNumber, Parent.RunPresetNumber);


            foreach (var preset in Presets)
            {
                var p = preset.Value as TesiraPreset;
                if (p == null) continue;
                var runPresetIndex = p.PresetIndex;
                var presetIndex = runPresetIndex;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex - 1)].StringValue = p.PresetName;
                //trilist.SetSigTrueAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex - 1),
                //() => RecallPreset(p.Key));
                trilist.SetSigHeldAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex - 1), 5000,
                    () => StorePreset(p.Key),
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

#if SERIES4
            this.LogVerbose(string.Format("Running Preset By Name - {0}", name));
#else
            Debug.Console(2, this, "Running Preset By Name - {0}", name);
#endif

            Parent.RunPreset(name);
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPresetByName \"{0}\"", name));
        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="id">Preset Id</param>
        public void RunPreset(int id)
        {

#if SERIES4
            this.LogVerbose(string.Format("Running Preset By ID - {0}", id));
#else
            Debug.Console(2, this, "Running Preset By ID - {0}", id);
#endif

            Parent.RunPreset(id);
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPreset {0}", id));
        }

        public void RecallPreset(string key)
        {
            Parent.RecallPreset(key);
        }

        public void SavePresetNumber(ushort n)
        {
            Parent.SavePresetNumber(n);

        }

        /// <summary>
        /// Saves a preset with the given name
        /// </summary>
        /// <param name="name">Preset Name</param>
        public void SavePreset(string name)
        {

#if SERIES4
            Debug.LogVerbose(this, "Saving Preset By Name - {0}", name);
#else
            Debug.Console(2, this, "Saving Preset By Name - {0}", name);
#endif

            Parent.SavePreset(name);
        }

        /// <summary>
        /// Saves a preset with the given ID
        /// </summary>
        /// <param id="id">Preset ID</param>
        public void SavePreset(int id)
        {

#if SERIES4
            Debug.LogVerbose(this, "Saving Preset By ID - {0}", id);
#else
            Debug.Console(2, this, "Saving Preset By ID - {0}", id);
#endif

            Parent.SavePreset(id);
        }

        public void StorePreset(string key)
        {
            Parent.StorePreset(key);
        }

#endregion

    }

    public class TesiraPreset : TesiraDspPresets, IKeyName
    {
        public string Key { get; private set; }
        public string Name { get { return Label; } }
        public int Index { get { return PresetIndex; } }

        public TesiraPreset(string key)
            : base()
        {
            Key = key;
        }
    }

    
}