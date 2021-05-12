using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI
{
    public class TesiraPreset : TesiraDspPresets, IDspPreset
    {
        public string Name { get; private set; }
        public int Index { get; private set; }

        public TesiraDspPresets PresetData { get; private set; }

        public TesiraPreset(TesiraDspPresets data)
        {
            PresetData = data;
            Name = data.Label;
            Index = data.PresetIndex;
        }
    }

    
}