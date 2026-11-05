#nullable disable
using System;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>The fault poll interval's limits: 0 = off, otherwise 10 seconds to 1 day.</summary>
    public static class FaultPollInterval
    {
        /// <summary>Shortest allowed interval; 1-9 is raised to this.</summary>
        public const int MinimumSecs = 10;

        /// <summary>
        /// Longest allowed interval (1 day); longer is lowered to this. The poll timer can't take
        /// more than int.MaxValue milliseconds (about 24.8 days), and nobody needs fault reports
        /// further apart than daily.
        /// </summary>
        public const int MaximumSecs = 86400;

        /// <summary>0 or less = off (0); otherwise clamped to <see cref="MinimumSecs"/>-<see cref="MaximumSecs"/>.</summary>
        public static int Normalize(int secs)
        {
            if (secs <= 0) return 0;
            return Math.Min(Math.Max(secs, MinimumSecs), MaximumSecs);
        }
    }
}
