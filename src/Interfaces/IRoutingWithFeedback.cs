using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces
{
  public interface IRoutingWithFeedback : IRoutingNumeric
  {
    IntFeedback SourceIndexFeedback { get; }
  }
}