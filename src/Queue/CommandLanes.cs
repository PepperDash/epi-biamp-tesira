#nullable disable
using System;
using System.Collections.Generic;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
    /// <summary>
    /// Which lane a TTP command waits in. Background traffic (subscriptions and session housekeeping)
    /// can come in bursts of dozens, from startup, a watchdog restore or a resubscribe, so it waits
    /// behind anything a user or program asked for.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal static class CommandLanes
    {
        /// <summary>
        /// True for subscribe and unsubscribe commands and SESSION commands. Everything else (set,
        /// toggle, increment, get, preset recall, dialing, pass-through) is foreground.
        /// </summary>
        public static bool IsBackground(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;

            var text = command.TrimStart();
            if (text.StartsWith("SESSION ", StringComparison.OrdinalIgnoreCase)) return true;

            var verb = Verb(text);
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
    /// A two-lane FIFO: foreground items always come out before background ones, each lane in
    /// arrival order. Not thread safe; the owner locks around it.
    /// </summary>
    /// <remarks>Kept free of Crestron types so it can be unit tested.</remarks>
    internal sealed class TwoLaneQueue<T>
    {
        private readonly Queue<T> foreground = new Queue<T>();
        private readonly Queue<T> background = new Queue<T>();
        private readonly int capacity;

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
            if (foreground.Count > 0)
            {
                item = foreground.Dequeue();
                return true;
            }
            if (background.Count > 0)
            {
                item = background.Dequeue();
                return true;
            }
            item = default;
            return false;
        }

        public void Clear()
        {
            foreground.Clear();
            background.Clear();
        }
    }
}
