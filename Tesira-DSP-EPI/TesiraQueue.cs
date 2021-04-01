using System;
using System.Data;
using PepperDash.Core;
using Crestron.SimplSharp;
using PepperDash.Essentials.Devices.Common.VideoCodec.Cisco;

namespace Tesira_DSP_EPI
{
    public class TesiraQueue
    {

        private CrestronQueue LocalQueue { get; set; }

        private TesiraDsp Parent { get; set; }

        //private CEvent DequeueEvent { get; set; }

        public bool CommandQueueInProgress { get; set; }
    
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
            try
            {
                Debug.Console(0, Parent, "[AdvanceQueue] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");
                Debug.Console(0, Parent, "[AdvanceQueue] - Incoming Response : \"{0}\".", response);


                if (LocalQueue.IsEmpty)
                {
                    Debug.Console(0, Parent, "[AdvanceQueue] - Command Queue is empty.");

                    return;
                }

                if (LocalQueue.Peek() is QueuedCommand)
                {
                    //Expected response belongs to a child class
                    var tempCommand = (QueuedCommand)LocalQueue.Dequeue();
                    Debug.Console(0, Parent, "[AdvanceQueue:InsidePeek] - Command Dequeued. CommandQueue Size: {0} : Outgoing Command - {1}", LocalQueue.Count, tempCommand.Command);
                    tempCommand.ControlPoint.ParseGetMessage(tempCommand.AttributeCode, response);
                }
                else
                {
                    LocalQueue.Dequeue();
                }

                Debug.Console(0, Parent, "[AdvanceQueue - Default] - Commmand queue {0}.", LocalQueue.IsEmpty ? "is empty" : "has entries");
               // DequeueEvent.Set();
                
                if (LocalQueue.IsEmpty)
                    CommandQueueInProgress = false;
                else
                {
                    Debug.Console(0, Parent, "[AdvanceQueue] - Triggering 'SendNextQueuedCommand'");

                    SendNextQueuedCommand();
                }
                
            }
            finally
            {
                Debug.Console(0, Parent, "[AdvanceQueue] - Reached Finally");
                //DequeueEvent.Set();
            }
        }

        /// <summary>
        /// Adds a command from a child module to the queue
        /// </summary>
        /// <param name="commandToEnqueue">Command object from child module</param>
        public void EnqueueCommand(QueuedCommand commandToEnqueue)
        {
            Debug.Console(0, Parent, "[EqueueCommand(QueuedCommand)] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            LocalQueue.Enqueue(commandToEnqueue);
            Debug.Console(0, Parent, "[EqueueCommand(QueuedCommand)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.", commandToEnqueue.Command, LocalQueue.Count);
            if (CommandQueueInProgress) return;
            Debug.Console(0, Parent, "[EnqueueCommand(QueuedCommand)] - Triggering 'SendNextQueuedCommand'");

            SendNextQueuedCommand();
        }

        /// <summary>
        /// Adds a raw string command to the queue
        /// </summary>
        /// <param name="command">String to enqueue</param>
        public void EnqueueCommand(string command)
        {
            Debug.Console(0, Parent, "[EqueueCommand(String)] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            LocalQueue.Enqueue(command);
            Debug.Console(0, Parent, "[EqueueCommand(String)] - Command Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, LocalQueue.Count);
            Debug.Console(0, Parent, "[EnqueueCommand(String)] - Triggering 'SendNextQueuedCommand'");

            SendNextQueuedCommand();
        }

        /// <summary>
        /// Sends the next queued command to the DSP
        /// </summary>
        public void SendNextQueuedCommand()
        {
           // DequeueEvent.Wait();
            Debug.Console(0, Parent, "[SendNextQueuedCommand] - Attempting to send a queued commend");

            if (LocalQueue.IsEmpty)
            {
                CommandQueueInProgress = false;
                return;
            }
            Debug.Console(0, Parent, "[SendNextQueuedCommand] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

            if (!Parent.Communication.IsConnected)
            {
                Debug.Console(0, Parent, "[SendNextQueuedCommand] - Unable to send queued command - Tesira Disconnected");
                return;
            }
            CommandQueueInProgress = true;

            if (LocalQueue.Peek() is QueuedCommand)
            {
                var nextCommand = (QueuedCommand)LocalQueue.Peek();
                Debug.Console(0, Parent, "[SendNextQueuedCommand(QueuedCommand)] - Sending Line - {0}", nextCommand.Command);
                Parent.SendLine(nextCommand.Command);
            }

            else
            {
                var nextCommand = (string)LocalQueue.Peek();
                Debug.Console(0, Parent, "[SendNextQueuedCommand(String)] - Sending Line - {0}", nextCommand);
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