using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces
{
  /// <summary>
  /// Interface for devices that have state control functionality.
  /// </summary>
  public interface IHasStateControlWithFeedback : IHasStateControl
  {
    /// <summary>
    /// Sets the state to On.
    /// </summary>
    void StateOn();

    /// <summary>
    /// Sets the state to Off.
    /// </summary>
    void StateOff();

    /// <summary>
    /// Gets the state feedback for the device. This property provides a BoolFeedback
    /// that represents the current state (on/off) of the device.
    /// </summary>
    BoolFeedback StateFeedback { get; }
  }
}