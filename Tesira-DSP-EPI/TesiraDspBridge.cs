using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common;
using PepperDash.Essentials.Bridges;
using Tesira_DSP_EPI;
using Newtonsoft.Json;
using Crestron.SimplSharp.Reflection;

namespace Tesira_DSP_EPI {
    public static class TesiraDspDeviceApiExtensions {
        public static void LinkToApiExt(this TesiraDsp DspDevice, BasicTriList trilist, uint joinStart, string joinMapKey) {


            TesiraDspDeviceJoinMap joinMap = new TesiraDspDeviceJoinMap();

            var JoinMapSerialized = JoinMapHelper.GetJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(JoinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraDspDeviceJoinMap>(JoinMapSerialized);

            joinMap.OffsetJoinNumbers(joinStart);
            Debug.Console(1, DspDevice, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            ushort x = 1;
            var comm = DspDevice as ICommunicationMonitor;
            DspDevice.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline]);
            trilist.SetStringSigAction(joinMap.Address, (s) => { DspDevice.SetIpAddress(s); });

            Debug.Console(2, "There are {0} Level Control Points", DspDevice.LevelControlPoints.Count());
            foreach (var channel in DspDevice.LevelControlPoints) {
                //var TesiraChannel = channel.Value as Tesira.DSP.EPI.TesiraDspLevelControl;
                Debug.Console(2, "TesiraChannel {0} connect", x);

                var genericChannel = channel.Value as IBasicVolumeWithFeedback;
                if (channel.Value.Enabled) {
                    Debug.Console(2, "TesiraChannel {0} Is Enabled", x);
                    trilist.StringInput[joinMap.ChannelName + x].StringValue = channel.Value.LevelCustomName;
                    trilist.UShortInput[joinMap.ChannelType + x].UShortValue = (ushort)channel.Value.Type;
                    trilist.BooleanInput[joinMap.ChannelVisible + x].BoolValue = true;

                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.ChannelMuteToggle + x]);
                    genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.ChannelVolume + x]);

                    trilist.SetSigTrueAction(joinMap.ChannelMuteToggle + x, () => genericChannel.MuteToggle());
                    trilist.SetSigTrueAction(joinMap.ChannelMuteOn + x, () => genericChannel.MuteOn());
                    trilist.SetSigTrueAction(joinMap.ChannelMuteOff + x, () => genericChannel.MuteOff());

                    trilist.SetBoolSigAction(joinMap.ChannelVolumeUp + x, b => genericChannel.VolumeUp(b));
                    trilist.SetBoolSigAction(joinMap.ChannelVolumeDown + x, b => genericChannel.VolumeDown(b));

