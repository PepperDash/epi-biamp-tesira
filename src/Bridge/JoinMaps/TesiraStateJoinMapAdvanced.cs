using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps
{
  /// <summary>
  /// State Joinmap for Advanced Bridge - Meant for holistic DSP Object
  /// </summary>
  public class TesiraStateJoinMapAdvanced : JoinMapBaseAdvanced
  {
    [JoinName("Toggle")]
    public JoinDataComplete Toggle =
        new JoinDataComplete(new JoinData { JoinNumber = 1300, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "State Toggle and Feedback",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("On")]
    public JoinDataComplete On =
        new JoinDataComplete(new JoinData { JoinNumber = 1450, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "State On and Feedback",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("Off")]
    public JoinDataComplete Off =
        new JoinDataComplete(new JoinData { JoinNumber = 1600, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "State On and Feedback",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("Label")]
    public JoinDataComplete Label =
        new JoinDataComplete(new JoinData { JoinNumber = 1300, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "State Label",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Serial
            });


    public TesiraStateJoinMapAdvanced(uint joinStart)
        : base(joinStart, typeof(TesiraStateJoinMapAdvanced))
    {
    }

  }

}