using PepperDash.Essentials.Core;


namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    public class TesiraExpanderJoinMap : JoinMapBaseAdvanced
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

        [JoinName("Hostname")]
        public JoinDataComplete Hostname =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device Hostname",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SerialNumber")]
        public JoinDataComplete SerialNumber =
            new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device Serial Number",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Firmware")]
        public JoinDataComplete Firmware =
            new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Device Firmware",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MacAddress")] public JoinDataComplete MacAddress =
            new JoinDataComplete(new JoinData {JoinNumber = 4, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Device Mac Address",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public TesiraExpanderJoinMap(uint joinStart)
            : base(joinStart, typeof(TesiraExpanderJoinMap))
        {
        }


    }
}