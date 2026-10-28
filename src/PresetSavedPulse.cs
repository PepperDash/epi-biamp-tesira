using System;
using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// A bridge feedback that goes high for a fixed time each time its preset is saved.
    /// </summary>
    internal sealed class PresetSavedPulse : IDisposable
    {
        private const int PulseMs = 2000;

        private readonly System.Timers.Timer timer;
        private bool state;

        public BoolFeedback Feedback { get; }

        public PresetSavedPulse(string key)
        {
            Feedback = new BoolFeedback(key + "-savedFeedback", () => state);
            timer = new System.Timers.Timer(PulseMs) { AutoReset = false };
            timer.Elapsed += (sender, e) =>
            {
                state = false;
                Feedback.FireUpdate();
            };
        }

        /// <summary>Goes high now and low after the pulse time; a new pulse restarts the time.</summary>
        public void Pulse()
        {
            timer.Stop();
            state = true;
            Feedback.FireUpdate();
            timer.Start();
        }

        public void Dispose() => timer.Dispose();
    }
}
