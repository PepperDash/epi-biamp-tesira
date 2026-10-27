#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Mirrors one control point's outstanding commands, in queue order, so out-of-range recovery
    /// can tell whether its boundary correction would override a direct level set that is still
    /// queued behind the rejected step.
    /// </summary>
    /// <remarks>
    /// The DSP command queue sends one command at a time and routes exactly one reply (+OK or
    /// -ERR) per command back to the control point that sent it, so recording each send and
    /// popping on each reply keeps this in step with the queue. A reconnect clears the queue;
    /// call <see cref="Reset"/> when the control point resubscribes.
    /// </remarks>
    internal sealed class LevelRequestTracker
    {
        // Bounds the mirror if replies are ever lost without a reset.
        internal const int MaxPending = 256;

        private readonly object trackerLock = new object();
        private readonly Queue<bool> pendingIsLevelSet = new Queue<bool>();

        /// <summary>Records a command as it is queued.</summary>
        /// <param name="isLevelSet">True for a direct level set, false for anything else.</param>
        public void RecordRequest(bool isLevelSet)
        {
            lock (trackerLock)
            {
                if (pendingIsLevelSet.Count >= MaxPending) pendingIsLevelSet.Dequeue();
                pendingIsLevelSet.Enqueue(isLevelSet);
            }
        }

        /// <summary>Consumes the oldest outstanding command when its reply arrives.</summary>
        public void RecordResponse()
        {
            lock (trackerLock)
            {
                if (pendingIsLevelSet.Count > 0) pendingIsLevelSet.Dequeue();
            }
        }

        /// <summary>
        /// Call after <see cref="RecordResponse"/> for the rejected command. True when no direct
        /// level set is still queued behind it, so a boundary correction cannot undo a newer set.
        /// </summary>
        public bool ShouldCorrect()
        {
            lock (trackerLock) return !pendingIsLevelSet.Any(isSet => isSet);
        }

        public void Reset()
        {
            lock (trackerLock) pendingIsLevelSet.Clear();
        }
    }
}
