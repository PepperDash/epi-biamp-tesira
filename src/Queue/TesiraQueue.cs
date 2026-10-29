using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;
using PepperDash.Core.Logging;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
    public class TesiraQueue
    {
        // Two lanes: subscriptions and session traffic wait behind user and program commands, so a
        // startup, resubscribe or watchdog restore burst can't delay a button press (see CommandLanes).
        private TwoLaneQueue<QueuedCommand> LocalQueue { get; set; }

        private TesiraDsp Parent { get; set; }

        public bool CommandQueueInProgress { get; set; }

        /// <summary>
        /// Gets the number of items in the queue
        /// </summary>
        public int Count => LocalQueue?.Count ?? 0;

        /// <summary>
        /// Gets whether the queue is empty
        /// </summary>
        public bool IsEmpty => LocalQueue?.IsEmpty ?? true;

        private QueuedCommand lastDequeued;

        private readonly object lockObject = new object();

        // Level changes (set/increment/decrement) queued but not yet sent, per control point.
        // Updated with the queue under lockObject.
        private readonly PendingCommandCounter pendingLevelChanges = new PendingCommandCounter();

        private int _commandTimeoutMs = 8000;

        /// <summary>
        /// How long to wait for a response to an outstanding command before giving up on it and
        /// handing recovery to the parent (see HandleCommandTimeout). Without this, a single lost
        /// response stalls the queue indefinitely, since nothing else ever calls SendNextQueuedCommand.
        /// </summary>
        public int CommandTimeoutMs
        {
            get => _commandTimeoutMs;
            set => _commandTimeoutMs = value < 1 ? 1 : value;
        }

        private System.Timers.Timer commandTimeoutTimer;

        /// <summary>
        /// Constructor for Tesira Queue
        /// </summary>
        /// <param name="queueSize">Maximum Queue Size (ignored for priority queue)</param>
        /// <param name="parent">Parent TesiraDsp Class</param>
        public TesiraQueue(int queueSize, TesiraDsp parent)
        {
            LocalQueue = new TwoLaneQueue<QueuedCommand>(queueSize);
            Parent = parent;
            CommandQueueInProgress = false;
        }

        /// <summary>
        /// Dequeue from TesiraQueue and process queue responses
        /// </summary>
        /// <param name="response">Command String comparator for QueuedCommand</param>
        public void HandleResponse(string response)
        {
            lock (lockObject)
            {
                // A response arrived (whether or not it's the one actually expected), so the
                // outstanding command is no longer at risk of stalling the queue.
                commandTimeoutTimer?.Dispose();
                commandTimeoutTimer = null;

                Parent.LogVerbose("[HandleResponse] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                if (lastDequeued?.ControlPoint != null)
                {
                    Parent.LogVerbose("[HandleResponse] Response Received for parsing: '{response}'. Command: '{outgoingCommand}'", response, lastDequeued.Command);

                    lastDequeued.ControlPoint.ParseGetMessage(lastDequeued.AttributeCode, response);
                }
                else
                {
                    Parent.LogVerbose("[HandleResponse] Incoming Response: '{response}'. No Controlpoint waiting for response", response);
                }

                // Subscribe replies go to the watchdog, attributed to the component that sent them.
                if (lastDequeued?.RequestingComponent != null)
                    Parent.NotifySubscribeReply(lastDequeued, response);

                lastDequeued = null;

                if (LocalQueue.IsEmpty)
                {
                    Parent.LogVerbose("[HandleResponse] Command Queue is empty. Ending queue processing.");
                    CommandQueueInProgress = false;
                    return;
                }

                SendNextQueuedCommand();
            }
        }

        /// <summary>
        /// Adds a command from a child module to the queue
        /// </summary>
        /// <param name="commandToEnqueue">Command object from child module</param>
        public void EnqueueCommand(QueuedCommand commandToEnqueue)
        {
            lock (lockObject)
            {
                Parent.LogVerbose("[EnqueueCommand] Attempting to enqueue command for {controlPoint} with priority {priority}", commandToEnqueue.ControlPoint?.Key ?? "no control point", commandToEnqueue.Priority);
                Parent.LogVerbose("[EnqueueCommand] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                var countBefore = LocalQueue.Count;
                if (!LocalQueue.Enqueue(commandToEnqueue, CommandLanes.IsBackground(commandToEnqueue.Command)))
                    Parent.LogWarning("[EnqueueCommand] Queue full ({count}); dropped '{command}'", LocalQueue.Count, commandToEnqueue.Command);
                if (LocalQueue.Count > countBefore && IsLevelChange(commandToEnqueue))
                    pendingLevelChanges.Add(commandToEnqueue.ControlPoint);
                if (LocalQueue.Count > countBefore && commandToEnqueue.RequestingComponent != null)
                    Parent.NotifySubscribeEnqueued(commandToEnqueue);

                Parent.LogVerbose("[EnqueueCommand] Command Enqueued: '{command}'.  CommandQueue has {count} items", commandToEnqueue.Command, LocalQueue.Count);

                if (CommandQueueInProgress) return;

                if (lastDequeued == null)
                {
                    Parent.LogVerbose("[EnqueueCommand] Sending Next Queued Command");
                    SendNextQueuedCommand();
                }
            }
        }

        /// <summary>
        /// Adds a raw string command to the queue
        /// </summary>
        /// <param name="command">String to enqueue</param>
        /// <param name="sendLineRaw">Send command without appending delimiter</param>
        /// <param name="priority">Command priority (defaults to Low priority)</param>
        public void EnqueueCommand(string command, bool sendLineRaw = false, int priority = (int)CommandPriority.Low)
        {
            EnqueueCommand(new QueuedCommand(command, null, null, sendLineRaw: sendLineRaw, priority: priority));
        }

        /// <summary>
        /// Sends the next queued command to the DSP
        /// </summary>
        public void SendNextQueuedCommand()
        {
            lock (lockObject)
            {
                Parent.LogVerbose("[SendNextQueuedCommand] Attempting to send a queued command");

                if (LocalQueue.IsEmpty)
                {
                    Parent.LogVerbose("[SendNextQueuedCommand] Command Queue is empty. No command to send.");
                    CommandQueueInProgress = false;
                    return;
                }

                // TTP replies carry no correlation id, so only one command may ever be outstanding.
                if (lastDequeued != null)
                {
                    Parent.LogVerbose("[SendNextQueuedCommand] Command '{command}' is still awaiting a response. Not sending.", lastDequeued.Command);
                    return;
                }

                Parent.LogVerbose("[SendNextQueuedCommand] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                if (!Parent.Communication.IsConnected)
                {
                    Parent.LogVerbose("[SendNextQueuedCommand] Unable to send queued command. Tesira Disconnected");
                    return;
                }

                CommandQueueInProgress = true;

                if (!LocalQueue.TryDequeue(out lastDequeued))
                {
                    Parent.LogError("[SendNextQueuedCommand] Failed to dequeue command despite queue not being empty");
                    CommandQueueInProgress = false;
                    return;
                }

                if (IsLevelChange(lastDequeued)) pendingLevelChanges.Remove(lastDequeued.ControlPoint);

                Parent.LogVerbose("[SendNextQueuedCommand] Sending Line {line}. ControlPoint: {controlPoint}", lastDequeued.Command, lastDequeued.ControlPoint?.Key ?? "no control point");

                if (lastDequeued.SendLineRaw)
                    Parent.SendLineRaw(lastDequeued.Command, lastDequeued.BypassTxQueue);
                else
                    Parent.SendLine(lastDequeued.Command, lastDequeued.BypassTxQueue);

                // Guard against this exact command stalling the queue forever if its response
                // never arrives. HandleResponse cancels this on any reply; if it fires instead,
                // the command is discarded and the queue moves on rather than hanging indefinitely.
                var sentCommand = lastDequeued;
                commandTimeoutTimer?.Dispose();
                commandTimeoutTimer = new System.Timers.Timer(CommandTimeoutMs) { AutoReset = false };
                commandTimeoutTimer.Elapsed += (sender, e) => HandleCommandTimeout(sentCommand);
                commandTimeoutTimer.Start();
            }
        }

        /// <summary>
        /// Fires when no response arrived for <paramref name="timedOutCommand"/> within
        /// <see cref="CommandTimeoutMs"/> of it actually being sent. The parent decides how to
        /// recover before anything else is sent: its late reply would otherwise be consumed as the
        /// next command's response. Only if the parent declines does the queue move on.
        /// </summary>
        private void HandleCommandTimeout(QueuedCommand timedOutCommand)
        {
            lock (lockObject)
            {
                // Already resolved (a response arrived, queue was cleared, or a newer command
                // is now outstanding) - nothing to do.
                if (lastDequeued != timedOutCommand) return;

                Parent.LogWarning("[CommandTimeout] No response received for command '{command}' (ControlPoint: {controlPoint}) within {timeoutMs}ms.",
                    timedOutCommand.Command, timedOutCommand.ControlPoint?.Key ?? "no control point", CommandTimeoutMs);

                lastDequeued = null;
                commandTimeoutTimer = null;
            }

            // Outside the lock: the parent's recovery clears this queue, which re-enters the lock.
            if (Parent.NotifyCommandTimedOut(timedOutCommand)) return;

            Parent.LogWarning("[CommandTimeout] Discarding '{command}' and continuing.", timedOutCommand.Command);
            SendNextQueuedCommand();
        }

        /// <summary>
        /// Clears the TesiraQueue
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                if (LocalQueue == null) return;
                LocalQueue.Clear();
                pendingLevelChanges.Clear();
                Parent.NotifyQueueCleared();
                lastDequeued = null;
                CommandQueueInProgress = false;
                commandTimeoutTimer?.Dispose();
                commandTimeoutTimer = null;
            }
        }

        /// <summary>
        /// Discards commands that have not been sent yet, but keeps tracking the outstanding one (and
        /// its timeout) so its reply is still matched to it rather than to the next command sent.
        /// </summary>
        public void ClearPending()
        {
            lock (lockObject)
            {
                if (LocalQueue == null) return;
                LocalQueue.Clear();
                pendingLevelChanges.Clear();
                Parent.NotifyQueueCleared();
                if (lastDequeued == null) CommandQueueInProgress = false;
            }
        }

        /// <summary>
        /// True when a level change (set, increment or decrement) for <paramref name="controlPoint"/>
        /// is queued and not yet sent.
        /// </summary>
        public bool HasPendingLevelChange(ISubscribedComponent controlPoint)
        {
            lock (lockObject) return pendingLevelChanges.Has(controlPoint);
        }

        private static bool IsLevelChange(QueuedCommand command) =>
            command?.ControlPoint != null &&
            (command.AttributeCode == "level" || command.AttributeCode == "levelOut") &&
            PendingCommandCounter.IsChangeCommand(command.Command);

    }

}