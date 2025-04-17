using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    /// <summary>
    /// Device Joinmap for Advanced Bridge - Meant for holistic DSP Object
    /// </summary>
    public class TesiraDspDeviceJoinMapAdvanced : JoinMapBaseAdvanced
    {

        [JoinName("IsOnline")] public JoinDataComplete IsOnline =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Resubscribe")] public JoinDataComplete Resubscribe =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Trigger control resubscription",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("CommandPassThru")] public JoinDataComplete CommandPassThru =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Pass discrete commands directly to/from the device",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        public TesiraDspDeviceJoinMapAdvanced(uint joinStart)
            : base(joinStart, typeof (TesiraDspDeviceJoinMapAdvanced))
        {
        }
    }

    /// <summary>
    /// Device Joinmap for Advanced Bridge - Meant for bridging the device information as a standalone device
    /// </summary>
    public class TesiraDspDeviceJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {

        [JoinName("IsOnline")] public JoinDataComplete IsOnline =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Device Online",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Resubscribe")] public JoinDataComplete Resubscribe =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Trigger control resubscription",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Name")] public JoinDataComplete Name =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Device Name",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("CommandPassThru")] public JoinDataComplete CommandPassThru =
            new JoinDataComplete(new JoinData {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Pass discrete commands directly to/from the device",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("SerialNumber")]
        public JoinDataComplete SerialNumber =
            new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Serial Number of the Device",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Firmware")]
        public JoinDataComplete Firmware =
            new JoinDataComplete(new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Firmware of the Device",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Hostname")]
        public JoinDataComplete Hostname =
            new JoinDataComplete(new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Hostname of the Device",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("IpAddress")]
        public JoinDataComplete IpAddress =
            new JoinDataComplete(new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "IP Address of the Device",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MacAddress")]
        public JoinDataComplete MacAddress =
            new JoinDataComplete(new JoinData { JoinNumber = 7, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Mac Address of the Device",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });



        public TesiraDspDeviceJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof (TesiraDspDeviceJoinMapAdvancedStandalone))
        {
        }
    }
}