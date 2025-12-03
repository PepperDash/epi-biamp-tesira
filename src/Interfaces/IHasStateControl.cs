

using PepperDash.Core;

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