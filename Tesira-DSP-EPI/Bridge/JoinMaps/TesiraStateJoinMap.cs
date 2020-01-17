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
        public uint Label { get; set; }


        public TesiraStateJoinMap(uint JoinStart) {

            //These Are Arrays - They all start at Join + IndexOfState

            //Digital
            Toggle = 1300;
            On = 1450;
            Off = 1600;

            //Analog

            //Serial
            Label = 1300;

            OffsetJoinNumbers(JoinStart);
        }



        public override void OffsetJoinNumbers(uint joinStart) {
            var joinOffset = joinStart - 1;
            var properties = this.GetType().GetCType().GetProperties().Where(o => o.PropertyType == typeof(uint)).ToList();
            foreach (var property in properties) {
                property.SetValue(this, (uint)property.GetValue(this, null) + joinOffset, null);
            }
        }
    }
}