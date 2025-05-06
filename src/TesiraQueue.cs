using System;
using PepperDash.Core;
using Crestron.SimplSharp;
using Tesira_DSP_EPI.Interfaces;

#if SERIES4
using PepperDash.Core.Logging;
#endif

namespace Tesira_DSP_EPI
{
    public class TesiraQueue
    {

        public CrestronQueue LocalQueue { get; private set; }

        private TesiraDsp Parent { get; set; }

        //private CEvent DequeueEvent { get; set; }

        public bool CommandQueueInProgress { get; set; }

        private QueuedCommand LastDequeued { get; set; }
    
        /// <summary>
        /// Constructor for Tesira Queue
        /// </summary>
        /// <param name="queueSize">Maximum Queue Size</param>
        /// <param name="parent">Parent TesiraDsp Class</param>
        public TesiraQueue(int queueSize, TesiraDsp parent)
        {
            LocalQueue = new CrestronQueue(queueSize);
            Parent = parent;
            CommandQueueInProgress = false;

            //DequeueEvent = new CEvent(true, false);
        }

        /// <summary>
        /// Dequeue from TesiraQueue and process queue responses
        /// </summary>
        /// <param name="response">Command String comparator for QueuedCommand</param>
        public void AdvanceQueue(string response)
        {
#if SERIES4
            Parent.LogVerbose(string.Format("[AdvanceQueue] - Command Queue {0} in progress.",
                CommandQueueInProgress ? "is" : "is not"));
            Parent.LogVerbose(string.Format("[AdvanceQueue] - Incoming Response : \"{0}\".", response));
#else
            Debug.Console(2, Parent, "[AdvanceQueue] - Command Queue {0} in progress.",
                CommandQueueInProgress ? "is" : "is not");
            Debug.Console(2, Parent, "[AdvanceQueue] - Incoming Response : \"{0}\".", response);
#endif

            if (LastDequeued != null)
            {
#if SERIES4
                Parent.LogVerbose(string.Format("[AdvanceQueue] - Command Sent. CommandQueue Size: {0} : Outgoing Command - {1}",
                    LocalQueue.Count, LastDequeued.Command));
#else
                Debug.Console(2, Parent,
                    "[AdvanceQueue] - Command Sent. CommandQueue Size: {0} : Outgoing Command - {1}",
                    LocalQueue.Count, LastDequeued.Command);
#endif
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

        /// <summary>
        /// Adds a command from a child module to the queue
        /// </summary>
        /// <param name="commandToEnqueue">Command object from child module</param>
        public void EnqueueCommand(QueuedCommand commandToEnqueue)
        {
#if SERIES4
            Parent.LogVerbose(string.Format("[EqueueCommand(QueuedCommand)] - Command Queue {0} in progress.",
                CommandQueueInProgress ? "is" : "is not"));
#else
            Debug.Console(2, Parent, "[EqueueCommand(QueuedCommand)] - Command Queue {0} in progress.",
                CommandQueueInProgress ? "is" : "is not");
#endif

            LocalQueue.Enqueue(commandToEnqueue);
#if SERIES4
            Parent.LogVerbose(string.Format("[EqueueCommand(QueuedCommand)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.",
                commandToEnqueue.Command, LocalQueue.Count));
#else
            Debug.Console(2, Parent,
                "[EqueueCommand(QueuedCommand)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.",
                commandToEnqueue.Command, LocalQueue.Count);
#endif
            if (CommandQueueInProgress) return;
#if SERIES4
            Parent.LogVerbose("[EnqueueCommand(QueuedCommand)] - Triggering 'SendNextQueuedCommand'");
#else
            Debug.Console(2, Parent, "[EnqueueCommand(QueuedCommand)] - Triggering 'SendNextQueuedCommand'");
#endif
            if (LastDequeued == null)
                SendNextQueuedCommand();
        }

        /// <summary>
        /// Adds a raw string command to the queue
        /// </summary>
        /// <param name="command">String to enqueue</param>
        public void EnqueueCommand(string command)
        {
#if SERIES4
            Parent.LogVerbose(string.Format("[EqueueCommand(String)] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not"));
#else
            Debug.Console(2, Parent, "[EqueueCommand(String)] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");
#endif

            LocalQueue.Enqueue(command);
#if SERIES4
            Parent.LogVerbose(string.Format("[EqueueCommand(String)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, LocalQueue.Count));
            Parent.LogVerbose("[EnqueueCommand(String)] - Triggering 'SendNextQueuedCommand'");
#else
            Debug.Console(2, Parent, "[EqueueCommand(String)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, LocalQueue.Count);
            Debug.Console(2, Parent, "[EnqueueCommand(String)] - Triggering 'SendNextQueuedCommand'");
#endif

            if(LastDequeued == null)
                SendNextQueuedCommand();
        }

        /// <summary>
        /// Sends the next queued command to the DSP
        /// </summary>
        public void SendNextQueuedCommand()
        {
           // DequeueEvent.Wait();
#if SERIES4
            Parent.LogVerbose("[SendNextQueuedCommand] - Attempting to send a queued commend");
#else
            Debug.Console(2, Parent, "[SendNextQueuedCommand] - Attempting to send a queued commend");
#endif

            if (LocalQueue.IsEmpty)
            {
                CommandQueueInProgress = false;
                return;
            }
#if SERIES4
            Parent.LogVerbose(string.Format("[SendNextQueuedCommand] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not"));
#else
            Debug.Console(2, Parent, "[SendNextQueuedCommand] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");
#endif

            if (!Parent.Communication.IsConnected)
            {
#if SERIES4
                Parent.LogVerbose("[SendNextQueuedCommand] - Unable to send queued command - Tesira Disconnected");
#else
                Debug.Console(2, Parent, "[SendNextQueuedCommand] - Unable to send queued command - Tesira Disconnected");
#endif
                return;
            }
            CommandQueueInProgress = true;

            if (LocalQueue.Peek() is QueuedCommand)
            {
                LastDequeued = (QueuedCommand)LocalQueue.Dequeue();
#if SERIES4
                Parent.LogVerbose(string.Format("[SendNextQueuedCommand(QueuedCommand)] - Sending Line - {0}", LastDequeued.Command));
#else
                Debug.Console(2, Parent, "[SendNextQueuedCommand(QueuedCommand)] - Sending Line - {0}", LastDequeued.Command);
#endif
                Parent.SendLine(LastDequeued.Command);
            }
            else
            {
                LastDequeued = null;
                var nextCommand = (string)LocalQueue.Dequeue();
#if SERIES4
                Parent.LogVerbose(string.Format("[SendNextQueuedCommand(String)] - Sending Line - {0}", nextCommand));
#else
                Debug.Console(2, Parent, "[SendNextQueuedCommand(String)] - Sending Line - {0}", nextCommand);
#endif
                Parent.SendLine(nextCommand);
            }
        }

        /// <summary>
        /// Clears the TesiraQueue
        /// </summary>
        public void Clear()
        {
            if (LocalQueue == null) return;
            LocalQueue.Clear();
        }

    }

    /// <summary>
    /// Contains all data for a component command
    /// </summary>
    public class QueuedCommand
    {
        public QueuedCommand(String command, string attributeCode, ISubscribedComponent controlPoint)
        {
            Command = command;
            AttributeCode = attributeCode;
            ControlPoint = controlPoint;
        }

        public readonly string Command;
        public readonly string AttributeCode;
        public readonly ISubscribedComponent ControlPoint;
    }

}