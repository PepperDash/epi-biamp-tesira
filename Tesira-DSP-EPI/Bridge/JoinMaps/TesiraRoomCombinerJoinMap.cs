using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;

namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    public class TesiraRoomCombinerJoinMap : JoinMapBase
    {
        public uint MuteToggle { get; set; }
        public uint MuteOn { get; set; }
        public uint MuteOff { get; set; }
        public uint Volume { get; set; }
        public uint MuteToggleFb { get; set; }
        public uint MuteOnFb { get; set; }
        public uint MuteOffFb { get; set; }
        public uint VolumeFb { get; set; }
        public uint Type { get; set; }
        public uint Label { get; set; }
        public uint VolumeUp { get; set; }
        public uint VolumeDown { get; set; }
        public uint Permissions { get; set; }
        public uint Visible { get; set; }
        public uint Group { get; set; }
        public uint GroupFb { get; set; }


        public TesiraRoomCombinerJoinMap(uint joinStart)
        {
            //Digital
            VolumeUp = 2201;
            VolumeDown = 2202;
            MuteToggle = 2203;
            MuteToggleFb = 2203;
            MuteOn = 2204;
            MuteOnFb = 2204;
            MuteOff = 2205;
            MuteOffFb = 2205;
            Visible = 2206;
            
            //String
            Label = 2201;

            //Analog
            Volume = 2201;
            VolumeFb = 2201;
            Type = 2202;
            Permissions = 2203; 
            Group = 2204;
            GroupFb = 2204;

            OffsetJoinNumbers(joinStart);

        }

        public override void OffsetJoinNumbers(uint joinStart)
        {
            var joinOffset = joinStart - 1;

            MuteToggle += joinOffset;
            MuteToggleFb += joinOffset;
            MuteOn += joinOffset;
            MuteOnFb += joinOffset;
            MuteOff += joinOffset;
            MuteOffFb += joinOffset;
            VolumeUp += joinOffset;
            VolumeDown += joinOffset;
            Visible += joinOffset;

            Volume += joinOffset;
            VolumeFb += joinOffset;
            Type += joinOffset;
            Permissions += joinOffset;
            Label += joinOffset;
            Group += joinOffset;
            GroupFb += joinOffset;
        }
    }
}