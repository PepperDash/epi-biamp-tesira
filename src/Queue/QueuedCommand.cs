using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
  /// <summary>
  /// Common priority levels for Tesira commands
  /// </summary>
  public enum CommandPriority
  {
    /// <summary>
    /// Lowest priority - Default for most commands
    /// </summary>
    Low = 1,

    /// <summary>
    /// Normal priority - Device info, level queries
    /// </summary>
    Normal = 5,

    /// <summary>
    /// High priority - Critical control commands
    /// </summary>
    High = 10,

    /// <summary>
    /// Critical priority - Error recovery, unsubscribe
    /// </summary>
    Critical = 20
  }
  /// <summary>
  /// Contains all data for a component command
  /// </summary>
  public class QueuedCommand
  {
    /// <summary>
    /// Constructor for QueuedCommand
    /// </summary>
    /// <param name="command">command to send</param>
    /// <param name="attributeCode">tesira attribute code for this command</param>
    /// <param name="controlPoint">control point object</param>
    /// <param name="bypassTxQueue">bypass TX Queue for this command</param>
    /// <param name="sendLineRaw">send command without appending delimiter</param>
    /// <param name="priority">command priority (higher values = higher priority)</param>
    public QueuedCommand(string command, string attributeCode, ISubscribedComponent controlPoint, bool bypassTxQueue = false, bool sendLineRaw = false, int priority = 1)
    {
      Command = command;
      AttributeCode = attributeCode;
      ControlPoint = controlPoint;
      BypassTxQueue = bypassTxQueue;
      SendLineRaw = sendLineRaw;
      Priority = priority;
    }

    /// <summary>
    /// Command String to send
    /// </summary>
    public readonly string Command;

    /// <summary>
    /// Attribute Code for the command
    /// </summary>
    public readonly string AttributeCode;

    /// <summary>
    /// Control Point associated with the command
    /// </summary>
    public readonly ISubscribedComponent ControlPoint;

    /// <summary>
    /// Bypass tx queue to handling pacing independently
    /// </summary>
    public readonly bool BypassTxQueue;

    /// <summary>
    /// Send command without appending delimiter
    /// </summary>
    public readonly bool SendLineRaw;

    /// <summary>
    /// Command priority (higher values = higher priority)
    /// </summary>
    public readonly int Priority;
  }

}