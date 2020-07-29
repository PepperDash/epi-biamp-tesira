using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI {
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

        void Subscribe();

        void Unsubscribe();

        bool Enabled { get; }

        string InstanceTag1 { get; }

        string InstanceTag2 { get; }

    }
}