using Independentsoft.Exchange;
using Newtonsoft.Json;
using PepperDash.Core.Logging;
using PepperDash.Essentials.AppServer.Messengers;

namespace PepperDash.Essentials.AppServer.Messengers
{
  /// <summary>
  /// Messenger class for State Control devices
  /// </summary>
  public class StateControlMessenger : MessengerBase
  {
    private IHasStateControl device;

    /// <summary>
    /// Constructor for StateControl Messenger
    /// </summary>
    /// <param name="key">messenger key</param>
    /// <param name="messagePath">message path</param>
    /// <param name="device">device instance</param>
    public StateControlMessenger(string key, string messagePath, IHasStateControl device) : base(key, messagePath, device)
    {
      this.device = device;
    }

    /// <inheritdoc />
    protected override void RegisterActions()
    {
      AddAction("/stateToggle", (id, content) => device.StateToggle());

      // Adding muteToggle path to allow for use with existing UIs
      AddAction("/muteToggle", (id, content) => device.StateToggle());

      if (!(device is IHasStateControlWithFeedback stateControlWithFeedback))
      {
        this.LogDebug("Device {key} does not implement IHasStateControlWithFeedback, skipping StateOn/StateOff actions", device.Key);
        return;
      }

      AddAction("/fullStatus", (id, content) => SendStatus(id));

      AddAction("/stateStatus", (id, content) => SendStatus(id));

      AddAction("/stateOn", (id, content) => stateControlWithFeedback.StateOn());
      AddAction("/stateOff", (id, content) => stateControlWithFeedback.StateOff());

      AddAction("/muteOn", (id, content) => stateControlWithFeedback.StateOn());
      AddAction("/muteOff", (id, content) => stateControlWithFeedback.StateOff());

      stateControlWithFeedback.StateFeedback.OutputChange += (s, a) => SendStatus();
    }

    private void SendStatus(string id = null)
    {
      if (!(device is IHasStateControlWithFeedback stateControlWithFeedback))
      {
        return;
      }

      var message = new StateMessage
      {
        Volume = new State
        {
          HasMute = true,
          Muted = stateControlWithFeedback.StateFeedback.BoolValue
        }
      };

      PostStatusMessage(message, id);
    }
  }

  /// <summary>
  /// Represents a StateMessage
  /// </summary>
  public class StateMessage : DeviceStateMessageBase
  {
    /// <summary>
    /// Gets or sets the Volume
    /// </summary>
    // Using volume at the moment to allow for compatibility with existing UIs
    [JsonProperty("volume", NullValueHandling = NullValueHandling.Ignore)]
    public State Volume { get; set; }
  }

  /// <summary>
  /// Represents a State
  /// </summary>
  public class State
  {
    [JsonProperty("hasMute", NullValueHandling = NullValueHandling.Ignore)]
    public bool? HasMute { get; set; }

    [JsonProperty("muted", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Muted { get; set; }
  }
}