                    trilist.SetUShortSigAction(joinMap.ChannelVolume + x, u => { if (u > 0) { genericChannel.SetVolume(u); } });
                    //channel.Value.DoPoll();
                }
                x++;
            }


            //Presets 
            x = 0;
            trilist.SetStringSigAction(joinMap.Presets, s => DspDevice.RunPreset(s));
            foreach (var preset in DspDevice.PresetList) {
                var temp = x;
                trilist.StringInput[joinMap.Presets + temp + 1].StringValue = preset.label;
                trilist.SetSigTrueAction(joinMap.Presets + temp + 1, () => DspDevice.RunPresetNumber(temp));
                x++;
            }

            // VoIP Dialer
            
            uint lineOffset = 0;
            foreach (var line in DspDevice.Dialers) {
                var dialer = line;
                var dialerLineOffset = lineOffset;
                Debug.Console(2, "AddingDialerBRidge {0} {1} Offset", dialer.Key, dialerLineOffset);
                trilist.SetSigTrueAction((joinMap.Keypad0 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num0));
                trilist.SetSigTrueAction((joinMap.Keypad1 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num1));
                trilist.SetSigTrueAction((joinMap.Keypad2 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num2));
                trilist.SetSigTrueAction((joinMap.Keypad3 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num3));
                trilist.SetSigTrueAction((joinMap.Keypad4 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num4));
                trilist.SetSigTrueAction((joinMap.Keypad5 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num5));
                trilist.SetSigTrueAction((joinMap.Keypad6 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num6));
                trilist.SetSigTrueAction((joinMap.Keypad7 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num7));
                trilist.SetSigTrueAction((joinMap.Keypad8 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num8));
                trilist.SetSigTrueAction((joinMap.Keypad9 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num9));
                trilist.SetSigTrueAction((joinMap.KeypadStar + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Star));
                trilist.SetSigTrueAction((joinMap.KeypadPound + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Pound));
                trilist.SetSigTrueAction((joinMap.KeypadClear + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Clear));
                trilist.SetSigTrueAction((joinMap.KeypadBackspace + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Backspace));

                trilist.SetSigTrueAction(joinMap.Dial + dialerLineOffset, () => dialer.Value.Dial());
                trilist.SetSigTrueAction(joinMap.DoNotDisturbToggle + dialerLineOffset, () => dialer.Value.DoNotDisturbToggle());
                trilist.SetSigTrueAction(joinMap.DoNotDisturbOn + dialerLineOffset, () => dialer.Value.DoNotDisturbOn());
                trilist.SetSigTrueAction(joinMap.DoNotDisturbOff + dialerLineOffset, () => dialer.Value.DoNotDisturbOff());
                trilist.SetSigTrueAction(joinMap.AutoAnswerToggle + dialerLineOffset, () => dialer.Value.AutoAnswerToggle());
                trilist.SetSigTrueAction(joinMap.AutoAnswerOn + dialerLineOffset, () => dialer.Value.AutoAnswerOn());
                trilist.SetSigTrueAction(joinMap.AutoAnswerOff + dialerLineOffset, () => dialer.Value.AutoAnswerOff());
                trilist.SetSigTrueAction(joinMap.Answer + dialerLineOffset, () => dialer.Value.Answer());
                trilist.SetSigTrueAction(joinMap.EndCall + dialerLineOffset, () => dialer.Value.EndAllCalls());
                trilist.SetSigTrueAction(joinMap.OnHook + dialerLineOffset, () => dialer.Value.OnHook());
                trilist.SetSigTrueAction(joinMap.OffHook + dialerLineOffset, () => dialer.Value.OffHook());

                trilist.SetStringSigAction(joinMap.DialStringCmd + dialerLineOffset, s  => dialer.Value.SetDialString(s));

                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbToggle + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOn + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOff + dialerLineOffset]);

                dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerToggle + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerOn + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoAnswerOff + dialerLineOffset]);
                dialer.Value.CallerIDNumberFB.LinkInputSig(trilist.StringInput[joinMap.CallerIDNumberFB + dialerLineOffset]);
                dialer.Value.CallerIDNameFB.LinkInputSig(trilist.StringInput[joinMap.CallerIDNameFB + dialerLineOffset]);

                dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Dial + dialerLineOffset]);
                dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.OffHook + dialerLineOffset]);
                dialer.Value.OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.OnHook + dialerLineOffset]);
                dialer.Value.DialStringFeedback.LinkInputSig(trilist.StringInput[joinMap.DialStringCmd + dialerLineOffset]);
                dialer.Value.IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IncomingCall + dialerLineOffset]);

                lineOffset = lineOffset + 50;
            }
            

        }
    }
    public class TesiraDspDeviceJoinMap : JoinMapBase {
        public uint IsOnline { get; set; }
        public uint Address { get; set; }
        public uint ChannelMuteToggle { get; set; }
        public uint ChannelMuteOn { get; set; }
        public uint ChannelMuteOff { get; set; }
        public uint ChannelVolume { get; set; }
        public uint ChannelType { get; set; }
        public uint ChannelName { get; set; }
        public uint ChannelVolumeUp { get; set; }
        public uint ChannelVolumeDown { get; set; }
        public uint Presets { get; set; }
        public uint ChannelVisible { get; set; }

        
        public uint DialStringCmd { get; set; }
        public uint Keypad0 { get; set; }
        public uint Keypad1 { get; set; }
        public uint Keypad2 { get; set; }
        public uint Keypad3 { get; set; }
        public uint Keypad4 { get; set; }
        public uint Keypad5 { get; set; }
        public uint Keypad6 { get; set; }
        public uint Keypad7 { get; set; }
        public uint Keypad8 { get; set; }
        public uint Keypad9 { get; set; }
        public uint KeypadStar { get; set; }
        public uint KeypadPound { get; set; }
        public uint KeypadClear { get; set; }
        public uint KeypadBackspace { get; set; }
        public uint Dial { get; set; }
        public uint DoNotDisturbToggle { get; set; }
        public uint DoNotDisturbOn { get; set; }
        public uint DoNotDisturbOff { get; set; }
        public uint AutoAnswerToggle { get; set; }
        public uint AutoAnswerOn { get; set; }
        public uint AutoAnswerOff { get; set; }
        public uint OffHook { get; set; }
        public uint OnHook { get; set; }
        public uint CallerIDNumberFB { get; set; }
        public uint CallerIDNameFB { get; set; }
        public uint Answer { get; set; }
        public uint EndCall { get; set; }
        public uint IncomingCall { get; set; }
        
        public TesiraDspDeviceJoinMap() {

            // Arrays
            ChannelName = 200;
            ChannelMuteToggle = 400;
            ChannelMuteOn = 600;
            ChannelMuteOff = 800;
            ChannelVolume = 200;
            ChannelVolumeUp = 1000;
            ChannelVolumeDown = 1200;
            ChannelType = 400;
            Presets = 100;
            ChannelVisible = 200;

            // SingleJoins
            IsOnline = 1;
            Address = 1;
            Presets = 100;
            
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
            DoNotDisturbToggle = 3132;
            DoNotDisturbOn = 3133;
            DoNotDisturbOff = 3134;
            AutoAnswerToggle = 3127;
            AutoAnswerOn = 3125;
            AutoAnswerOff = 3126;
            Dial = 3124;
            OffHook = 3130;
            OnHook = 3129;

            //Analog

            //String
            DialStringCmd = 3100;
            CallerIDNumberFB = 3104;
            CallerIDNameFB = 3105;
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