#nullable disable
using System;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Doubling backoff counted in ticks: due on the 1st tick, then after 2, 4, 8 ... ticks, up to
    /// <see cref="MaxTicks"/> apart. <see cref="Reset"/> starts over. Not thread-safe.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    public sealed class RetryBackoff
    {
        private int every = 1;
        private int ticks;

        public RetryBackoff(int maxTicks)
        {
            MaxTicks = Math.Max(1, maxTicks);
        }

        /// <summary>The longest gap between retries, in ticks.</summary>
        public int MaxTicks { get; }

        /// <summary>Counts one tick. True when a retry is due.</summary>
        public bool Tick()
        {
            if (++ticks < every) return false;
            ticks = 0;
            every = Math.Min(every * 2, MaxTicks);
            return true;
        }

        public void Reset()
        {
            every = 1;
            ticks = 0;
        }
    }
}
