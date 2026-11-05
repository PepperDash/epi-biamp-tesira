#nullable disable
namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// Level and mute state of a mock fader, and which of the two the block has. Free of Crestron and
    /// Essentials types so it can be unit tested in the test host.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>TesiraDspFaderControl</c>'s <c>hasLevel</c> / <c>hasMute</c>. A mute-only block
    /// (no level attribute on the DSP) never reports a level, so its level stays 0, and every level
    /// change is ignored: the real driver skips volume up/down for it, and a level set is refused by
    /// the DSP. A level-only block likewise ignores mute changes and always reports unmuted.
    /// </remarks>
    internal sealed class MockFaderModel
    {
        internal const ushort MaxRawLevel = 65535;

        // Volume steps come from VolumeRamp's timer threads, outside its lock, and can overlap each
        // other or a direct SetLevel: every read-modify-write of the state happens under this lock.
        private readonly object _gate = new object();

        public MockFaderModel(bool hasLevel, bool hasMute, ushort initialLevelPercent, bool initialMute)
        {
            HasLevel = hasLevel;
            HasMute = hasMute;
            Level = hasLevel ? PercentToRaw(initialLevelPercent) : (ushort)0;
            IsMuted = hasMute && initialMute;
        }

        public bool HasLevel { get; }

        public bool HasMute { get; }

        /// <summary>Level, 0-65535 as Essentials scales it. Always 0 without a level.</summary>
        public ushort Level { get; private set; }

        /// <summary>Mute state. Always false without a mute.</summary>
        public bool IsMuted { get; private set; }

        /// <summary>Sets the level. Returns true when it changed; false without a level.</summary>
        public bool SetLevel(ushort level)
        {
            lock (_gate)
            {
                if (!HasLevel || Level == level) return false;
                Level = level;
                return true;
            }
        }

        /// <summary>Sets the mute state. Returns true when it changed; false without a mute.</summary>
        public bool SetMute(bool muted)
        {
            lock (_gate)
            {
                if (!HasMute || IsMuted == muted) return false;
                IsMuted = muted;
                return true;
            }
        }

        /// <summary>
        /// Steps the level up or down by <paramref name="rawStep"/>, stopping at the ends of the range.
        /// Returns true when it changed; false without a level or already at the end.
        /// </summary>
        public bool Step(bool up, ushort rawStep)
        {
            return Step(up, rawStep, out _);
        }

        /// <summary>
        /// As <see cref="Step(bool, ushort)"/>; <paramref name="pastEnd"/> is true when the full step would
        /// have passed the end of the range, decided with the same level the step changed. The real DSP
        /// refuses such a step and the real fader then stops its press-and-hold repeat.
        /// </summary>
        public bool Step(bool up, ushort rawStep, out bool pastEnd)
        {
            lock (_gate)  // the target is computed from the level it replaces (SetLevel re-enters the lock)
            {
                pastEnd = false;
                if (!HasLevel) return false;
                var unclamped = up ? Level + rawStep : Level - rawStep;
                pastEnd = unclamped > MaxRawLevel || unclamped < 0;
                var target = System.Math.Max(0, System.Math.Min(MaxRawLevel, unclamped));
                return SetLevel((ushort)target);
            }
        }

        internal const ushort LegacyRawStep = 655;    // ~1%: the step without an incrementAmount
        internal const double DefaultMinLevel = -100; // a Tesira level block's default range, dB
        internal const double DefaultMaxLevel = 12;

        /// <summary>
        /// One volume step on the 0-65535 scale: <paramref name="incrementAmount"/> dB (the real fader's
        /// config value, a string such as "2.0") over the fader's minLevel..maxLevel range, as the real
        /// fader's increment moves the level, at least 1. Without a usable amount or range, the legacy
        /// ~1% step.
        /// </summary>
        internal static ushort RawStep(string incrementAmount, double minLevel, double maxLevel)
        {
            if (!double.TryParse(incrementAmount, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var db)
                || !(db > 0) || double.IsInfinity(db) || !(maxLevel > minLevel))
                return LegacyRawStep;
            var raw = System.Math.Round(db / (maxLevel - minLevel) * MaxRawLevel);
            return (ushort)System.Math.Max(1, System.Math.Min(MaxRawLevel, raw));
        }

        internal static ushort PercentToRaw(ushort percent)
        {
            if (percent >= 100) return MaxRawLevel;
            return (ushort)(percent * MaxRawLevel / 100);
        }
    }
}
