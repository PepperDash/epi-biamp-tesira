using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps
{
  /// <summary>
  /// Router per-output Joinmap for Advanced Bridge - Meant for holistic DSP Object.
  /// Each router gets <see cref="OutputsPerRouter"/> joins: output N of the router with bridgeIndex B
  /// is join number + ((B - 1) * OutputsPerRouter) + (N - 1). The block sits between the room combiners
  /// (2201+) and the dialers (3100+), below the EISC's 4000-join limit: routers with bridgeIndex 1-7.
  /// </summary>
  public class TesiraRouterJoinMapAdvanced : JoinMapBaseAdvanced
  {
    /// <summary>Outputs each router can bridge on the DSP-wide map.</summary>
    public const uint OutputsPerRouter = 64;

    /// <summary>Routers that fit before the dialer joins (3100+).</summary>
    public const uint MaxRouters = 7;

    [JoinName("OutputRoute")]
    public JoinDataComplete OutputRoute =
        new JoinDataComplete(new JoinData { JoinNumber = 2601, JoinSpan = OutputsPerRouter },
            new JoinMetadata
            {
              Description = "Router output: set the input routed to this output (0 clears) and feedback of the routed input",
              JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
              JoinType = eJoinType.Analog
            });

    [JoinName("OutputRouteName")]
    public JoinDataComplete OutputRouteName =
        new JoinDataComplete(new JoinData { JoinNumber = 2601, JoinSpan = OutputsPerRouter },
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
