using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
  public class TesiraDspLogicMeter : TesiraDspControlPoint, IStateFeedback
  {
    public BoolFeedback StateFeedback { get; private set; }

    private bool state;
    public bool State
    {
      get => state;
      private set
      {
        state = value;
        StateFeedback.FireUpdate();
      }
    }

    private const string meterAttributeCode = "state";
    private const int defaultPollTime = 500;

    /// <summary>
    /// Subscription Identifer for Meter Data
    /// </summary>
    public string MeterCustomName { get; set; }

    /// <summary>
    /// Integer Feedback for Meter
    /// </summary>
    public IntFeedback MeterFeedback { get; set; }

    /// <summary>
    /// Represents the subscription status of the meter.
    /// </summary>
    public BoolFeedback SubscribedFeedback { get; set; }

    public TesiraDspLogicMeter(string key, TesiraLogicMeterBlockConfig config, TesiraDsp parent)
      : base(config.MeterInstanceTag, string.Empty, config.Index, 0, parent, string.Format(TesiraDsp.KeyFormatter, parent.Key, key), config.Label, config.BridgeIndex)
    {
      Label = config.Label;
      Enabled = true;

      StateFeedback = new BoolFeedback(Key + "-StateFeedback", () => state);
      SubscribedFeedback = new BoolFeedback(Key + "-SubscribedFeedback", () => IsSubscribed);

      Feedbacks.Add(StateFeedback);
    }

    public override void Subscribe()
    {
      MeterCustomName = $"{InstanceTag1}__meter{Index1}";
      AddCustomName(MeterCustomName);
      SendSubscriptionCommand(MeterCustomName, meterAttributeCode, defaultPollTime, 0);
    }

    public override void ParseGetMessage(string attributeCode, string message)
    {
      this.LogVerbose("Parsing Message - {message}: message has an attributeCode of {attributeCode}", message, attributeCode);
    }

    public override void ParseSubscriptionMessage(string customName, string message)
    {
      this.LogVerbose("Parsing Subscription Message - {message}: message has an attributeCode of {attributeCode}", message, customName);
      IsSubscribed = true;
      SubscribedFeedback.FireUpdate();

      if (!bool.TryParse(message, out var newState))
      {
        this.LogError("Failed to parse logic meter state from message: {message}", message);
        return;
      }

      State = newState;

    }
  }
}