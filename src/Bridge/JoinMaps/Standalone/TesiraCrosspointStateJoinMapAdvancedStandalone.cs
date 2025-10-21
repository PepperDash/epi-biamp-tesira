using PepperDash.Essentials.Core;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone
{

    /// <summary>
    /// Matrix Mixer Joinmap for Advanced Bridge - Meant for bridging the matrix mixer as a standalone device
    /// </summary>
    public class TesiraCrosspointStateJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        [JoinName("On")]
        public JoinDataComplete On = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "State On and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Off")]
        public JoinDataComplete Off = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "State On and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Toggle")]
        public JoinDataComplete Toggle =
            new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "State Toggle and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Label")]
        public JoinDataComplete Label =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "State Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });


        public TesiraCrosspointStateJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof(TesiraCrosspointStateJoinMapAdvancedStandalone))
        {
        }

    }

}