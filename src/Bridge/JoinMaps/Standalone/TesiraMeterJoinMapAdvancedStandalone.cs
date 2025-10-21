using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone
{

    /// <summary>
    /// Meter Joinmap for Advanced Bridge - Meant for bridging the Meter as a standalone device
    /// </summary>
    public class TesiraMeterJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        [JoinName("Subscribe")]
        public JoinDataComplete Subscribe =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "High to Subscribe - Low to Unsubscribe",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Meter")]
        public JoinDataComplete Meter =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Meter Data",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Label")]
        public JoinDataComplete Label =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Meter Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public TesiraMeterJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof(TesiraMeterJoinMapAdvancedStandalone))
        {
        }

    }

}