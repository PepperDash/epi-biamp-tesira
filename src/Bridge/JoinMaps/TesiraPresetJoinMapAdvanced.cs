using PepperDash.Essentials.Core;


namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps
{
  /// <summary>
  /// Meter Joinmap for Advanced Bridge - Meant for holistic DSP Object
  /// </summary>
  public class TesiraPresetJoinMapAdvanced : JoinMapBaseAdvanced
  {
    [JoinName("PresetSelection")]
    public JoinDataComplete PresetSelection =
        new JoinDataComplete(new JoinData { JoinNumber = 100, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Recall Preset Explicitly by Configured Index",
              JoinCapabilities = eJoinCapabilities.FromSIMPL,
              JoinType = eJoinType.Digital
            });

    [JoinName("PresetName")]
    public JoinDataComplete PresetName =
        new JoinDataComplete(new JoinData { JoinNumber = 100, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Recall Preset by name",
              JoinCapabilities = eJoinCapabilities.FromSIMPL,
              JoinType = eJoinType.AnalogSerial
            });

    [JoinName("PresetNameFeedback")]
    public JoinDataComplete PresetNameFeedback =
        new JoinDataComplete(new JoinData { JoinNumber = 100, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Preset Labels as configured for explicit preset recall",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Serial
            });

    [JoinName("PresetSavedFeedback")]
    public JoinDataComplete PresetSavedFeedback =
        new JoinDataComplete(new JoinData { JoinNumber = 100, JoinSpan = 1 },
            new JoinMetadata
            {
              Description = "Preset Saved Indication - Pulses high for 2 seconds when preset is saved",
              JoinCapabilities = eJoinCapabilities.ToSIMPL,
              JoinType = eJoinType.Digital
            });

    public TesiraPresetJoinMapAdvanced(uint joinStart)
        : base(joinStart, typeof(TesiraPresetJoinMapAdvanced))
    {
    }

  }

}