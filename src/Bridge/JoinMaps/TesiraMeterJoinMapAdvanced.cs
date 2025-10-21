using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps
{
  /// <summary>
  /// Meter Joinmap for Advanced Bridge - Meant for holistic DSP Object
  /// </summary>
  public class TesiraMeterJoinMapAdvanced : JoinMapBaseAdvanced
  {
    [JoinName("Subscribe")]
    public JoinDataComplete Subscribe =
        new JoinDataComplete(new JoinData { JoinNumber = 3501, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "High to Subscribe - Low to Unsubscribe",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("Meter")]
    public JoinDataComplete Meter =
        new JoinDataComplete(new JoinData { JoinNumber = 3501, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Meter Data",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Analog
            });

    [JoinName("Label")]
    public JoinDataComplete Label =
        new JoinDataComplete(new JoinData { JoinNumber = 3501, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Meter Label",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Serial
            });


    public TesiraMeterJoinMapAdvanced(uint joinStart)
        : base(joinStart, typeof(TesiraMeterJoinMapAdvanced))
    {
    }

  }

}