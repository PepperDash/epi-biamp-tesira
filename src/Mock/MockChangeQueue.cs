#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// Applies a mock block's changes one at a time, in the order they were posted, without holding a
    /// lock while a change is applied, so a subscriber that routes another device can't deadlock
    /// against it. A change posted while one is being applied (by a subscriber on the same thread, or
    /// by another thread) is queued and applied afterwards by the thread already applying; that
    /// <see cref="Post"/> returns at once.
    /// </summary>
    public sealed class MockChangeQueue<T>
    {
        private readonly object queueLock = new object();
        private readonly Queue<T> pending = new Queue<T>();
        private readonly Action<T> apply;
        private bool applying;

        public MockChangeQueue(Action<T> apply)
        {
            this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
        }

        /// <summary>
        /// Queues a change and, unless another call is already applying changes, applies every queued
        /// change. If applying one throws, the rest are still applied and the first exception is then
        /// rethrown.
        /// </summary>
        public void Post(T change)
        {
            lock (queueLock)
            {
                pending.Enqueue(change);
                if (applying) return;
                applying = true;
            }

            ExceptionDispatchInfo firstError = null;
            while (true)
            {
                T next;
                lock (queueLock)
                {
                    if (pending.Count == 0)
                    {
                        applying = false;
                        break;
                    }
                    next = pending.Dequeue();
                }

                try
                {
                    apply(next);
                }
                catch (Exception ex)
                {
                    if (firstError == null) firstError = ExceptionDispatchInfo.Capture(ex);
                }
            }

            firstError?.Throw();
        }
    }
}
