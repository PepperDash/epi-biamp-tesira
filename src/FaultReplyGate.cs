#nullable disable

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Decides which fault poll replies to apply. While polling is on, every reply is applied. While
    /// it's off, only replies to explicit one-shot polls (PollNow / d13) are applied, one per request,
    /// so a reply to a periodic poll queued before polling was turned off can't restore the values
    /// that turning it off cleared. Not thread-safe: the caller holds its own lock.
    /// </summary>
    public class FaultReplyGate
    {
        private int pendingOneShots;

        /// <summary>Explicit polls sent while polling was off whose replies haven't arrived yet.</summary>
        public int PendingOneShots => pendingOneShots;

        /// <summary>Call when a poll is sent. Only polls sent while polling is off are counted.</summary>
        public void PollSent(bool pollingEnabled)
        {
            if (!pollingEnabled) pendingOneShots++;
        }

        /// <summary>Call when polling is turned off: replies to earlier polls are no longer wanted.</summary>
        public void PollingTurnedOff()
        {
            pendingOneShots = 0;
        }

        /// <summary>Call for each parsed reply. True if it should be applied.</summary>
        public bool ShouldApply(bool pollingEnabled)
        {
            if (pollingEnabled) return true;
            if (pendingOneShots <= 0) return false;
            pendingOneShots--;
            return true;
        }
    }
}
