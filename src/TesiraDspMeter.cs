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

        /// <summary>
        /// True once something asked this meter to stream (bridge join high or <see cref="Subscribe"/>),
        /// until <see cref="UnSubscribe"/>. Meters publish every interval, so session start and
        /// resubscribe only restore the ones that were asked for.
        /// </summary>
        public bool SubscriptionRequested { get; private set; }

        // Request state and the command it sends change together, so an opt-out can't land
        // between an automatic restore's check and its subscribe.
        private readonly object requestLock = new object();

        // A stray publish (the unsubscribe was dropped, e.g. by Resubscribe clearing the queue)
        // re-sends the unsubscribe, at most this often.
        private static readonly TimeSpan strayUnsubscribeInterval = TimeSpan.FromSeconds(5);
        private DateTime lastStrayUnsubscribe = DateTime.MinValue;

        /// <summary>
        /// Starts metering and records the request, so reconnects and resubscribes restore it.
        /// </summary>
        public override void Subscribe()
        {
            lock (requestLock)
            {
                SubscriptionRequested = true;
                SendMeterSubscription();
            }
        }

        /// <summary>
        /// Re-subscribes only if metering was requested; never records a request. Used by session
        /// start, resubscribe and the watchdog so they can't undo an opt-out.
        /// </summary>
        public void RestoreSubscription()
        {
            lock (requestLock)
            {
                if (!SubscriptionRequested) return;
                SendMeterSubscription();
            }
        }

        private void SendMeterSubscription()
        {
            MeterCustomName = string.Format("{0}__meter{1}", InstanceTag1, Index1);
            AddCustomName(MeterCustomName);
            SendSubscriptionCommand(MeterCustomName, meterAttributeCode, defaultPollTime, 0);
        }

        public void UnSubscribe()
        {
            lock (requestLock)
            {
                SubscriptionRequested = false;
                IsSubscribed = false;

                SendUnSubscriptionCommand(MeterCustomName, meterAttributeCode, 0);
            }
            SubscribedFeedback.FireUpdate();
        }

        /// <summary>
        /// Subscribes when true, unsubscribes when false. Used by the bridge subscribe join.
        /// </summary>
        public void SetSubscribed(bool subscribe)
        {
            if (subscribe)
                Subscribe();
            else
                UnSubscribe();
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            this.LogVerbose("Parsing Message: {message}. AttributeCode: {attributeCode}", message, attributeCode);
        }

        public override void ParseSubscriptionMessage(string customName, string message)
        {
            lock (requestLock)
            {
                if (!SubscriptionRequested)
                {
                    // Not asked for: don't count it as subscribed (that would make it a watchdog
                    // candidate) and stop it streaming.
                    var now = DateTime.UtcNow;
                    if (now - lastStrayUnsubscribe < strayUnsubscribeInterval) return;
                    lastStrayUnsubscribe = now;
                    this.LogDebug("Publish for unrequested meter {customName}; unsubscribing", customName);
                    SendUnSubscriptionCommand(customName, meterAttributeCode, 0);
                    return;
                }
            }

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

            // One action for both edges: SetSigTrueAction and SetSigFalseAction share the sig's
            // single action slot, so setting both left only the unsubscribe.
            trilist.SetBoolSigAction(joinMap.Subscribe.JoinNumber, SetSubscribed);

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