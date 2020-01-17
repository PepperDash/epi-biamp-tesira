using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;


namespace Tesira_DSP_EPI.Bridge.JoinMaps {
    public class TesiraLevelJoinMap : JoinMapBase {
        
        public uint MuteToggle { get; set; }
        public uint MuteOn { get; set; }
        public uint MuteOff { get; set; }
        public uint Volume { get; set; }
        public uint Type { get; set; }
        public uint Name { get; set; }
        public uint VolumeUp { get; set; }
        public uint VolumeDown { get; set; }
        public uint Status { get; set; }
        public uint Permissions { get; set; }
        public uint Visible { get; set; }


        public TesiraLevelJoinMap(uint JoinStart) {

            //These Are Arrays - They all start at Join + IndexOfFader/Mute

            //Digital
            MuteToggle = 400;
            MuteOn = 600;
            MuteOff = 800;
            VolumeUp = 1000;
            VolumeDown = 1200;
            Visible = 200;

            //Analog
            Volume = 200;
            Type = 400;
            Status = 600;
            Permissions = 800;

            //Serial
            Name = 200;

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