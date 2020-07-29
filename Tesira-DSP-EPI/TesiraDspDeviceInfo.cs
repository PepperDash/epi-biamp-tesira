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
        public FeedbackCollection<Feedback> Feedbacks;

        readonly TesiraDsp _parent;

        public StringFeedback NameFeedback { get; set; }

        TesiraDspDeviceInfo(string key, string name, TesiraDsp parent)
            : base(key, name)
        {
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

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            //var comm = DspDevice as IBasicCommunication;


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