using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps {
    public class TesiraDspDeviceJoinMap : JoinMapBase {
        public uint IsOnline { get; set; }
        public uint CommandPassthruTx { get; set; }
        public uint CommandPassthruRx { get; set; }

        public TesiraDspDeviceJoinMap(uint JoinStart) {


            //Digital
            IsOnline = 1;

            //Serial
            CommandPassthruTx = 1;
            CommandPassthruRx = 1;

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