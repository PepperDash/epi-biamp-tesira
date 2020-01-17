using System;
using PepperDash.Essentials.Core;
namespace Tesira_DSP_EPI {
    public interface IRoutingWithFeedback : IRouting  {
        IntFeedback SourceIndexFeedback { get; }
    }
}