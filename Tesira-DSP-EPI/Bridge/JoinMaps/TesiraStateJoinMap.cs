using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps {
    public class TesiraStateJoinMap : JoinMapBase {

        public uint Toggle { get; set; }
        public uint On { get; set; }
        public uint Off { get; set; }
        public uint ToggleFb { get; set; }
        public uint OnFb { get; set; }
        public uint OffFb { get; set; }
        public uint Label { get; set; }


        public TesiraStateJoinMap(uint JoinStart) {

            //These Are Arrays - They all start at Join + IndexOfState

            //Digital
            Toggle = 1300;
            On = 1450;
            Off = 1600;
            ToggleFb = 1300;
            OnFb = 1450;
            OffFb = 1600;

            //Analog

            //Serial
            Label = 1300;

            OffsetJoinNumbers(JoinStart);
        }



        public override void OffsetJoinNumbers(uint joinStart) {
            var joinOffset = joinStart - 1;

            Toggle += joinStart; ;
            On += joinStart;
            Off += joinOffset;
            ToggleFb += joinOffset;
            OnFb += joinOffset;
            OffFb += joinOffset;
        }
    }
}