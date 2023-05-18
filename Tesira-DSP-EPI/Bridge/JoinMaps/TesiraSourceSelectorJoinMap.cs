using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps {
    /// <summary>
    /// Meter Joinmap for Advanced Bridge - Meant for holistic DSP Object
    /// </summary>
    /// <summary>
    /// Meter Joinmap for Advanced Bridge - Meant for bridging the Meter as a standalone device
    /// </summary>
    public class TesiraSourceSelectorJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        [JoinName("Index")] public JoinDataComplete Index =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Source Selector Index Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Label")] public JoinDataComplete Label =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Source Selector Label",
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
                Description = "Poll Current Route",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });


        public TesiraSourceSelectorJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof(TesiraSourceSelectorJoinMapAdvancedStandalone))
        {
        }

    }

}