using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceInfo;

namespace Tesira_DSP_EPI
{
    public class TesiraExpanderMonitor : StatusMonitorBase
    {



        public TesiraExpanderMonitor(IKeyed parent, long warningTime, long errorTime)
            : base(parent, warningTime, errorTime)
        {

        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }

    }
}