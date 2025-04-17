using System;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using Tesira_DSP_EPI.Extensions;

namespace Tesira_DSP_EPI
{
    //AudioMeter1 unsubscribe level 1 SomethingCool
    public class TesiraDspMeter : TesiraDspControlPoint
    {
        private readonly double _meterMinimum;
        private readonly double _meterMaximum;
        private readonly int _defaultPollTime;

        private const string MeterAttributeCode = "level";
        private const double MeterMinimumDefault = -100;
        private const double MeterMaximumDefault = 49;
        private const int DefaultPollTimeDefault = 500;

        private const string KeyFormatter = "{0}--{1}";

        /// <summary>
        /// Subscription Identifer for Meter Data
        /// </summary>
        public string MeterCustomName { get; set; }

        /// <summary>
        /// Integer Feedback for Meter
        /// </summary>
        public IntFeedback MeterFeedback { get; set; }
        int _currentMeter;

        public BoolFeedback SubscribedFeedback { get; set; }

		public TesiraDspMeter(string key, TesiraMeterBlockConfig config, TesiraDsp parent)
            : base(config.MeterInstanceTag, string.Empty, config.Index, 0, parent, string.Format(KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
        {
            DeviceManager.AddDevice(this);

            Label = config.Label;
            Enabled = config.Enabled;

            MeterFeedback = new IntFeedback(Key + "-MeterFeedback",() => _currentMeter);
            SubscribedFeedback = new BoolFeedback(Key + "-SubscribedFeedback",() => IsSubscribed);

            Feedbacks.Add(MeterFeedback);
            Feedbacks.Add(SubscribedFeedback);
            Feedbacks.Add(NameFeedback);

            parent.Feedbacks.AddRange(Feedbacks);

		    if (config.MeterData != null)
		    {
		        var data = config.MeterData;
		        _meterMinimum = data.MeterMimimum;
		        _meterMaximum = data.MeterMaxiumum;
		        _defaultPollTime = data.DefaultPollTime;
		    }
		    else
		    {
                _meterMinimum = MeterMinimumDefault;
                _meterMaximum = MeterMaximumDefault;
                _defaultPollTime = DefaultPollTimeDefault;
		    }

            /*CrestronConsole.AddNewConsoleCommand(s => Subscribe(), "enablemeters", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => UnSubscribe(), "disablemeters", "", ConsoleAccessLevelEnum.AccessOperator);*/
        }

        public override void Subscribe()
        {
            MeterCustomName = string.Format("{0}__meter{1}", InstanceTag1, Index1);
            AddCustomName(MeterCustomName);
            SendSubscriptionCommand(MeterCustomName, MeterAttributeCode, _defaultPollTime, 0);
        }

        public void UnSubscribe()
        {
            IsSubscribed = false;

            SendUnSubscriptionCommand(MeterCustomName, MeterAttributeCode, 0);
            SubscribedFeedback.FireUpdate();
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);  
        }

        public override void ParseSubscriptionMessage(string customName, string message)
        {
            IsSubscribed = true;
            SubscribedFeedback.FireUpdate();

            Debug.Console(2, this, "Parsing Message - '{0}'", message);
            var value = Double.Parse(message).Scale(_meterMinimum, _meterMaximum, ushort.MinValue, ushort.MaxValue, this);
            _currentMeter = (ushort)value;

            Debug.Console(2, this, "Scaled Meter Value - '{0}'", _currentMeter);
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

            Debug.Console(2, this, "AddingMeterBridge {0} | Join:{1}", Key, joinMap.Label.JoinNumber);

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