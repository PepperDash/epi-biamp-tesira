using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    /// <summary>
    /// Device Joinmap for Advanced Bridge - Meant for holistic DSP Object
    /// </summary>
    public class TesiraDspDeviceJoinMapAdvanced : JoinMapBaseAdvanced
    {

        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(new JoinData() { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata() { Description = "Device Online", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });

        [JoinName("CommandPassThru")]
        public JoinDataComplete CommandPassThru = new JoinDataComplete(new JoinData() { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata() { Description = "Pass discrete commands directly to/from the device", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Serial });

        [JoinName("DirectPreset")]
        public JoinDataComplete DirectPreset = new JoinDataComplete(new JoinData() { JoinNumber = 100, JoinSpan = 1 },
            new JoinMetadata() { Description = "Directly Recall a Preset", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });

        public TesiraDspDeviceJoinMapAdvanced(uint joinStart)
            : base(joinStart, typeof(TesiraDspDeviceJoinMapAdvanced)) { }
    }

    /// <summary>
    /// Device Joinmap for Advanced Bridge - Meant for bridging the device information as a standalone device
    /// </summary>
    public class TesiraDspDeviceJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {

        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(new JoinData() { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata() { Description = "Device Online", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Name")]
        public JoinDataComplete Name = new JoinDataComplete(new JoinData() { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata() { Description = "Device Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("CommandPassThru")]
        public JoinDataComplete CommandPassThru = new JoinDataComplete(new JoinData() { JoinNumber = 2, JoinSpan = 2 },
            new JoinMetadata() { Description = "Pass discrete commands directly to/from the device", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.Serial });

        public TesiraDspDeviceJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof(TesiraDspDeviceJoinMapAdvancedStandalone)) { }
    }
}