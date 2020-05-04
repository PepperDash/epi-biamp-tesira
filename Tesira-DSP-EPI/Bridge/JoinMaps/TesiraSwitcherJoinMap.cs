﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;


namespace Tesira_DSP_EPI.Bridge.JoinMaps {
    public class TesiraSwitcherJoinMap : JoinMapBase {

        public uint SourceSelectorIndex { get; set; }
        public uint SourceSelectorIndexFb { get; set; }
        public uint SourceSelectorLabel { get; set; }

        public TesiraSwitcherJoinMap(uint JoinStart) {

            //Digital


            //Analog
            SourceSelectorIndex = 150;
            SourceSelectorIndexFb = 150;


            //String
            SourceSelectorLabel = 150;

            OffsetJoinNumbers(JoinStart);
        }




        public override void OffsetJoinNumbers(uint joinStart) {
            var joinOffset = joinStart - 1;

            SourceSelectorIndex += joinOffset;
            SourceSelectorIndexFb += joinOffset;
            SourceSelectorLabel += joinOffset;
        }
    }
}