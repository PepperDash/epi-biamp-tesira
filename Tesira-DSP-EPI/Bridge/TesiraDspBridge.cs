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
using Tesira_DSP_EPI.Bridge.JoinMaps;

namespace Tesira_DSP_EPI.Bridge {
    public static class TesiraDspDeviceApiExtensions {
        public static void LinkToApiExt(this TesiraDsp DspDevice, BasicTriList trilist, uint joinStart, string joinMapKey) {


            TesiraDspDeviceJoinMap joinMap = new TesiraDspDeviceJoinMap();

            var JoinMapSerialized = JoinMapHelper.GetJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(JoinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraDspDeviceJoinMap>(JoinMapSerialized);

            joinMap.OffsetJoinNumbers(joinStart);
            TesiraDialerJoinMap dialerJoinMap = new TesiraDialerJoinMap(joinStart);
            TesiraLevelJoinMap levelJoinMap = new TesiraLevelJoinMap(joinStart);
            TesiraStateJoinMap stateJoinMap = new TesiraStateJoinMap(joinStart);
            TesiraSwitcherJoinMap switcherJoinMap = new TesiraSwitcherJoinMap(joinStart);
            TesiraPresetJoinMap presetJoinMap = new TesiraPresetJoinMap(joinStart);

            Debug.Console(1, DspDevice, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            ushort x = 1;
            var comm = DspDevice as ICommunicationMonitor;
            DspDevice.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline]);


            //Level and Mute Control
            Debug.Console(2, DspDevice, "There are {0} Level Control Points", DspDevice.LevelControlPoints.Count());
            foreach (var channel in DspDevice.LevelControlPoints) {
                //var TesiraChannel = channel.Value as Tesira.DSP.EPI.TesiraDspLevelControl;
                Debug.Console(2, "TesiraChannel {0} connect", x);

                var genericChannel = channel.Value as IBasicVolumeWithFeedback;
                if (channel.Value.Enabled) {
                    Debug.Console(2, DspDevice, "TesiraChannel {0} Is Enabled", x);
                    trilist.StringInput[levelJoinMap.Name + x].StringValue = channel.Value.Label;
                    trilist.UShortInput[levelJoinMap.Type + x].UShortValue = (ushort)channel.Value.Type;
                    trilist.UShortInput[levelJoinMap.Status + x].UShortValue = (ushort)channel.Value.ControlType;
                    trilist.UShortInput[levelJoinMap.Permissions + x].UShortValue = (ushort)channel.Value.Permissions;
                    trilist.BooleanInput[levelJoinMap.Visible + x].BoolValue = true;

                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[levelJoinMap.MuteToggle + x]);
                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[levelJoinMap.MuteOn + x]);
                    genericChannel.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[levelJoinMap.MuteOff + x]);
                    genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[levelJoinMap.Volume + x]);

                    trilist.SetSigTrueAction(levelJoinMap.MuteToggle + x, () => genericChannel.MuteToggle());
                    trilist.SetSigTrueAction(levelJoinMap.MuteOn + x, () => genericChannel.MuteOn());
                    trilist.SetSigTrueAction(levelJoinMap.MuteOff + x, () => genericChannel.MuteOff());

                    trilist.SetBoolSigAction(levelJoinMap.VolumeUp + x, b => genericChannel.VolumeUp(b));
                    trilist.SetBoolSigAction(levelJoinMap.VolumeDown + x, b => genericChannel.VolumeDown(b));

