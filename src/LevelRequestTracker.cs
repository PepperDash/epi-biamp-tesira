#nullable disable
using System;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Remembers the most recent level request (a ramp step or a direct set) so out-of-range
    /// recovery can tell whether its boundary correction would override a newer set.
    /// </summary>
    /// <remarks>
    /// Commands are queued in order, so when a step is rejected, any set requested after it is
    /// still queued behind it. Correcting to the boundary would then be appended after that
    /// set and undo it.
    /// </remarks>
    internal sealed class LevelRequestTracker
    {
        private const double Tolerance = 0.001;

        private readonly object trackerLock = new object();
        private bool lastWasSet;
        private double lastSetDb;

        public void RecordStep()
        {
            lock (trackerLock) lastWasSet = false;
        }

        public void RecordSet(double levelDb)
        {
            lock (trackerLock)
            {
                lastWasSet = true;
                lastSetDb = levelDb;
            }
        }

        /// <summary>
        /// True when the rejected request is the latest one, so a boundary correction is safe.
        /// False when a newer set was requested after it.
        /// </summary>
        /// <param name="rejectedDb">The attempted level from the DSP's out-of-range error.</param>
        public bool ShouldCorrect(double rejectedDb)
        {
            lock (trackerLock)
            {
                if (!lastWasSet) return true;
                return Math.Abs(rejectedDb - lastSetDb) < Tolerance;
            }
        }
    }
}
