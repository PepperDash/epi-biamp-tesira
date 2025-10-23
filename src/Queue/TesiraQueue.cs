﻿using Crestron.SimplSharp;
using PepperDash.Core.Logging;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
    public class TesiraQueue
    {
        public CrestronQueue<QueuedCommand> LocalQueue { get; private set; }

        private TesiraDsp Parent { get; set; }

        public bool CommandQueueInProgress { get; set; }

        private QueuedCommand lastDequeued;

        private readonly object lockObject = new object();

        /// <summary>
        /// Constructor for Tesira Queue
        /// </summary>
        /// <param name="queueSize">Maximum Queue Size</param>
        /// <param name="parent">Parent TesiraDsp Class</param>
        public TesiraQueue(int queueSize, TesiraDsp parent)
        {
            LocalQueue = new CrestronQueue<QueuedCommand>(queueSize);
            Parent = parent;
            CommandQueueInProgress = false;
        }

        /// <summary>
        /// Dequeue from TesiraQueue and process queue responses
        /// </summary>
        /// <param name="response">Command String comparator for QueuedCommand</param>
        public void AdvanceQueue(string response)
        {
            lock (lockObject)
            {
                Parent.LogVerbose("[AdvanceQueue] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                if (lastDequeued?.ControlPoint != null)
                {
                    Parent.LogVerbose("[AdvanceQueue] Response Receieved for parsing: '{response}'. Command: '{outgoingCommand}'", response, lastDequeued.Command);

                    lastDequeued.ControlPoint.ParseGetMessage(lastDequeued.AttributeCode, response);

                    lastDequeued = null;
                }
                else
                {
                    Parent.LogVerbose("[AdvanceQueue] Incoming Response: '{response}'. No Controlpoint waiting for response", response);
                }

                if (LocalQueue.IsEmpty)
                {
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
                Parent.LogVerbose("[EnqueueCommand] Attempting to enqueue command for {controlPoint}", commandToEnqueue.ControlPoint?.Key ?? "no control point");
                Parent.LogVerbose("[EnqueueCommand] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                LocalQueue.Enqueue(commandToEnqueue);

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
        public void EnqueueCommand(string command)
        {
            EnqueueCommand(new QueuedCommand(command, null, null));
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
                    CommandQueueInProgress = false;
                    return;
                }
                Parent.LogVerbose("[SendNextQueuedCommand] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                if (!Parent.Communication.IsConnected)
                {
                    Parent.LogVerbose("[SendNextQueuedCommand] Unable to send queued command. Tesira Disconnected");
                    return;
                }

                CommandQueueInProgress = true;

                lastDequeued = LocalQueue.Dequeue();
                Parent.LogVerbose("[SendNextQueuedCommand] Sending Line {line}. ControlPoint: {controlPoint}", lastDequeued.Command, lastDequeued.ControlPoint?.Key ?? "no control point");
                Parent.SendLine(lastDequeued.Command);
            }
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
                lastDequeued = null;
                CommandQueueInProgress = false;
            }
        }

    }

}