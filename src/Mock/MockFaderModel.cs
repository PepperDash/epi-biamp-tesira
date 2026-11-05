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
            if (!HasLevel || Level == level) return false;
            Level = level;
            return true;
        }

        /// <summary>Sets the mute state. Returns true when it changed; false without a mute.</summary>
        public bool SetMute(bool muted)
        {
            if (!HasMute || IsMuted == muted) return false;
            IsMuted = muted;
            return true;
        }

        internal static ushort PercentToRaw(ushort percent)
        {
            if (percent >= 100) return MaxRawLevel;
            return (ushort)(percent * MaxRawLevel / 100);
        }
    }
}
