#nullable disable
using System;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Press-and-hold volume ramp state for one level control: an immediate step on press, a
    /// 200 ms delay, then a step every repeat interval until release. Each direction has its own
    /// hold-timeout safety stop, so a lost release cannot ramp forever.
    /// </summary>
    /// <remarks>
    /// Kept free of Crestron and Essentials types so it can be unit tested in the test host.
    /// State changes happen under a lock; steps are sent after the lock is released, because
    /// sending enqueues on the DSP command queue, whose lock is held while responses are parsed
    /// (and an out-of-range response calls <see cref="Cancel"/>).
    /// </remarks>
    internal sealed class VolumeRamp : IDisposable
    {
        internal const int RepeatDelayMs = 200;

        private readonly object rampLock = new object();
        private readonly Direction up;
        private readonly Direction down;
        private readonly Action<bool> sendStep;
        private readonly Action onUpPress;
        private readonly Action<string> logWarning;
        private readonly Func<int> repeatRateMs;
        private readonly Func<int> holdTimeoutMs;

        /// <param name="sendStep">Sends one increment (true) or decrement (false).</param>
        /// <param name="repeatRateMs">Interval between repeated steps while held.</param>
        /// <param name="holdTimeoutMs">Maximum hold per direction; 0 or less disables it.</param>
        /// <param name="onUpPress">Runs once per new up press (e.g. automatic unmute).</param>
        /// <param name="logWarning">Receives the hold-timeout warning.</param>
        public VolumeRamp(Action<bool> sendStep, Func<int> repeatRateMs, Func<int> holdTimeoutMs,
            Action onUpPress = null, Action<string> logWarning = null)
        {
            this.sendStep = sendStep ?? throw new ArgumentNullException(nameof(sendStep));
            this.repeatRateMs = repeatRateMs ?? throw new ArgumentNullException(nameof(repeatRateMs));
            this.holdTimeoutMs = holdTimeoutMs ?? throw new ArgumentNullException(nameof(holdTimeoutMs));
            this.onUpPress = onUpPress;
            this.logWarning = logWarning;

            up = new Direction(this, true);
            down = new Direction(this, false);
        }

        /// <summary>Press (true) or release (false) in the given direction.</summary>
        public void Set(bool isUp, bool press)
        {
            if (press) Press(isUp);
            else Release(isUp);
        }

        public void Press(bool isUp)
        {
            var d = Get(isUp);
            bool newPress;

            lock (rampLock)
            {
                newPress = !d.Repeating;
                if (newPress)
                {
                    d.Generation++;
                    d.Held = true;
                    Restart(d.DelayTimer, RepeatDelayMs);
                    d.HoldTimer.Stop();
                    var hold = holdTimeoutMs();
                    if (hold > 0) Restart(d.HoldTimer, hold);
                }
                else
                {
                    Restart(d.RepeatTimer, repeatRateMs());
                }
            }

            sendStep(isUp);
            if (newPress && isUp) onUpPress?.Invoke();
        }

        public void Release(bool isUp)
        {
            lock (rampLock) ReleaseLocked(Get(isUp));
        }

        private static void ReleaseLocked(Direction d)
        {
            d.Generation++;
            d.Held = false;
            d.Repeating = false;
            d.RepeatTimer.Stop();
            d.DelayTimer.Stop();
            d.HoldTimer.Stop();
        }

        /// <summary>
        /// Stops repeating in both directions (e.g. the DSP rejected a step as out of range).
        /// Hold-timeout timers stay armed; they release normally when they fire.
        /// </summary>
        public void Cancel()
        {
            lock (rampLock)
            {
                foreach (var d in new[] { up, down })
                {
                    d.RepeatTimer.Stop();
                    d.DelayTimer.Stop();
                    d.Generation++;
                    d.Held = false;
                    d.Repeating = false;
                }
            }
        }

        internal bool IsHeld(bool isUp) { lock (rampLock) return Get(isUp).Held; }
        internal bool IsRepeating(bool isUp) { lock (rampLock) return Get(isUp).Repeating; }
        internal bool IsHoldTimerArmed(bool isUp) { lock (rampLock) return Get(isUp).HoldTimer.Enabled; }
        internal bool IsRepeatTimerArmed(bool isUp) { lock (rampLock) return Get(isUp).RepeatTimer.Enabled; }

        // Timer callbacks. Each checks and changes state atomically, so a callback that was already
        // queued when the button was released finds Held cleared and does nothing.

        internal void OnDelayElapsed(bool isUp)
        {
            var d = Get(isUp);
            int generation;
            lock (rampLock)
            {
                if (!d.Held) return;
                d.Repeating = true;
                generation = d.Generation;
            }
            SendThenRearm(d, isUp, generation);
        }

        internal void OnRepeatElapsed(bool isUp)
        {
            var d = Get(isUp);
            int generation;
            lock (rampLock)
            {
                if (!d.Held || !d.Repeating) return;
                generation = d.Generation;
            }
            SendThenRearm(d, isUp, generation);
        }

        // Send first, then re-arm only if the same press is still repeating. Sending can block on
        // the command queue lock; arming first would let further callbacks pile up behind it and
        // enqueue steps after a release.
        private void SendThenRearm(Direction d, bool isUp, int generation)
        {
            sendStep(isUp);
            lock (rampLock)
            {
                if (d.Generation != generation || !d.Held || !d.Repeating) return;
                Restart(d.RepeatTimer, repeatRateMs());
            }
        }

        internal void OnHoldElapsed(bool isUp)
        {
            var d = Get(isUp);
            lock (rampLock)
            {
                // A one-shot timer is disabled when it fires. If it is enabled again, a newer press
                // re-armed it after this callback was queued, and that press must not be released.
                if (d.HoldTimer.Enabled) return;
                if (!d.Held && !d.Repeating) return;
                ReleaseLocked(d);
            }
            logWarning?.Invoke(string.Format("Volume {0} hold timeout expired; forcing release. Panel may have dropped.",
                isUp ? "up" : "down"));
        }

        /// <summary>Test hook: the hold timer fires as if its interval had elapsed.</summary>
        internal void FireHoldTimer(bool isUp)
        {
            lock (rampLock) Get(isUp).HoldTimer.Stop();
            OnHoldElapsed(isUp);
        }

        private Direction Get(bool isUp) => isUp ? up : down;

        private static void Restart(System.Timers.Timer timer, int intervalMs)
        {
            timer.Stop();
            timer.Interval = Math.Max(1, intervalMs);
            timer.Start();
        }

        public void Dispose()
        {
            lock (rampLock)
            {
                up.Dispose();
                down.Dispose();
            }
        }

        private sealed class Direction : IDisposable
        {
            public bool Held;
            public bool Repeating;
            // Bumped on every new press, release and cancel; a send that finishes under an older
            // generation must not re-arm the repeat.
            public int Generation;
            public readonly System.Timers.Timer DelayTimer;
            public readonly System.Timers.Timer RepeatTimer;
            public readonly System.Timers.Timer HoldTimer;

            public Direction(VolumeRamp owner, bool isUp)
            {
                DelayTimer = OneShot(() => owner.OnDelayElapsed(isUp));
                RepeatTimer = OneShot(() => owner.OnRepeatElapsed(isUp));
                HoldTimer = OneShot(() => owner.OnHoldElapsed(isUp));
            }

            private static System.Timers.Timer OneShot(Action elapsed)
            {
                var timer = new System.Timers.Timer { AutoReset = false, Enabled = false };
                timer.Elapsed += (sender, e) => elapsed();
                return timer;
            }

            public void Dispose()
            {
                DelayTimer.Dispose();
                RepeatTimer.Dispose();
                HoldTimer.Dispose();
            }
        }
    }
}
