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

        protected override int ParseSelection(string value) => LogicSelectorStates.ActiveChannel(value);

        protected override void SendSelection(int input)
        {
            string command;
            if (input > 0)
            {
                command = string.Format("\"{0}\" set state {1} true", InstanceTag1, input);
            }
            else if (CurrentSourceIndex > 0)
            {
                // "None": turn off the active channel
                command = string.Format("\"{0}\" set state {1} false", InstanceTag1, CurrentSourceIndex);
            }
            else
            {
                this.LogVerbose("Logic selector already has no channel selected");
                return;
            }

            // Built here rather than with SendFullCommand, which would index "state" with Index1 rather
            // than the channel. The reply is a bare +OK; the change arrives as a "states" publish.
            Parent.CommandQueue.EnqueueCommand(new QueuedCommand(command, "state", this, priority: (int)CommandPriority.Critical));
        }
    }
}
