using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Interfaces {
    public interface IRoutingWithFeedback : IRoutingNumeric  {
        IntFeedback SourceIndexFeedback { get; }
    }

    public interface IParseMessage
    {
        void ParseGetMessage(string attribute, string data);
    }

    public interface ISubscribedComponent : IParseMessage, IKeyed
    {
        void SendSubscriptionCommand(string customName, string attributeCode, int responseRate, int instanceTag);

        void SendUnSubscriptionCommand(string customName, string attributeCode, int instanceTag);

        void ParseSubscriptionMessage(string customName, string value);

        void Subscribe();

        void Unsubscribe();

        void AddCustomName(string customName);

        bool IsSubscribed { get;  }

        bool Enabled { get; }

        string InstanceTag1 { get; }

        string InstanceTag2 { get; }

        List<string> CustomNames { get; } 

    }

    public interface IVolumeComponent
    {
        void GetMinLevel();
        void GetMaxLevel();
        void GetVolume();
        double MinLevel { get; }
        double MaxLevel { get; }
    }
}