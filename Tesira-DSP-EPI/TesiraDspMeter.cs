using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Bridges;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.CrestronThread;
using Tesira_DSP_EPI.Extensions;

namespace Tesira_DSP_EPI
{
    //AudioMeter1 unsubscribe level 1 SomethingCool
    public class TesiraDspMeter : TesiraDspControlPoint, IKeyed
    {
        static readonly double meterMinimum = -100;
        static readonly double meterMaximum = 49;
        static readonly int defaultPollTime = 500;
        static readonly string meterAttributeCode = "level";

        public string MeterCustomName { get; set; }

        public IntFeedback MeterFeedback { get; set; }
        int currentMeter;

        public BoolFeedback SubscribedFeedback { get; set; }

        public StringFeedback LabelFeedback { get; set; }

		public TesiraDspMeter(uint key, TesiraMeterBlockConfig config, TesiraDsp parent)
            : base(config.meterInstanceTag, string.Empty, config.index, 0, parent)
        {
            Key = string.Format("{0}--{1}", Parent.Key, key);
            DeviceManager.AddDevice(this);

            Label = config.label;
            IsSubscribed = false;
            Enabled = config.enabled;

            MeterFeedback = new IntFeedback(() => currentMeter);
            SubscribedFeedback = new BoolFeedback(() => IsSubscribed);
            LabelFeedback = new StringFeedback(() => config.label);

            LabelFeedback.FireUpdate();

            /*CrestronConsole.AddNewConsoleCommand(s => Subscribe(), "enablemeters", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => UnSubscribe(), "disablemeters", "", ConsoleAccessLevelEnum.AccessOperator);*/
        }

        public override void Subscribe()
        {
            MeterCustomName = string.Format("{0}~meter{1}", this.InstanceTag1, this.Index1);
            SendSubscriptionCommand(MeterCustomName, meterAttributeCode, defaultPollTime, 0);
        }

        public void UnSubscribe()
        {
            SendUnSubscriptionCommand(MeterCustomName, meterAttributeCode, 0);
            IsSubscribed = false;
            SubscribedFeedback.FireUpdate();
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);  
        }

        public void ParseSubscriptionMessage(string customName, string message)
        {
            IsSubscribed = true;
            SubscribedFeedback.FireUpdate();

            Debug.Console(2, this, "Parsing Message - '{0}'", message);
            var value = Double.Parse(message).Scale(meterMinimum, meterMaximum, (double)ushort.MinValue, (double)ushort.MaxValue);
            currentMeter = (ushort)value;

            Debug.Console(2, this, "Scaled Meter Value - '{0}'", currentMeter);
            MeterFeedback.FireUpdate();
        }     
    }
}