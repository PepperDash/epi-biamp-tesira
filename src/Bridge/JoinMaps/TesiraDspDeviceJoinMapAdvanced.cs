using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps
{
  /// <summary>
  /// Device Joinmap for Advanced Bridge - Meant for holistic DSP Object
  /// </summary>
  public class TesiraDspDeviceJoinMapAdvanced : JoinMapBaseAdvanced
  {

    [JoinName("IsOnline")]
    public JoinDataComplete IsOnline =
        new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Device Online",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("Resubscribe")]
    public JoinDataComplete Resubscribe =
        new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Trigger control resubscription",
              JoinCapabilities = eJoinCapabilities.FromSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("CommandPassThru")]
    public JoinDataComplete CommandPassThru =
        new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Pass discrete commands directly to/from the device",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Serial
            });

    public TesiraDspDeviceJoinMapAdvanced(uint joinStart)
        : base(joinStart, typeof(TesiraDspDeviceJoinMapAdvanced))
    {
    }
  }
}