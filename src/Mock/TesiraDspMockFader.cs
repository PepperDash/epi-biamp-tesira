using System;
using System.Reflection;
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

        // Typed backing fields so ForcePublishState() can call ForceFireUpdate().
        // The public properties expose them as IntFeedback/BoolFeedback (base types)
        // to satisfy IBasicVolumeWithFeedback without an explicit interface impl.
        private readonly ForceFireIntFeedback  _typedVolumeFeedback;
        private readonly ForceFireBoolFeedback _typedMuteFeedback;

        public IntFeedback  VolumeLevelFeedback { get; }
        public BoolFeedback MuteFeedback        { get; }

        public TesiraDspMockFader(string key, string name, ushort initialLevelPercent, bool initialMute)
            : base(key, name)
        {
            _volumeLevel = PercentToRaw(initialLevelPercent);
            _isMuted = initialMute;

            _typedVolumeFeedback = new ForceFireIntFeedback(key + "-VolumeLevel", () => _volumeLevel);
            _typedMuteFeedback   = new ForceFireBoolFeedback(key + "-Mute",       () => _isMuted);

            // Assign base-typed properties so the interface contract is satisfied.
            VolumeLevelFeedback = _typedVolumeFeedback;
            MuteFeedback        = _typedMuteFeedback;
        }

        public override bool CustomActivate()
        {
            // Do NOT fire feedbacks here. Leaving IntFeedback._IntValue at 0 means
            // the first ForcePublishState() call from TesiraDspMock's bootstrap timer
            // sees a real change (0 → actual value) and pushes state to subscribers.
            return base.CustomActivate();
        }

        /// <summary>
        /// Unconditionally broadcasts the current volume level and mute state to all
        /// subscribed WebSocket clients, regardless of whether the values have changed
        /// since the last push. Called by <see cref="TesiraDspMock"/>'s bootstrap
        /// timer so newly-connecting frontends receive state without needing to
        /// request <c>/fullStatus</c>.
        /// </summary>
        public void ForcePublishState()
        {
            _typedVolumeFeedback.ForceFireUpdate();
            _typedMuteFeedback.ForceFireUpdate();
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

        // ── Force-fire feedback helpers ──────────────────────────────────────
        //
        // IntFeedback.FireUpdate() and BoolFeedback.FireUpdate() only invoke
        // OutputChange when the value has changed since the last call, which means
        // a repeating timer that re-fires the same stored value is a no-op.
        // These subclasses reset the internal change-tracking field to a sentinel
        // before calling FireUpdate(), guaranteeing OutputChange fires every time
        // ForceFireUpdate() is called regardless of current value.

        private sealed class ForceFireIntFeedback : IntFeedback
        {
            // Cache the FieldInfo once per type. The private _IntValue field lives
            // on IntFeedback itself, so we target that declaring type explicitly.
            private static readonly FieldInfo s_field =
                typeof(IntFeedback).GetField(
                    "_IntValue",
                    BindingFlags.NonPublic | BindingFlags.Instance);

            public ForceFireIntFeedback(string key, Func<int> valueFunc)
                : base(key, valueFunc) { }

            /// <summary>
            /// Forces OutputChange to fire with the current value even if unchanged.
            /// Uses int.MaxValue as sentinel — outside the valid ushort range (0–65535),
            /// so ValueFunc() can never equal it.
            /// </summary>
            public void ForceFireUpdate()
            {
                s_field?.SetValue(this, int.MaxValue);
                FireUpdate();
            }
        }

        private sealed class ForceFireBoolFeedback : BoolFeedback
        {
            private static readonly FieldInfo s_field =
                typeof(BoolFeedback).GetField(
                    "_BoolValue",
                    BindingFlags.NonPublic | BindingFlags.Instance);

            public ForceFireBoolFeedback(string key, Func<bool> valueFunc)
                : base(key, valueFunc) { }

            /// <summary>
            /// Forces OutputChange to fire with the current mute value even if unchanged.
            /// Flips the tracking field to the opposite bool, then FireUpdate() detects
            /// a change back to the actual value.
            /// </summary>
            public void ForceFireUpdate()
            {
                s_field?.SetValue(this, !BoolValue);
                FireUpdate();
            }
        }
    }
}
