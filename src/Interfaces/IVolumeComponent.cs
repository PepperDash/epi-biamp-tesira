namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Interfaces
{
    public interface IVolumeComponent
    {
        void GetMinLevel();
        void GetMaxLevel();
        void GetVolume();
        double MinLevel { get; }
        double MaxLevel { get; }
    }
}