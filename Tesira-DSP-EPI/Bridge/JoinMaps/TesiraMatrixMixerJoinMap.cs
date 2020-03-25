using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    public class TesiraMatrixMixerJoinMap : JoinMapBase
    {
        public uint Toggle { get; set; }
        public uint On { get; set; }
        public uint Off { get; set; }

        TesiraMatrixMixerJoinMap()
        {
            Toggle = 2001;
            On = 2002;
            Off = 2003;
        }

        public TesiraMatrixMixerJoinMap(uint joinStart)
            : this()
        {
            OffsetJoinNumbers(joinStart);
        }

        public override void OffsetJoinNumbers(uint joinStart)
        {
            var joinOffset = joinStart - 1;
            var properties = this.GetType().GetCType().GetProperties().Where(o => o.PropertyType == typeof(uint)).ToList();
            foreach (var property in properties)
            {
                property.SetValue(this, (uint)property.GetValue(this, null) + joinOffset, null);
            }
        }
    }
}