using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Queue
{
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
    public QueuedCommand(string command, string attributeCode, ISubscribedComponent controlPoint, bool bypassTxQueue = false)
    {
      Command = command;
      AttributeCode = attributeCode;
      ControlPoint = controlPoint;
      BypassTxQueue = bypassTxQueue;
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
  }

}