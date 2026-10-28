using System;
using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// A bridge feedback that goes high for a fixed time each time its preset is saved. One per
    /// preset per device; every bridge that device links to shares it.
    /// </summary>
    internal sealed class PresetSavedPulse : IDisposable
    {
        private const int PulseMs = 2000;

        private readonly object pulseLock = new object();
        private readonly System.Timers.Timer timer;
        private bool state;

        public BoolFeedback Feedback { get; }

        public PresetSavedPulse(string key)
        {
            Feedback = new BoolFeedback(key + "-savedFeedback", () => state);
            timer = new System.Timers.Timer(PulseMs) { AutoReset = false };
            timer.Elapsed += (sender, e) => Expire();
        }

        /// <summary>Goes high now and low after the pulse time; a new pulse restarts the time.</summary>
        public void Pulse()
        {
            // Publish inside the lock so a pulse and an expiry can't publish out of order. Feedback
            // subscribers (linked sigs) don't call back into this class.
            lock (pulseLock)
            {
                state = true;
                timer.Stop();
                timer.Start();
                Feedback.FireUpdate();
            }
        }

        private void Expire()
        {
            lock (pulseLock)
            {
                // A one-shot timer is disabled when it fires; enabled again means a newer pulse
                // restarted it after this handler was queued, so that pulse still owns the state.
                if (timer.Enabled) return;
                state = false;
                Feedback.FireUpdate();
            }
        }

        public void Dispose() => timer.Dispose();
    }
}
