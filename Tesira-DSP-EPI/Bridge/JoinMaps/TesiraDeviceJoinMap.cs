using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    public class TesiraDspDeviceJoinMap : JoinMapBase
    {
        public uint IsOnline { get; set; }
        public uint CommandPassthruTx { get; set; }
        public uint CommandPassthruRx { get; set; }
        public uint DirectPreset { get; set; }

        public TesiraDspDeviceJoinMap(uint JoinStart)
        {


            //Digital
            IsOnline = 1;
            DirectPreset = 100;

            //Serial
            CommandPassthruTx = 1;
            CommandPassthruRx = 1;

            OffsetJoinNumbers(JoinStart);

        }

        public override void OffsetJoinNumbers(uint joinStart)
        {
            var joinOffset = joinStart - 1;

            IsOnline += joinOffset;
            DirectPreset += joinOffset;
            CommandPassthruRx += joinOffset;
            CommandPassthruTx += joinOffset;
        }
    }
}