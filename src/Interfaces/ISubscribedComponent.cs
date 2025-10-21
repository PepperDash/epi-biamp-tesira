using System.Collections.Generic;
using PepperDash.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces
{
  public interface ISubscribedComponent : IParseMessage, IKeyed
  {
    void SendSubscriptionCommand(string customName, string attributeCode, int responseRate, int instanceTag);

    void SendUnSubscriptionCommand(string customName, string attributeCode, int instanceTag);

    void ParseSubscriptionMessage(string customName, string value);

    void Subscribe();

    void Unsubscribe();

    void AddCustomName(string customName);

    bool IsSubscribed { get; }

    bool Enabled { get; }

    string InstanceTag1 { get; }

    string InstanceTag2 { get; }

    List<string> CustomNames { get; }

  }
}