using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone
{

    /// <summary>
    /// Router Joinmap for Advanced Bridge - Meant for bridging the Router as a standalone device.
    /// Keeps the switcher map's joins; output N's route is OutputRoute + (N - 1), so output 1 is the old Index join.
    /// </summary>
    public class TesiraRouterJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        /// <summary>Outputs a standalone router can bridge.</summary>
        public const uint MaxOutputs = 64;

        [JoinName("Index")]
        public JoinDataComplete Index =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Route to the first output, set and feedback (same join as OutputRoute for output 1)",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Label")]
        public JoinDataComplete Label =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Router Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("RouteOrSource")]
        public JoinDataComplete RouteOrSource =
            new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Source List XSIG -or- Routed String",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Poll")]
        public JoinDataComplete Poll =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Poll every output's route",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OutputRoute")]
        public JoinDataComplete OutputRoute =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = MaxOutputs },
            new JoinMetadata
            {
                Description = "Router output: set the input routed to this output (0 clears) and feedback of the routed input",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("OutputRouteName")]
        public JoinDataComplete OutputRouteName =
            new JoinDataComplete(new JoinData { JoinNumber = 101, JoinSpan = MaxOutputs },
            new JoinMetadata
            {
                Description = "Router output: label of the routed input",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        public TesiraRouterJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof(TesiraRouterJoinMapAdvancedStandalone))
        {
        }

    }

}
