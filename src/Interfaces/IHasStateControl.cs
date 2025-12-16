using PepperDash.Core;

namespace PepperDash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces
{
  /// <summary>
  /// Interface for devices that have state control functionality.
  /// </summary>
  public interface IHasStateControl : IKeyName
  {
    /// <summary>
    /// Toggles the state between On and Off.
    /// </summary>
    void StateToggle();
  }
}