using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps
{
  /// <summary>
  /// Router per-output Joinmap for Advanced Bridge - Meant for holistic DSP Object.
  /// Each router gets <see cref="OutputsPerRouter"/> joins: output N of the router with bridgeIndex B
  /// is join number + ((B - 1) * OutputsPerRouter) + (N - 1).
  /// </summary>
  public class TesiraRouterJoinMapAdvanced : JoinMapBaseAdvanced
  {
    /// <summary>Outputs each router can bridge on the DSP-wide map.</summary>
    public const uint OutputsPerRouter = 64;

    [JoinName("OutputRoute")]
    public JoinDataComplete OutputRoute =
        new JoinDataComplete(new JoinData { JoinNumber = 4001, JoinSpan = OutputsPerRouter },
            new JoinMetadata
            {
              Description = "Router output: set the input routed to this output (0 clears) and feedback of the routed input",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Analog
            });

    [JoinName("OutputRouteName")]
    public JoinDataComplete OutputRouteName =
        new JoinDataComplete(new JoinData { JoinNumber = 4001, JoinSpan = OutputsPerRouter },
            new JoinMetadata
            {
              Description = "Router output: label of the routed input",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Serial
            });

    public TesiraRouterJoinMapAdvanced(uint joinStart)
        : base(joinStart, typeof(TesiraRouterJoinMapAdvanced))
    {
    }

  }

}
