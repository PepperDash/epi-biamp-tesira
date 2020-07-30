using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace Tesira_DSP_EPI
{
    public class TesiraDspDeviceInfo : EssentialsBridgeableDevice
    {
        /// <summary>
        /// Feedback Collection for Component
        /// </summary>
        public FeedbackCollection<Feedback> Feedbacks;

        private readonly Dictionary<uint, TesiraDspPresets> Presets; 

        readonly TesiraDsp _parent;

        public StringFeedback NameFeedback { get; set; }

        /// <summary>
        /// Constructor for Device Info Object
        /// </summary>
        /// <param name="key">Unique Key</param>
        /// <param name="name">Friendly Name</param>
        /// <param name="parent">Parent Device</param>
        public TesiraDspDeviceInfo(string key, string name, TesiraDsp parent, Dictionary<uint, TesiraDspPresets> presets)
            : base(key, name)
        {
            Presets = presets;
            _parent = parent;

            NameFeedback = new StringFeedback(() => _parent.Name);
            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(_parent.CommunicationMonitor.IsOnlineFeedback);
            Feedbacks.Add(_parent.CommandPassthruFeedback);
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraDspDeviceJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraDspDeviceJoinMapAdvancedStandalone>(joinMapSerialized);

            var presetJoinMap = new TesiraPresetJoinMapAdvancedStandalone(joinStart);
            var presetJoinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(presetJoinMapSerialized))
            {
                presetJoinMap =
                    JsonConvert.DeserializeObject<TesiraPresetJoinMapAdvancedStandalone>(presetJoinMapSerialized);
            }

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
                bridge.AddJoinMap(Key, presetJoinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            //var comm = DspDevice as IBasicCommunication;

            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, _parent.RunPreset);

            foreach (var preset in Presets)
            {
                var p = preset;
                var runPresetIndex = preset.Key;
                var presetIndex = runPresetIndex - 1;
                trilist.StringInput[presetJoinMap.PresetNameFeedback.JoinNumber - presetIndex].StringValue = p.Value.Label;
                trilist.SetSigTrueAction(presetJoinMap.PresetSelection.JoinNumber + presetIndex, () => _parent.RunPresetNumber((ushort)runPresetIndex));
            }


            _parent.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            _parent.CommandPassthruFeedback.LinkInputSig(trilist.StringInput[joinMap.CommandPassThru.JoinNumber]);
            NameFeedback.LinkInputSig((trilist.StringInput[joinMap.Name.JoinNumber]));

            trilist.SetStringSigAction(joinMap.CommandPassThru.JoinNumber, _parent.SendLineRaw);

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

            };

        }
    }
}