                    trilist.SetUShortSigAction(levelJoinMap.Volume + x, u => { if (u > 0) { genericChannel.SetVolume(u); } });
                    //channel.Value.DoPoll();
                }
                x++;
            }

            //states
            x = 1;
            Debug.Console(2, DspDevice, "There are {0} State Control Points", DspDevice.States.Count());
            foreach (var state in DspDevice.States) {
                Debug.Console(2, DspDevice, "Tesira State {0} connect to {1}", state.Key, x);
                if (state.Value.Enabled) {
                    Debug.Console(2, DspDevice, "Tesira State {0} at {1} is Enabled", state.Key, x);

                    var s = state;
                    s.Value.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.Toggle + x]);
                    trilist.StringInput[stateJoinMap.Label + x].StringValue = state.Value.Label;
                    s.Value.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.On + x]);
                    s.Value.StateFeedback.LinkComplementInputSig(trilist.BooleanInput[stateJoinMap.Off + x]);

                    trilist.SetSigTrueAction(stateJoinMap.Toggle + x, () => s.Value.StateToggle());
                    trilist.SetSigTrueAction(stateJoinMap.On + x, () => s.Value.StateOn());
                    trilist.SetSigTrueAction(stateJoinMap.Off + x, () => s.Value.StateOff());          
                }
                x++;
            }
            
            
            //Source Selectors
            x = 0;
            Debug.Console(2, DspDevice, "There are {0} SourceSelector Control Points", DspDevice.Switchers.Count());
            foreach (var switcher in DspDevice.Switchers) {
                Debug.Console(2, DspDevice, "Tesira Switcher {0} connect to {1}", switcher.Key, x);
                if (switcher.Value.Enabled) {
                    ushort y = (ushort)((x * 2) + 1);
                    Debug.Console(2, DspDevice, "Tesira Switcher {0} is Enabled", x);

                    var s = switcher.Value as IRoutingWithFeedback;
                    s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.SourceSelectorIndex + y]);

                    trilist.SetUShortSigAction(switcherJoinMap.SourceSelectorIndex + y, u => switcher.Value.SetSource(u));

                    trilist.StringInput[switcherJoinMap.SourceSelectorLabel + y].StringValue = switcher.Value.Label;


                    //trilist.SetSigTrueAction(joinMap.SourceSelectorMake + y, () => switcher.Value.MakeRoute());
                }
                x++;
            }

            

            //Presets 
            x = 0;
            trilist.SetStringSigAction(presetJoinMap.DirectPreset, s => DspDevice.RunPreset(s));
            foreach (var preset in DspDevice.PresetList) {
                var temp = x;
                trilist.StringInput[presetJoinMap.PresetName + temp + 1].StringValue = preset.label;
                trilist.SetSigTrueAction(presetJoinMap.PresetSelection + temp + 1, () => DspDevice.RunPresetNumber(temp));
                x++;
            }

            // VoIP Dialer
            
            uint lineOffset = 0;
            foreach (var line in DspDevice.Dialers) {
                var dialer = line;
                var dialerLineOffset = lineOffset += 1;
                Debug.Console(2, "AddingDialerBRidge {0} {1} Offset", dialer.Key, dialerLineOffset);
                trilist.SetSigTrueAction((dialerJoinMap.Keypad0 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num0));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad1 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num1));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad2 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num2));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad3 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num3));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad4 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num4));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad5 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num5));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad6 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num6));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad7 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num7));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad8 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num8));
                trilist.SetSigTrueAction((dialerJoinMap.Keypad9 + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Num9));
                trilist.SetSigTrueAction((dialerJoinMap.KeypadStar + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Star));
                trilist.SetSigTrueAction((dialerJoinMap.KeypadPound + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Pound));
                trilist.SetSigTrueAction((dialerJoinMap.KeypadClear + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Clear));
                trilist.SetSigTrueAction((dialerJoinMap.KeypadBackspace + dialerLineOffset), () => dialer.Value.SendKeypad(Tesira_DSP_EPI.TesiraDspDialer.eKeypadKeys.Backspace));

                trilist.SetSigTrueAction(dialerJoinMap.Dial + dialerLineOffset, () => dialer.Value.Dial());
                trilist.SetSigTrueAction(dialerJoinMap.DoNotDisturbToggle + dialerLineOffset, () => dialer.Value.DoNotDisturbToggle());
                trilist.SetSigTrueAction(dialerJoinMap.DoNotDisturbOn + dialerLineOffset, () => dialer.Value.DoNotDisturbOn());
                trilist.SetSigTrueAction(dialerJoinMap.DoNotDisturbOff + dialerLineOffset, () => dialer.Value.DoNotDisturbOff());
                trilist.SetSigTrueAction(dialerJoinMap.AutoAnswerToggle + dialerLineOffset, () => dialer.Value.AutoAnswerToggle());
                trilist.SetSigTrueAction(dialerJoinMap.AutoAnswerOn + dialerLineOffset, () => dialer.Value.AutoAnswerOn());
                trilist.SetSigTrueAction(dialerJoinMap.AutoAnswerOff + dialerLineOffset, () => dialer.Value.AutoAnswerOff());
                trilist.SetSigTrueAction(dialerJoinMap.Answer + dialerLineOffset, () => dialer.Value.Answer());
                trilist.SetSigTrueAction(dialerJoinMap.EndCall + dialerLineOffset, () => dialer.Value.EndAllCalls());
                trilist.SetSigTrueAction(dialerJoinMap.OnHook + dialerLineOffset, () => dialer.Value.OnHook());
                trilist.SetSigTrueAction(dialerJoinMap.OffHook + dialerLineOffset, () => dialer.Value.OffHook());

                trilist.SetStringSigAction(dialerJoinMap.DialStringCmd + dialerLineOffset, s => dialer.Value.SetDialString(s));

                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbToggle + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbOn + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbOff + dialerLineOffset]);

                dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerToggle + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerOn + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerOff + dialerLineOffset]);
                dialer.Value.CallerIDNumberFB.LinkInputSig(trilist.StringInput[dialerJoinMap.CallerIDNumberFB + dialerLineOffset]);
                dialer.Value.CallerIDNameFB.LinkInputSig(trilist.StringInput[dialerJoinMap.CallerIDNameFB + dialerLineOffset]);

                dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.Dial + dialerLineOffset]);
                dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.OffHook + dialerLineOffset]);
                dialer.Value.OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.OnHook + dialerLineOffset]);
                dialer.Value.DialStringFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.DialStringCmd + dialerLineOffset]);
                dialer.Value.IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.IncomingCall + dialerLineOffset]);

                lineOffset += 50;
            }
            

        }
    }
    /*
    public class TesiraDspDeviceJoinMap : JoinMapBase {
        public uint IsOnline { get; set; }
        
        
        public TesiraDspDeviceJoinMap() {

            
            //Digital
            IsOnline = 1;
            
        }

        public override void OffsetJoinNumbers(uint joinStart) {
            var joinOffset = joinStart - 1;
            var properties = this.GetType().GetCType().GetProperties().Where(o => o.PropertyType == typeof(uint)).ToList();
            foreach (var property in properties) {
                property.SetValue(this, (uint)property.GetValue(this, null) + joinOffset, null);
            }
        }
    }*/

}