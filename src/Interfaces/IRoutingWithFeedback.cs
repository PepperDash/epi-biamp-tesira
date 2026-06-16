using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces
{
  /// <summary>
  /// A switching midpoint with route feedback that also exposes the routed source index.
  /// </summary>
  public interface IRoutingWithFeedback : IRoutingMidpointWithFeedback
  {
    IntFeedback SourceIndexFeedback { get; }
  }
}