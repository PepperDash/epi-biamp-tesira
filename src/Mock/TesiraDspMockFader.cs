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
        // an initial percent (0-100) which the model scales up once at construction.
        private const ushort DefaultMuteToggleLevel = 0;

        private readonly MockFaderModel _model;
        // One step on the 0-65535 scale: incrementAmount dB over minLevel..maxLevel (legacy ~1% without one)
        private readonly ushort _rawStep;
        private readonly bool _unmuteOnVolChange;
        // Press-and-hold, as the real fader: a step on press, then repeats until release or the hold timeout
        private readonly VolumeRamp _ramp;
        private ushort _volumeLevel => _model.Level;
        private bool _isMuted => _model.IsMuted;

        /// <summary>The block has a level (config <c>hasLevel</c>), as on the real fader.</summary>
        public bool HasLevel => _model.HasLevel;

        /// <summary>The block has a mute (config <c>hasMute</c>), as on the real fader.</summary>
        public bool HasMute => _model.HasMute;

        // Typed backing fields so ForcePublishState() can call ForceFireUpdate().
        // The public properties expose them as IntFeedback/BoolFeedback (base types)
        // to satisfy IBasicVolumeWithFeedback without an explicit interface impl.
        private readonly ForceFireIntFeedback  _typedVolumeFeedback;
        private readonly ForceFireBoolFeedback _typedMuteFeedback;

        public IntFeedback  VolumeLevelFeedback { get; }
        public BoolFeedback MuteFeedback        { get; }

        public TesiraDspMockFader(string key, string name, ushort initialLevelPercent, bool initialMute,
            bool hasLevel = true, bool hasMute = true)
            : this(key, name, new TesiraDspMockFaderConfig
            {
                InitialLevelPercent = initialLevelPercent,
                InitialMute = initialMute,
                HasLevel = hasLevel,
                HasMute = hasMute,
            })
        {
        }

        public TesiraDspMockFader(string key, string name, TesiraDspMockFaderConfig config)
            : base(key, name)
        {
            config = config ?? new TesiraDspMockFaderConfig();
            _model = new MockFaderModel(config.HasLevel, config.HasMute, config.InitialLevelPercent, config.InitialMute);
            _rawStep = MockFaderModel.RawStep(config.IncrementAmount, config.MinLevel, config.MaxLevel);
            _unmuteOnVolChange = config.UnmuteOnVolChange;
            var repeatMs = config.VolumeRepeatRateMs;
            var holdMs = config.VolumeHoldTimeoutMs ?? 10000;  // the device resolves null first; as the real fader
            _ramp = new VolumeRamp(
                StepLevel,
                () => repeatMs,
                () => holdMs,
                onUpPress: () => { if (_unmuteOnVolChange && _isMuted) MuteOff(); },
                logWarning: message => this.LogWarning("{key}: {message}", Key, message));

            _typedVolumeFeedback = new ForceFireIntFeedback(key + "-VolumeLevel", () => _volumeLevel);
            _typedMuteFeedback   = new ForceFireBoolFeedback(key + "-Mute",       () => _isMuted);

            // Assign base-typed properties so the interface contract is satisfied.
            VolumeLevelFeedback = _typedVolumeFeedback;
            MuteFeedback        = _typedMuteFeedback;
        }

        protected override bool CustomActivate()
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
            if (!HasLevel)
            {
                // The real fader sends the set, and the DSP refuses it on a mute-only block
                this.LogDebug("SetVolume {level} ignored: the block has no level", level);
                return;
            }
            // As the real fader: a higher level unmutes when unmuteOnVolChange is set
            if (level > _volumeLevel && _unmuteOnVolChange && _isMuted) MuteOff();
            if (!_model.SetLevel(level)) return;
            this.LogDebug("SetVolume: {level}", level);
            VolumeLevelFeedback.FireUpdate();
        }

        /// <summary>Press (true) and release (false), as the real fader: one step on press, then repeats while held.</summary>
        public void VolumeUp(bool pressRelease)
        {
            if (!HasLevel) return;
            _ramp.Set(true, pressRelease);
        }

        /// <summary>Press (true) and release (false), as the real fader: one step on press, then repeats while held.</summary>
        public void VolumeDown(bool pressRelease)
        {
            if (!HasLevel) return;
            _ramp.Set(false, pressRelease);
        }

        private void StepLevel(bool up)
        {
            var changed = _model.Step(up, _rawStep, out var pastEnd);
            // As the real fader, which cancels its ramp on the DSP's out-of-range reply: a step past an
            // end lands on it (the real fader then sets the limit) and the repeat stops until a new press
            if (pastEnd) _ramp.Cancel();
            if (!changed) return;
            this.LogVerbose("Volume {direction}: {level}", up ? "up" : "down", _volumeLevel);
            VolumeLevelFeedback.FireUpdate();
        }

        public void MuteOn()
        {
            if (!HasMute)
            {
                this.LogDebug("MuteOn ignored: the block has no mute");
                return;
            }
            if (!_model.SetMute(true)) return;
            this.LogDebug("MuteOn");
            MuteFeedback.FireUpdate();
        }

        public void MuteOff()
        {
            if (!HasMute)
            {
                this.LogDebug("MuteOff ignored: the block has no mute");
                return;
            }
            if (!_model.SetMute(false)) return;
            this.LogDebug("MuteOff");
            MuteFeedback.FireUpdate();
        }

        public void MuteToggle()
        {
            if (!HasMute) return;
            if (_isMuted) MuteOff(); else MuteOn();
            // Guard: if a caller toggles from mute-on directly with no level set yet,
            // still leave level at DefaultMuteToggleLevel so consumers see something
            // meaningful. (Kept explicit; no behaviour change otherwise.)
            if (!_isMuted && _volumeLevel == 0)
            {
                SetVolume(DefaultMuteToggleLevel);
            }
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
                if (s_field == null) throw new MissingFieldException(typeof(IntFeedback).FullName, "_IntValue");
                s_field.SetValue(this, int.MaxValue);
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
