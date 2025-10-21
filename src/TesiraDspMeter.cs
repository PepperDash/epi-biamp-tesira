using System;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Extensions;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Represents a meter for the Tesira DSP.
    /// </summary>
    public class TesiraDspMeter : TesiraDspControlPoint, IMeterFeedback
    {
        private readonly double meterMinimum;
        private readonly double meterMaximum;
        private readonly int defaultPollTime;

        private const string meterAttributeCode = "level";
        private const double meterMinimumDefault = -100;
        private const double meterMaximumDefault = 49;
        private const int defaultPollTimeDefault = 500;

        private const string keyFormatter = "{0}--{1}";

        /// <summary>
        /// Subscription Identifer for Meter Data
        /// </summary>
        public string MeterCustomName { get; set; }

        /// <summary>
        /// Integer Feedback for Meter
        /// </summary>
        public IntFeedback MeterFeedback { get; set; }
        int currentMeter;

        /// <summary>
        /// Represents the subscription status of the meter.
        /// </summary>
        public BoolFeedback SubscribedFeedback { get; set; }

        /// <summary>
        /// Creates a new instance of the TesiraDspMeter class.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="config"></param>
        /// <param name="parent"></param>
        public TesiraDspMeter(string key, TesiraMeterBlockConfig config, TesiraDsp parent)
            : base(config.MeterInstanceTag, string.Empty, config.Index, 0, parent, string.Format(keyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            DeviceManager.AddDevice(this);

            Label = config.Label;
            Enabled = config.Enabled;

            MeterFeedback = new IntFeedback(Key + "-MeterFeedback", () => currentMeter);
            SubscribedFeedback = new BoolFeedback(Key + "-SubscribedFeedback", () => IsSubscribed);

            Feedbacks.Add(MeterFeedback);
            Feedbacks.Add(SubscribedFeedback);
            Feedbacks.Add(NameFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

            if (config.MeterData != null)
            {
                var data = config.MeterData;
                meterMinimum = data.MeterMimimum;
                meterMaximum = data.MeterMaxiumum;
                defaultPollTime = data.DefaultPollTime;
            }
            else
            {
                meterMinimum = meterMinimumDefault;
                meterMaximum = meterMaximumDefault;
                defaultPollTime = defaultPollTimeDefault;
            }
        }

        public override void Subscribe()
        {
            MeterCustomName = string.Format("{0}__meter{1}", InstanceTag1, Index1);
            AddCustomName(MeterCustomName);
            SendSubscriptionCommand(MeterCustomName, meterAttributeCode, defaultPollTime, 0);
        }

        public void UnSubscribe()
        {
            IsSubscribed = false;

            SendUnSubscriptionCommand(MeterCustomName, meterAttributeCode, 0);
            SubscribedFeedback.FireUpdate();
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            this.LogVerbose("Parsing Message: {message}. AttributeCode: {attributeCode}", message, attributeCode);
        }

        public override void ParseSubscriptionMessage(string customName, string message)
        {
            IsSubscribed = true;
            SubscribedFeedback.FireUpdate();

            this.LogVerbose("Parsing Message: {message}", message);
            var value = double.Parse(message).Scale(meterMinimum, meterMaximum, ushort.MinValue, ushort.MaxValue, this);
            currentMeter = (ushort)value;

            this.LogVerbose("Scaled Meter Value: {currentMeter}", currentMeter);
            MeterFeedback.FireUpdate();
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraMeterJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraMeterJoinMapAdvancedStandalone>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            this.LogVerbose("AddingMeterBridge {key} | Join:{joinNumber}", Key, joinMap.Label.JoinNumber);

            MeterFeedback.LinkInputSig(trilist.UShortInput[joinMap.Meter.JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Label.JoinNumber]);
            SubscribedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Subscribe.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.Subscribe.JoinNumber, Subscribe);
            trilist.SetSigFalseAction(joinMap.Subscribe.JoinNumber, UnSubscribe);

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