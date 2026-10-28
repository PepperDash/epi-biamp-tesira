using System;
using System.Linq;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue;
using PepperDash.Core.Logging;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    /// <summary>
    /// A Tesira Logic Selector block used as a switcher: interlocked boolean channels, at most one true.
    /// Behaves like a Source Selector (routing, feedback, bridge); only the TTP differs.
    /// </summary>
    /// <remarks>
    /// Confirmed on a TesiraFORTE (5.7.1.4): "subscribe states" publishes every channel in one line,
    /// e.g. [false true false false], on any change, so one subscription covers the block. Setting a
    /// channel true clears the others; setting the active channel false leaves none selected.
    /// </remarks>
    public class TesiraDspLogicSelector : TesiraDspSourceSelector
    {
        public TesiraDspLogicSelector(string key, TesiraLogicSelectorControlBlockConfig config, TesiraDsp parent)
            : base(key, config, parent)
        {
        }

        protected override string SelectionAttribute => "states";

        // Most channels a Tesira Logic Selector can have
        private const int MaxChannels = 32;

        // Channel count from the last "states" value; 0 until one arrives
        private int channelCount;

        // Highest channel this plugin has selected, so a clear before the first "states" value still
        // turns off anything it turned on
        private int highestRequested;

        protected override int ParseSelection(string value)
        {
            var count = LogicSelectorStates.ChannelCount(value);
            if (count > 0) channelCount = count;
            return LogicSelectorStates.ActiveChannel(value);
        }

        protected override void SendSelection(int input)
        {
            if (input > 0)
            {
                var limit = channelCount > 0 ? channelCount : MaxChannels;
                if (input > limit)
                {
                    this.LogWarning("Logic selector {key}: channel {input} is out of range (1-{limit}); ignored", Key, input, limit);
                    return;
                }
                if (input > highestRequested) highestRequested = input;
                SendState(input, true);
                return;
            }

            // "None": turn every channel off. The commands queue behind any selection still pending, so
            // the result is none whatever was selected when they run; clearing only the last reported
            // channel could miss a selection that hadn't been reported yet. Before the first "states"
            // value (it arrives with the subscription at startup), cover every configured channel and
            // every channel this plugin has selected.
            var count = channelCount > 0
                ? channelCount
                : Math.Max((int)SourceSelectorInputs.Keys.DefaultIfEmpty(0u).Max(), highestRequested);
            count = Math.Min(count, MaxChannels);
            if (count == 0)
            {
                this.LogWarning("Can't clear logic selector {key}: channel count not known yet", Key);
                return;
            }
            for (var channel = 1; channel <= count; channel++)
            {
                SendState(channel, false);
            }
        }

        private void SendState(int channel, bool value)
        {
            // Built here rather than with SendFullCommand, which would index "state" with Index1 rather
            // than the channel. The reply is a bare +OK; the change arrives as a "states" publish.
            var command = string.Format("\"{0}\" set state {1} {2}", InstanceTag1, channel, value ? "true" : "false");
            Parent.CommandQueue.EnqueueCommand(new QueuedCommand(command, "state", this, priority: (int)CommandPriority.Critical));
        }
    }
}
