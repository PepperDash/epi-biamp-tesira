using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Core;
using System.Collections.Generic;

#if SERIES4
using PepperDash.Core.Logging;
#endif

namespace Tesira_DSP_EPI
{
    public class TesiraFactory : EssentialsPluginDeviceFactory<TesiraDsp>
    {
        /// <summary>
        /// Factory for building new TesiraDsp Device
        /// </summary>
        public TesiraFactory()
        {
            MinimumEssentialsFrameworkVersion = "2.4.7";

            TypeNames = new List<string> { "tesira", "tesiraforte", "tesiraserver", "tesira-dsp", "tesiradsp" };
        }

        /// <summary>
        /// Build new TesiraDsp Device from Config
        /// </summary>
        /// <param name="dc">TesiraDsp Device Config</param>
        /// <returns></returns>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {

#if SERIES4
            Debug.LogDebug("Factory Attempting to create new Biamp Tesira Device");
#else
            Debug.Console(1, "Factory Attempting to create new Biamp Tesira Device");
#endif

            var comm = CommFactory.CreateCommForDevice(dc);

            return new TesiraDsp(dc.Key, dc.Name, comm, dc);
        }
    }

}