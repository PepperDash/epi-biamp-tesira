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
                    LocalQueue?.Count ?? 0, LastDequeued.Command);
                
                if (LastDequeued.ControlPoint != null && !string.IsNullOrEmpty(LastDequeued.AttributeCode))
                {
                    LastDequeued.ControlPoint.ParseGetMessage(LastDequeued.AttributeCode, response);
                }
                else
                {
                    Debug.Console(2, Parent, "[AdvanceQueue] - ControlPoint or AttributeCode is null, skipping parse");
                }
                LastDequeued = null;
            }

            if (LocalQueue == null)
            {
                Debug.Console(2, Parent, "[AdvanceQueue] - LocalQueue is null");
                CommandQueueInProgress = false;
                return;
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
            if (commandToEnqueue == null)
            {
                Debug.Console(2, Parent, "[EqueueCommand(QueuedCommand)] - Command is null, ignoring");
                return;
            }

            if (string.IsNullOrEmpty(commandToEnqueue.Command))
            {
                Debug.Console(2, Parent, "[EqueueCommand(QueuedCommand)] - Command.Command is null or empty, ignoring");
                return;
            }

            if (LocalQueue == null)
            {
                Debug.Console(2, Parent, "[EqueueCommand(QueuedCommand)] - LocalQueue is null, ignoring");
                return;
            }

            if (Parent == null)
            {
                Debug.Console(2, "[EqueueCommand(QueuedCommand)] - Parent is null, ignoring");
                return;
            }

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
            if (string.IsNullOrEmpty(command))
            {
                Debug.Console(2, Parent, "[EqueueCommand(String)] - Command is null or empty, ignoring");
                return;
            }

            if (LocalQueue == null)
            {
                Debug.Console(2, Parent, "[EqueueCommand(String)] - LocalQueue is null, ignoring");
                return;
            }

            if (Parent == null)
            {
                Debug.Console(2, "[EqueueCommand(String)] - Parent is null, ignoring");
                return;
            }

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
            try
            {
                Debug.Console(2, Parent, "[SendNextQueuedCommand] - Attempting to send a queued commend");

                if (Parent == null)
                {
                    Debug.Console(2, "[SendNextQueuedCommand] - Parent is null, cannot proceed");
                    CommandQueueInProgress = false;
                    return;
                }

                if (LocalQueue == null)
                {
                    Debug.Console(2, Parent, "[SendNextQueuedCommand] - LocalQueue is null");
                    CommandQueueInProgress = false;
                    return;
                }

                if (LocalQueue.IsEmpty)
                {
                    Debug.Console(2, Parent, "[SendNextQueuedCommand] - Queue is empty");
                    CommandQueueInProgress = false;
                    return;
                }
                
                Debug.Console(2, Parent, "[SendNextQueuedCommand] - Command Queue {0} in progress.", CommandQueueInProgress ? "is" : "is not");

                if (Parent.Communication == null)
                {
                    Debug.Console(2, Parent, "[SendNextQueuedCommand] - Parent.Communication is null");
                    CommandQueueInProgress = false;
                    return;
                }

                if (!Parent.Communication.IsConnected)
                {
                    Debug.Console(2, Parent, "[SendNextQueuedCommand] - Unable to send queued command - Tesira Disconnected");
                    CommandQueueInProgress = false;
                    return;
                }
                
                CommandQueueInProgress = true;

                var peekResult = LocalQueue.Peek();
                if (peekResult == null)
                {
                    Debug.Console(2, Parent, "[SendNextQueuedCommand] - Peek returned null, advancing queue");
                    LocalQueue.Dequeue(); // Remove the null item
                    CommandQueueInProgress = false;
                    return;
                }

                if (peekResult is QueuedCommand queuedCommand)
                {
                    LastDequeued = (QueuedCommand)LocalQueue.Dequeue();
                    if (LastDequeued?.Command != null)
                    {
                        Debug.Console(2, Parent, "[SendNextQueuedCommand(QueuedCommand)] - Sending Line - {0}", LastDequeued.Command);
                        try
                        {
                            Parent.SendLine(LastDequeued.Command);
                        }
                        catch (Exception sendEx)
                        {
                            Debug.Console(2, Parent, "[SendNextQueuedCommand] - Exception in Parent.SendLine: {0}", sendEx.Message);
                            CommandQueueInProgress = false;
                        }
                    }
                    else
                    {
                        Debug.Console(2, Parent, "[SendNextQueuedCommand(QueuedCommand)] - Command is null, skipping");
                        CommandQueueInProgress = false;
                        return; // Add missing return statement
                    }
                }
                else if (peekResult is string)
                {
                    LastDequeued = null;
                    var nextCommand = (string)LocalQueue.Dequeue();
                    if (!string.IsNullOrEmpty(nextCommand))
                    {
                        Debug.Console(2, Parent, "[SendNextQueuedCommand(String)] - Sending Line - {0}", nextCommand);
                        try
                        {
                            Parent.SendLine(nextCommand);
                        }
                        catch (Exception sendEx)
                        {
                            Debug.Console(2, Parent, "[SendNextQueuedCommand] - Exception in Parent.SendLine: {0}", sendEx.Message);
                            CommandQueueInProgress = false;
                        }
                    }
                    else
                    {
                        Debug.Console(2, Parent, "[SendNextQueuedCommand(String)] - Command is null or empty, skipping");
                        CommandQueueInProgress = false;
                        return; // Add missing return statement
                    }
                }
                else
                {
                    Debug.Console(2, Parent, "[SendNextQueuedCommand] - Unknown queue item type: {0}", peekResult.GetType().Name);
                    LocalQueue.Dequeue(); // Remove the unknown item
                    CommandQueueInProgress = false;
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, Parent, "[SendNextQueuedCommand] - Critical Exception: {0}", ex.Message);
                Debug.Console(0, Parent, "[SendNextQueuedCommand] - StackTrace: {0}", ex.StackTrace);
                CommandQueueInProgress = false;
                
                // Try to clear problematic queue state
                try
                {
                    if (LocalQueue != null && !LocalQueue.IsEmpty)
                    {
                        Debug.Console(0, Parent, "[SendNextQueuedCommand] - Attempting to clear problematic queue item");
                        LocalQueue.Dequeue();
                    }
                }
                catch (Exception clearEx)
                {
                    Debug.Console(0, Parent, "[SendNextQueuedCommand] - Exception while clearing queue: {0}", clearEx.Message);
                }
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