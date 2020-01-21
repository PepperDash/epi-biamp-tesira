using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;


namespace Tesira_DSP_EPI.Bridge.JoinMaps {
    public class TesiraPresetJoinMap : JoinMapBase {
        public uint PresetSelection { get; set; }
        public uint PresetName { get; set; }
        //public uint DirectPreset { get; set; }

        public TesiraPresetJoinMap(uint JoinStart) {
            //101 is directPreset call
            //PresetNames Feedback and PresetSelection are arrays = Number + PresetIndex


            //digital
            PresetSelection = 100;

            //Analog

            //Serial
            //DirectPreset = 100;
            PresetName = 100;

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