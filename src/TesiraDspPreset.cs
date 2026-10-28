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
    public class TesiraDspPresetDevice : TesiraDspControlPoint, IHasDspPresetSave // extends IDspPresets

    {
        private const string keyFormatter = "{0}--{1}";

        /// <summary>
        /// Attribute-code prefix TesiraDsp.SavePreset tags save commands with, followed by the preset
        /// key, so the queue routes the save's reply here.
        /// </summary>
        internal const string SaveAttributePrefix = "savePreset:";

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
                LinkPresetJoin(trilist, Parent, p.Key,
                    (uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex - 1),
                    (uint)(presetJoinMap.PresetSavedFeedback.JoinNumber + presetIndex - 1),
                    Feedbacks);
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

        public void SavePreset(string key)
        {
            Parent.SavePreset(key);
        }

        #endregion

        /// <summary>
        /// Wires one preset's bridge joins, shared by the DSP bridge and this standalone bridge.
        /// With presetHoldTimeMs set, a short press recalls on release and a hold saves; otherwise
        /// the join recalls on press. The saved join pulses when the DSP confirms a save.
        /// </summary>
        internal static void LinkPresetJoin(BasicTriList trilist, TesiraDsp dsp, string presetKey, uint selectJoin,
            uint savedJoin, FeedbackCollection<PepperDash.Essentials.Core.Feedback> feedbacks)
        {
            if (dsp.PresetHoldTimeMs > 0)
            {
                trilist.SetSigHeldAction(selectJoin, (uint)dsp.PresetHoldTimeMs,
                    () => dsp.SavePreset(presetKey),
                    () => dsp.RecallPreset(presetKey));
            }
            else
            {
                trilist.SetSigTrueAction(selectJoin, () => dsp.RecallPreset(presetKey));
            }

            var pulse = new PresetSavedPulse(presetKey);
            pulse.Feedback.LinkInputSig(trilist.BooleanInput[savedJoin]);
            feedbacks.Add(pulse.Feedback);
            dsp.PresetSaved += key =>
            {
                if (key == presetKey) pulse.Pulse();
            };
        }

        /// <summary>
        /// Receives the replies to preset saves, which TesiraDsp.SavePreset routes here.
        /// </summary>
        public override void ParseGetMessage(string attributeCode, string message)
        {
            if (attributeCode == null || !attributeCode.StartsWith(SaveAttributePrefix, StringComparison.Ordinal))
                return;

            var key = attributeCode.Substring(SaveAttributePrefix.Length);
            if (message.StartsWith("+OK", StringComparison.Ordinal))
            {
                this.LogDebug("Preset {key} saved", key);
                Parent.OnPresetSaved(key);
            }
            else
            {
                this.LogWarning("Save of preset {key} failed: {message}", key, message);
            }
        }

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