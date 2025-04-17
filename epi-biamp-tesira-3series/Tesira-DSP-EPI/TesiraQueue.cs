using System;
using PepperDash.Core;
using Crestron.SimplSharp;
using Tesira_DSP_EPI.Interfaces;

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
            Debug.Console(2, Parent, "[AdvanceQueue] - Command Queue {0} in progress.",
                CommandQueueInProgress ? "is" : "is not");
            Debug.Console(2, Parent, "[AdvanceQueue] - Incoming Response : \"{0}\".", response);

            if (LastDequeued != null)
            {
                Debug.Console(2, Parent,
                    "[AdvanceQueue] - Command Sent. CommandQueue Size: {0} : Outgoing Command - {1}",
                    LocalQueue.Count, LastDequeued.Command);
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
            Debug.Console(2, Parent, "[EqueueCommand(QueuedCommand)] - Command Queue {0} in progress.",
                CommandQueueInProgress ? "is" : "is not");

            LocalQueue.Enqueue(commandToEnqueue);
            Debug.Console(2, Parent,
                "[EqueueCommand(QueuedCommand)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.",
                commandToEnqueue.Command, LocalQueue.Count);
            if (CommandQueueInProgress) return;
            Debug.Console(2, Parent, "[EnqueueCommand(QueuedCommand)] - Triggering 'SendNextQueuedCommand'");
            if (LastDequeued == null)
                SendNextQueuedCommand();

        }

        /// <summary>
        /// Adds a raw string command to the queue
        /// </summary>
        /// <param name="command">String to enqueue</param>
        public void EnqueueCommand(string command)
        {
            Debug.Console(2, Parent, "[EqueueCommand(String)] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            LocalQueue.Enqueue(command);
            Debug.Console(2, Parent, "[EqueueCommand(String)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, LocalQueue.Count);
            Debug.Console(2, Parent, "[EnqueueCommand(String)] - Triggering 'SendNextQueuedCommand'");

            if(LastDequeued == null)
                SendNextQueuedCommand();
        }

        /// <summary>
        /// Sends the next queued command to the DSP
        /// </summary>
        public void SendNextQueuedCommand()
        {
           // DequeueEvent.Wait();
            Debug.Console(2, Parent, "[SendNextQueuedCommand] - Attempting to send a queued commend");

            if (LocalQueue.IsEmpty)
            {
                CommandQueueInProgress = false;
                return;
            }
            Debug.Console(2, Parent, "[SendNextQueuedCommand] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            if (!Parent.Communication.IsConnected)
            {
                Debug.Console(2, Parent, "[SendNextQueuedCommand] - Unable to send queued command - Tesira Disconnected");
                return;
            }
            CommandQueueInProgress = true;

            if (LocalQueue.Peek() is QueuedCommand)
            {
                LastDequeued = (QueuedCommand)LocalQueue.Dequeue();
                Debug.Console(2, Parent, "[SendNextQueuedCommand(QueuedCommand)] - Sending Line - {0}", LastDequeued.Command);
                Parent.SendLine(LastDequeued.Command);
            }

            else
            {
                LastDequeued = null;
                var nextCommand = (string)LocalQueue.Dequeue();
                Debug.Console(2, Parent, "[SendNextQueuedCommand(String)] - Sending Line - {0}", nextCommand);
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