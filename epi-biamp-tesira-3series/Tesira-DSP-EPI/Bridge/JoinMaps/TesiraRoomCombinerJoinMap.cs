using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    /// <summary>
    /// Fader Joinmap for Advanced Bridge - Meant for holistic DSP Object
    /// </summary>
    public class TesiraRoomCombinerJoinMapAdvanced : JoinMapBaseAdvanced
    {
        [JoinName("MuteToggle")] public JoinDataComplete MuteToggle =
            new JoinDataComplete(new JoinData {JoinNumber = 2203, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Mute Toggle and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("MuteOn")] public JoinDataComplete MuteOn =
            new JoinDataComplete(new JoinData {JoinNumber = 2204, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Mute On and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("MuteOff")] public JoinDataComplete MuteOff =
            new JoinDataComplete(new JoinData {JoinNumber = 2205, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Mute Off and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("VolumeUp")] public JoinDataComplete VolumeUp =
            new JoinDataComplete(new JoinData {JoinNumber = 2201, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Level Increment",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("VolumeDown")] public JoinDataComplete VolumeDown =
            new JoinDataComplete(new JoinData {JoinNumber = 2202, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Level Decrement",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Visible")] public JoinDataComplete Visible =
            new JoinDataComplete(new JoinData {JoinNumber = 2206, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Visible and In Use",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Volume")] public JoinDataComplete Volume =
            new JoinDataComplete(new JoinData {JoinNumber = 2201, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Level Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Type")] public JoinDataComplete Type =
            new JoinDataComplete(new JoinData {JoinNumber = 2202, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Type [Speaker / Microphone]",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Group")] public JoinDataComplete Group =
            new JoinDataComplete(new JoinData {JoinNumber = 2204, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Control Data [Mic / Speaker]",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Permissions")] public JoinDataComplete Permissions =
            new JoinDataComplete(new JoinData {JoinNumber = 2203, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Permissions",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Label")] public JoinDataComplete Label =
            new JoinDataComplete(new JoinData {JoinNumber = 2201, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public TesiraRoomCombinerJoinMapAdvanced(uint joinStart)
            : base(joinStart, typeof (TesiraRoomCombinerJoinMapAdvanced))
        {
        }

    }

    /// <summary>
    /// Fader Joinmap for Advanced Bridge - Meant for bridging the fader as a standalone device
    /// </summary>
    public class TesiraRoomCombinerJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        [JoinName("VolumeUp")] public JoinDataComplete VolumeUp =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Level Increment",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("VolumeDown")] public JoinDataComplete VolumeDown =
            new JoinDataComplete(new JoinData {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Level Decrement",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("MuteOn")] public JoinDataComplete MuteOn =
            new JoinDataComplete(new JoinData {JoinNumber = 3, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Mute On and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("MuteOff")] public JoinDataComplete MuteOff =
            new JoinDataComplete(new JoinData {JoinNumber = 4, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Mute Off and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("MuteToggle")] public JoinDataComplete MuteToggle =
            new JoinDataComplete(new JoinData {JoinNumber = 5, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Mute Toggle and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });


        [JoinName("Visible")] public JoinDataComplete Visible =
            new JoinDataComplete(new JoinData {JoinNumber = 6, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Visible and In Use",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Volume")] public JoinDataComplete Volume =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Level Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Type")] public JoinDataComplete Type =
            new JoinDataComplete(new JoinData {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Type [Speaker / Microphone]",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Permissions")] public JoinDataComplete Permissions =
            new JoinDataComplete(new JoinData {JoinNumber = 3, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Permissions",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Group")] public JoinDataComplete Group =
            new JoinDataComplete(new JoinData {JoinNumber = 4, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Control Data [Mic / Speaker]",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("Label")] public JoinDataComplete Label =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Fader Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public TesiraRoomCombinerJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof (TesiraRoomCombinerJoinMapAdvancedStandalone))
        {
        }

    }

}