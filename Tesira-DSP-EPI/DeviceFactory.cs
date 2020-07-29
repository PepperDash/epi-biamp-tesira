using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Core;
using System.Collections.Generic;

namespace Tesira_DSP_EPI
{
    public class TesiraFactory : EssentialsPluginDeviceFactory<TesiraDsp>
    {
        public TesiraFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.5.6";

            TypeNames = new List<string> { "tesira", "tesiraforte", "tesiraserver", "tesira-dsp", "tesiradsp" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Biamp Tesira Device");

            var comm = CommFactory.CreateCommForDevice(dc);

            return new TesiraDsp(dc.Key, dc.Name, comm, dc);
        }
    }

}