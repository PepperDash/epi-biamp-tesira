using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraFactory : EssentialsPluginDeviceFactory<TesiraDsp>
    {
        /// <summary>
        /// Factory for building new TesiraDsp Device
        /// </summary>
        public TesiraFactory()
        {
            MinimumEssentialsFrameworkVersion = "2.17.0";

            TypeNames = new List<string> { "tesira", "tesiraforte", "tesiraserver", "tesira-dsp", "tesiradsp" };
        }

        /// <summary>
        /// Build new TesiraDsp Device from Config
        /// </summary>
        /// <param name="dc">TesiraDsp Device Config</param>
        /// <returns></returns>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.LogDebug("Factory Attempting to create new Biamp Tesira Device");

            var comm = CommFactory.CreateCommForDevice(dc);

            return new TesiraDsp(dc.Key, dc.Name, comm, dc);
        }
    }

}