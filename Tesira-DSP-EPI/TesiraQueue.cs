using PepperDash.Core;
using Crestron.SimplSharp;

namespace Tesira_DSP_EPI
{
    public class TesiraQueue
    {

        private CrestronQueue LocalQueue { get; set; }

        private TesiraDsp Parent { get; set; }

        //private bool CommandQueueInProgress { get; set; }
    
        /// <summary>
        /// Constructor for Tesira Queue
        /// </summary>
        /// <param name="queueSize">Maximum Queue Size</param>
        /// <param name="parent">Parent TesiraDsp Class</param>
        public TesiraQueue(int queueSize, TesiraDsp parent)
        {
            LocalQueue = new CrestronQueue(queueSize);
            Parent = parent;
            //CommandQueueInProgress = false;
        }

        /// <summary>
        /// Dequeue from TesiraQueue and process queue responses
        /// </summary>
        /// <param name="cmd">Command String comparator for QueuedCommand</param>
        public void AdvanceQueue(string cmd)
        {
            //Debug.Console(2, Parent, "Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            if (LocalQueue.IsEmpty) return;

            if (LocalQueue.Peek() is QueuedCommand)
            {
                // Expected response belongs to a child class
                var tempCommand = (QueuedCommand)LocalQueue.TryToDequeue();
                Debug.Console(1, Parent, "Command Dequeued. CommandQueue Size: {0} {1}", LocalQueue.Count, tempCommand.Command);
                tempCommand.ControlPoint.ParseGetMessage(tempCommand.AttributeCode, cmd);
            }
            else
            {
                LocalQueue.TryToDequeue();
            }

            Debug.Console(2, Parent, "Commmand queue {0}.", LocalQueue.IsEmpty ? "is empty" : "has entries");

            //if (LocalQueue.IsEmpty)
                //CommandQueueInProgress = false;
            //else
                SendNextQueuedCommand();
        }

        /// <summary>
        /// Adds a command from a child module to the queue
        /// </summary>
        /// <param name="commandToEnqueue">Command object from child module</param>
        public void EnqueueCommand(QueuedCommand commandToEnqueue)
        {
            //Debug.Console(2, Parent, "Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            LocalQueue.TryToEnqueue(commandToEnqueue);
            Debug.Console(1, Parent, "Command (QueuedCommand) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", commandToEnqueue.Command, LocalQueue.Count);
            //if (!CommandQueueInProgress)
                
            SendNextQueuedCommand();
        }

        /// <summary>
        /// Adds a raw string command to the queue
        /// </summary>
        /// <param name="command">String to enqueue</param>
        public void EnqueueCommand(string command)
        {
            //Debug.Console(2, Parent, "Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            LocalQueue.TryToEnqueue(command);
            Debug.Console(1, Parent, "Command (string) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, LocalQueue.Count);
            //if (!CommandQueueInProgress)
                SendNextQueuedCommand();
        }

        /// <summary>
        /// Sends the next queued command to the DSP
        /// </summary>
        public void SendNextQueuedCommand()
        {
            if (LocalQueue.IsEmpty)
            {
                //CommandQueueInProgress = false;
                return;
            }
            //Debug.Console(2, Parent, "Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            Debug.Console(2, Parent, "Attempting to send a queued commend");
            if (!Parent.Communication.IsConnected)
            {
                Debug.Console(2, Parent, "Unable to send queued command - Tesira Disconnected");
                return;
            }
            //CommandQueueInProgress = true;

            if (LocalQueue.Peek() is QueuedCommand)
            {
                var nextCommand = (QueuedCommand)LocalQueue.Peek();
                Parent.SendLine(nextCommand.Command);
            }

            else
            {
                var nextCommand = (string)LocalQueue.Peek();
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
        public string Command { get; set; }
        public string AttributeCode { get; set; }
        public ISubscribedComponent ControlPoint { get; set; }
    }

}