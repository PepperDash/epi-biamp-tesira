using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// In-memory fader — implements <see cref="IBasicVolumeWithFeedback"/> with no
    /// hardware. Stores level (0–65535) and mute state, and fires the standard
    /// Essentials feedbacks on every change so the standard auto-messengers
    /// propagate state to consumers (e.g. React frontend hooks).
    /// </summary>
    public class TesiraDspMockFader : EssentialsDevice, IBasicVolumeWithFeedback
    {
        // Essentials volume is a ushort 0-65535. Callers on the config side supply
        // an initial percent (0-100) which we scale up once at construction.
        private const ushort MaxRawLevel = 65535;
        private const ushort VolumeStep  = 655; // ~1% per press for VolumeUp/Down
        private const ushort DefaultMuteToggleLevel = 0;

        private ushort _volumeLevel;
        private bool _isMuted;

        public IntFeedback VolumeLevelFeedback { get; }
        public BoolFeedback MuteFeedback { get; }

        public TesiraDspMockFader(string key, string name, ushort initialLevelPercent, bool initialMute)
            : base(key, name)
        {
            _volumeLevel = PercentToRaw(initialLevelPercent);
            _isMuted = initialMute;

            VolumeLevelFeedback = new IntFeedback(key + "-VolumeLevel", () => _volumeLevel);
            MuteFeedback        = new BoolFeedback(key + "-Mute",         () => _isMuted);
        }

        public override bool CustomActivate()
        {
            // Fire initial feedback so any consumer that subscribes at activation
            // sees the seeded state without needing to poke a control first.
            VolumeLevelFeedback.FireUpdate();
            MuteFeedback.FireUpdate();
            return base.CustomActivate();
        }

        public void SetVolume(ushort level)
        {
            if (_volumeLevel == level) return;
            _volumeLevel = level;
            this.LogDebug("SetVolume: {level}", level);
            VolumeLevelFeedback.FireUpdate();
        }

        public void VolumeUp(bool pressRelease)
        {
            if (!pressRelease) return;
            var target = (ushort)System.Math.Min(_volumeLevel + VolumeStep, MaxRawLevel);
            SetVolume(target);
        }

        public void VolumeDown(bool pressRelease)
        {
            if (!pressRelease) return;
            var target = (ushort)System.Math.Max(_volumeLevel - VolumeStep, 0);
            SetVolume(target);
        }

        public void MuteOn()
        {
            if (_isMuted) return;
            _isMuted = true;
            this.LogDebug("MuteOn");
            MuteFeedback.FireUpdate();
        }

        public void MuteOff()
        {
            if (!_isMuted) return;
            _isMuted = false;
            this.LogDebug("MuteOff");
            MuteFeedback.FireUpdate();
        }

        public void MuteToggle()
        {
            if (_isMuted) MuteOff(); else MuteOn();
            // Guard: if a caller toggles from mute-on directly with no level set yet,
            // still leave level at DefaultMuteToggleLevel so consumers see something
            // meaningful. (Kept explicit; no behaviour change otherwise.)
            if (!_isMuted && _volumeLevel == 0)
            {
                SetVolume(DefaultMuteToggleLevel);
            }
        }

        private static ushort PercentToRaw(ushort percent)
        {
            if (percent >= 100) return MaxRawLevel;
            return (ushort)(percent * MaxRawLevel / 100);
        }
    }
}
