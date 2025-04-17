using PepperDash.Essentials.Core;


namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    /// <summary>
    /// Meter Joinmap for Advanced Bridge - Meant for holistic DSP Object
    /// </summary>
    public class TesiraPresetJoinMapAdvanced : JoinMapBaseAdvanced
    {
        [JoinName("PresetSelection")] public JoinDataComplete PresetSelection =
            new JoinDataComplete(new JoinData {JoinNumber = 100, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Recall Preset Explicitly by Configured Index",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PresetName")] public JoinDataComplete PresetName =
            new JoinDataComplete(new JoinData {JoinNumber = 100, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Recall Preset by name",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.AnalogSerial
                });

        [JoinName("PresetNameFeedback")] public JoinDataComplete PresetNameFeedback =
            new JoinDataComplete(new JoinData {JoinNumber = 100, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Preset Labels as configured for explicit preset recall",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public TesiraPresetJoinMapAdvanced(uint joinStart)
            : base(joinStart, typeof (TesiraPresetJoinMapAdvanced))
        {
        }

    }

    /// <summary>
    /// Meter Joinmap for Advanced Bridge - Meant for bridging the Presets as a standalone device in concert with DeviceInfo
    /// </summary>
    public class TesiraPresetJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        [JoinName("PresetSelection")] public JoinDataComplete PresetSelection =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Recall Preset Explicitly by Configured Index",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });
        [JoinName("PresetValidFeedback")]
        public JoinDataComplete PresetValidFeedback =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Preset Preset and Configured",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PresetName")] public JoinDataComplete PresetName =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Recall Preset by name",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.AnalogSerial
                });

        [JoinName("PresetNameFeedback")] public JoinDataComplete PresetNameFeedback =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Preset Labels as configured for explicit preset recall",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public TesiraPresetJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof (TesiraPresetJoinMapAdvancedStandalone))
        {
        }

    }

}