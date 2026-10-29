#nullable disable
using System;
using System.Collections.Generic;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
    /// <summary>
    /// Which lane a TTP command waits in. Subscriptions come in bursts of dozens, from startup, a
    /// watchdog restore or a resubscribe, so they wait behind anything a user or program asked for.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal static class CommandLanes
    {
        /// <summary>
        /// True for subscribe and unsubscribe commands. Everything else (set, toggle, increment, get,
        /// preset recall, dialing, pass-through, SESSION) is foreground. SESSION stays in order with the
        /// reads that follow it: "SESSION set verbose false" must reach the DSP before them, or their
        /// replies arrive in a format the parsers don't handle.
        /// </summary>
        public static bool IsBackground(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;

            var verb = Verb(command.TrimStart());
            return string.Equals(verb, "subscribe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(verb, "unsubscribe", StringComparison.OrdinalIgnoreCase);
        }

        // The word after the instance tag. Tags may be quoted and contain spaces: "Room 1" set level 1 0.
        private static string Verb(string text)
        {
            int afterTag;
            if (text[0] == '"')
            {
                var close = text.IndexOf('"', 1);
                if (close < 0) return null;
                afterTag = close + 1;
            }
            else
            {
                afterTag = text.IndexOf(' ');
                if (afterTag < 0) return null;
            }

            var rest = text.Substring(afterTag).TrimStart();
            var end = rest.IndexOf(' ');
            return end < 0 ? rest : rest.Substring(0, end);
        }
    }

    /// <summary>
    /// A two-lane FIFO: foreground items come out before background ones, each lane in arrival order.
    /// So that a steady stream of foreground items can't hold background ones forever (the watchdog's
    /// probes are background), one background item goes after every <see cref="MaxForegroundRun"/>
    /// foreground items in a row while any are waiting. Not thread safe; the owner locks around it.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal sealed class TwoLaneQueue<T>
    {
        private readonly Queue<T> foreground = new Queue<T>();
        private readonly Queue<T> background = new Queue<T>();
        private readonly int capacity;
        private int foregroundRun;

        /// <summary>Foreground items served in a row before a waiting background item gets a turn.</summary>
        public const int MaxForegroundRun = 8;

        public TwoLaneQueue(int capacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        public int Count => foreground.Count + background.Count;

        public bool IsEmpty => Count == 0;

        /// <returns>false when the queue is full and the item was dropped</returns>
        public bool Enqueue(T item, bool isBackground)
        {
            if (Count >= capacity) return false;
            (isBackground ? background : foreground).Enqueue(item);
            return true;
        }

        public bool TryDequeue(out T item)
        {
            var backgroundsTurn = background.Count > 0 && foregroundRun >= MaxForegroundRun;
            if (foreground.Count > 0 && !backgroundsTurn)
            {
                item = foreground.Dequeue();
                // Only a run that kept a background item waiting counts.
                foregroundRun = background.Count > 0 ? foregroundRun + 1 : 0;
                return true;
            }
            if (background.Count > 0)
            {
                item = background.Dequeue();
                foregroundRun = 0;
                return true;
            }
            item = default;
            return false;
        }

        public void Clear()
        {
            foreground.Clear();
            background.Clear();
            foregroundRun = 0;
        }
    }
}
