using Crestron.SimplSharp;
using PepperDash.Core.Logging;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
    public class TesiraQueue
    {

        public CrestronQueue<QueuedCommand> LocalQueue { get; private set; }

        private TesiraDsp Parent { get; set; }

        public bool CommandQueueInProgress { get; set; }

        private QueuedCommand LastDequeued { get; set; }

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

                Parent.LogVerbose("[AdvanceQueue] Incoming Response: {response}", response);

                if (LastDequeued != null && LastDequeued.ControlPoint != null)
                {
                    Parent.LogVerbose("[AdvanceQueue] Command Sent. CommandQueue Size: {size} | Outgoing Command: {outgoingCommand}", LocalQueue.Count, LastDequeued.Command);

                    LastDequeued.ControlPoint.ParseGetMessage(LastDequeued.AttributeCode, response);

                    LastDequeued = null;
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
                Parent.LogVerbose("[EqueueCommand] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                LocalQueue.Enqueue(commandToEnqueue);

                Parent.LogVerbose("[EqueueCommand] Command Enqueued: {command}.  CommandQueue has {count} items", commandToEnqueue.Command, LocalQueue.Count);

                if (CommandQueueInProgress) return;

                if (LastDequeued == null)
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
            lock (lockObject)
            {
                Parent.LogVerbose("[EnqueueCommand] Command Queue {state} in progress.", CommandQueueInProgress ? "is" : "is not");

                LocalQueue.Enqueue(new QueuedCommand(command, null, null));
                Parent.LogVerbose("[EnqueueCommand] Command Enqueued: {command}.  CommandQueue has {count} Elements.", command, LocalQueue.Count);
                Parent.LogVerbose("[EnqueueCommand] Triggering 'SendNextQueuedCommand'");

                if (LastDequeued == null)
                {
                    Parent.LogVerbose("[EnqueueCommand] Triggering 'SendNextQueuedCommand'");
                    SendNextQueuedCommand();
                }
            }
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

                LastDequeued = LocalQueue.Dequeue();
                Parent.LogVerbose("[SendNextQueuedCommand] Sending Line {line}", LastDequeued.Command);
                Parent.SendLine(LastDequeued.Command);
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
                LastDequeued = null;
                CommandQueueInProgress = false;
            }
        }

    }

}