using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using PepperDash.Essentials.Core;


namespace Tesira_DSP_EPI.Bridge.JoinMaps {
    public class TesiraDialerJoinMap : JoinMapBase {

        public uint DialString { get; set; }
        public uint DialStringFb { get; set; }
        public uint Label { get; set; }
        public uint CallerIDNumberFB { get; set; }
        public uint CallerIDNameFB { get; set; }

        //Digitals
        public uint IncomingCall { get; set; }//
        public uint Answer { get; set; }//
        public uint EndCall { get; set; }//
        public uint Keypad0 { get; set; }//
        public uint Keypad1 { get; set; }//
        public uint Keypad2 { get; set; }//
        public uint Keypad3 { get; set; }//
        public uint Keypad4 { get; set; }//
        public uint Keypad5 { get; set; }//
        public uint Keypad6 { get; set; }//
        public uint Keypad7 { get; set; }//
        public uint Keypad8 { get; set; }//
        public uint Keypad9 { get; set; }//
        public uint KeypadStar { get; set; }//
        public uint KeypadPound { get; set; }//
        public uint KeypadClear { get; set; }//
        public uint KeypadBackspace { get; set; }//
        public uint Dial { get; set; }//
        public uint DialFb { get; set; }//
        public uint AutoAnswerOn { get; set; }//
        public uint AutoAnswerOff { get; set; }//
        public uint AutoAnswerToggle { get; set; }
        public uint OnHook { get; set; }
        public uint OffHookFb { get; set; }
        public uint DoNotDisturbToggle { get; set; }//
        public uint DoNotDisturbOn { get; set; }//
        public uint DoNotDisturbOff { get; set; }//
        public uint DoNotDisturbToggleFb { get; set; }
        public uint DoNotDisturbOnFb { get; set; }
        public uint DoNotDisturbOffFb { get; set; }
        public uint AutoAnswerToggleFb { get; set; }
        public uint AutoAnswerOnFb { get; set; }
        public uint AutoAnswerOffFb { get; set; }
        public uint OffHook { get; set; }
        public uint OnHookFb { get; set; }

        //Analogs
        public uint CallState { get; set; }
        




        public TesiraDialerJoinMap(uint JoinStart) {
            //Is an array - all members start at value + 1
            //Each additional line appearance starts at Value + 1 + (50 * (LineAppearance - 1))

            //Digital
            IncomingCall = 3100;
            Answer = 3106;
            EndCall = 3107;
            Keypad0 = 3110;
            Keypad1 = 3111;
            Keypad2 = 3112;
            Keypad3 = 3113;
            Keypad4 = 3114;
            Keypad5 = 3115;
            Keypad6 = 3116;
            Keypad7 = 3117;
            Keypad8 = 3118;
            Keypad9 = 3119;
            KeypadStar = 3120;
            KeypadPound = 3121;
            KeypadClear = 3122;
            KeypadBackspace = 3123;
            Dial = 3124;
            DialFb = 3124;
            AutoAnswerOn = 3125;
            AutoAnswerOff = 3126;
            AutoAnswerToggle = 3127;
            AutoAnswerOnFb = 3125;
            AutoAnswerOffFb = 3126;
            AutoAnswerToggleFb = 3127;
            OnHook = 3129;
            OnHookFb = 3129;
            OffHook = 3130;
            OffHookFb = 3130;
            DoNotDisturbToggle = 3132;
            DoNotDisturbToggleFb = 3132;
            DoNotDisturbOn = 3133;
            DoNotDisturbOnFb = 3133;
            DoNotDisturbOff = 3134;
            DoNotDisturbOffFb = 3134;


            


            //Analog
            CallState = 3100;

            //String
            DialString = 3100;
            DialStringFb = 3100;
            Label = 3101;
            CallerIDNumberFB = 3104;
            CallerIDNameFB = 3105;

            OffsetJoinNumbers(JoinStart);
        }

        public  override void OffsetJoinNumbers(uint joinStart) {
            var joinOffset = joinStart - 1;
            var properties = this.GetType().GetCType().GetProperties().Where(o => o.PropertyType == typeof(uint)).ToList();
            foreach (var property in properties) {
                property.SetValue(this, (uint)property.GetValue(this, null) + joinOffset, null);
            }
        }
    }
}