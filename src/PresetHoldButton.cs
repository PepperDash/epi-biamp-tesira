#nullable disable
using System;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// Press-and-hold logic for one bridge preset join: a release before the hold time recalls, and
    /// reaching the hold time while still pressed saves. Exactly one of the two runs per press.
    /// </summary>
    /// <remarks>
    /// Replaces Essentials' SetSigHeldAction for this join. That helper checks the sig and marks the
    /// press held without synchronising with the release, so a release racing the hold timer could
    /// run both the recall and the save. Here the decision is made once, under a lock, and the
    /// action runs after the lock is released. Kept free of Crestron types so it can be unit tested.
    /// </remarks>
    internal sealed class PresetHoldButton : IDisposable
    {
        private readonly object buttonLock = new object();
        private readonly Action recall;
        private readonly Action save;
        private readonly System.Timers.Timer holdTimer;

        private bool pressed;
        private bool decided;

        public PresetHoldButton(int holdMs, Action recall, Action save)
        {
            this.recall = recall ?? throw new ArgumentNullException(nameof(recall));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            holdTimer = new System.Timers.Timer(Math.Max(1, holdMs)) { AutoReset = false };
            holdTimer.Elapsed += (sender, e) => OnHoldElapsed();
        }

        /// <summary>The bridge sig changed: true on press, false on release.</summary>
        public void Set(bool press)
        {
            if (press) Press();
            else Release();
        }

        public void Press()
        {
            lock (buttonLock)
            {
                pressed = true;
                decided = false;
                holdTimer.Stop();
                holdTimer.Start();
            }
        }

        public void Release()
        {
            bool runRecall;
            lock (buttonLock)
            {
                if (!pressed) return;
                pressed = false;
                holdTimer.Stop();
                runRecall = !decided;
                decided = true;
            }
            if (runRecall) recall();
        }

        internal void OnHoldElapsed()
        {
            lock (buttonLock)
            {
                // A one-shot timer is disabled when it fires; enabled again means a newer press re-armed
                // it after this callback was queued. Otherwise skip if released or already decided.
                if (holdTimer.Enabled || !pressed || decided) return;
                decided = true;
            }
            save();
        }

        /// <summary>Test hook: the hold timer fires as if its interval had elapsed.</summary>
        internal void FireHold()
        {
            lock (buttonLock) holdTimer.Stop();
            OnHoldElapsed();
        }

        internal bool IsHoldTimerArmed
        {
            get { lock (buttonLock) return holdTimer.Enabled; }
        }

        public void Dispose() => holdTimer.Dispose();
    }
}
