using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    /// <summary>
    /// Matrix Mixer Joinmap for Advanced Bridge - Meant for holistic DSP Object
    /// </summary>
    public class TesiraMatrixMixerJoinMapAdvanced : JoinMapBaseAdvanced
    {
        [JoinName("Toggle")]
        public JoinDataComplete Toggle = new JoinDataComplete(new JoinData() { JoinNumber = 2001, JoinSpan = 1 },
            new JoinMetadata() { Description = "State Toggle and Feedback", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("On")]
        public JoinDataComplete On = new JoinDataComplete(new JoinData() { JoinNumber = 2002, JoinSpan = 1 },
            new JoinMetadata() { Description = "State On and Feedback", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Off")]
        public JoinDataComplete Off = new JoinDataComplete(new JoinData() { JoinNumber = 2003, JoinSpan = 1 },
            new JoinMetadata() { Description = "State On and Feedback", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Label")]
        public JoinDataComplete Label = new JoinDataComplete(new JoinData() { JoinNumber = 2001, JoinSpan = 1 },
            new JoinMetadata() { Description = "State Label", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });


        public TesiraMatrixMixerJoinMapAdvanced(uint joinStart)
            : base(joinStart, typeof(TesiraMatrixMixerJoinMapAdvanced)) { }

    }

    /// <summary>
    /// Matrix Mixer Joinmap for Advanced Bridge - Meant for bridging the matrix mixer as a standalone device
    /// </summary>
    public class TesiraMatrixMixerJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        [JoinName("On")]
        public JoinDataComplete On = new JoinDataComplete(new JoinData() { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata() { Description = "State On and Feedback", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Off")]
        public JoinDataComplete Off = new JoinDataComplete(new JoinData() { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata() { Description = "State On and Feedback", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Toggle")]
        public JoinDataComplete Toggle = new JoinDataComplete(new JoinData() { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata() { Description = "State Toggle and Feedback", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Label")]
        public JoinDataComplete Label = new JoinDataComplete(new JoinData() { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata() { Description = "State Label", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });


        public TesiraMatrixMixerJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof(TesiraMatrixMixerJoinMapAdvancedStandalone)) { }

    }

}