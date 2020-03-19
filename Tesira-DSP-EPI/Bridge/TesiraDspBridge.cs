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

            TesiraDspDeviceJoinMap deviceJoinMap = new TesiraDspDeviceJoinMap(joinStart);
            TesiraDialerJoinMap dialerJoinMap = new TesiraDialerJoinMap(joinStart);
            TesiraLevelJoinMap levelJoinMap = new TesiraLevelJoinMap(joinStart);
            TesiraStateJoinMap stateJoinMap = new TesiraStateJoinMap(joinStart);
            TesiraSwitcherJoinMap switcherJoinMap = new TesiraSwitcherJoinMap(joinStart);
            TesiraPresetJoinMap presetJoinMap = new TesiraPresetJoinMap(joinStart);

            Debug.Console(1, DspDevice, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            ushort x = 1;
            //var comm = DspDevice as IBasicCommunication;


            DspDevice.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[deviceJoinMap.IsOnline]);
            DspDevice.CommandPassthruFeedback.LinkInputSig(trilist.StringInput[deviceJoinMap.CommandPassthruRx]);
            trilist.SetStringSigAction(deviceJoinMap.DirectPreset, s => DspDevice.RunPreset(s));

            trilist.SetStringSigAction(deviceJoinMap.CommandPassthruTx, s => DspDevice.SendLineRaw(s));


            //Level and Mute Control
            Debug.Console(2, DspDevice, "There are {0} Level Control Points", DspDevice.LevelControlPoints.Count());
            foreach (var channel in DspDevice.LevelControlPoints) {
                //var TesiraChannel = channel.Value as Tesira.DSP.EPI.TesiraDspLevelControl;
                Debug.Console(2, "TesiraChannel {0} connect", x);

                var genericChannel = channel.Value as IBasicVolumeWithFeedback;
                if (channel.Value.Enabled) {
                    Debug.Console(2, DspDevice, "TesiraChannel {0} Is Enabled", x);
                    trilist.StringInput[levelJoinMap.Label + x].StringValue = channel.Value.Label;
                    trilist.UShortInput[levelJoinMap.Type + x].UShortValue = (ushort)channel.Value.Type;
                    trilist.UShortInput[levelJoinMap.Status + x].UShortValue = (ushort)channel.Value.ControlType;
                    trilist.UShortInput[levelJoinMap.Permissions + x].UShortValue = (ushort)channel.Value.Permissions;
                    trilist.BooleanInput[levelJoinMap.Visible + x].BoolValue = true;

                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[levelJoinMap.MuteToggleFb + x]);
                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[levelJoinMap.MuteOnFb + x]);
                    genericChannel.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[levelJoinMap.MuteOffFb + x]);
                    genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[levelJoinMap.VolumeFb + x]);

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
                    s.Value.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.ToggleFb + x]);
                    s.Value.StateFeedback.LinkInputSig(trilist.BooleanInput[stateJoinMap.OnFb + x]);
                    s.Value.StateFeedback.LinkComplementInputSig(trilist.BooleanInput[stateJoinMap.OffFb + x]);

                    trilist.StringInput[stateJoinMap.Label + x].StringValue = state.Value.Label;

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
                    //3 switchers
                    //(0 * 2) + 1 = 1
                    //(1 * 2) + 1 = 3
                    //(2 * 2) + 1 = 5
                    Debug.Console(2, DspDevice, "Tesira Switcher {0} is Enabled", x);

                    var s = switcher.Value as IRoutingWithFeedback;
                    var tempSwitcher = switcher.Value;
                    s.SourceIndexFeedback.LinkInputSig(trilist.UShortInput[switcherJoinMap.SourceSelectorIndexFb + y]);

                    trilist.SetUShortSigAction(switcherJoinMap.SourceSelectorIndex + y, u => tempSwitcher.SetSource(u));

                    trilist.StringInput[switcherJoinMap.SourceSelectorLabel + y].StringValue = switcher.Value.Label;


                    //trilist.SetSigTrueAction(joinMap.SourceSelectorMake + y, () => switcher.Value.MakeRoute());
                }
                x++;
            }

            

            //Presets 
            x = 0;
            foreach (var preset in DspDevice.PresetList) {
                var p = preset;
                var temp = x;
                trilist.StringInput[presetJoinMap.PresetNameFeedback + temp + 1].StringValue = p.preset;
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

                trilist.StringInput[dialerJoinMap.Label + x].StringValue = dialer.Value.Label;
                trilist.StringInput[dialerJoinMap.DisplayNumber + x].StringValue = dialer.Value.DisplayNumber;

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

                trilist.SetStringSigAction(dialerJoinMap.DialString + dialerLineOffset, s => dialer.Value.SetDialString(s));

                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbToggleFb + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbOnFb + dialerLineOffset]);
                dialer.Value.DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.DoNotDisturbOffFb + dialerLineOffset]);

                dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.DialFb + dialerLineOffset]);
                dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.OffHookFb + dialerLineOffset]);
                dialer.Value.OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.OnHookFb + dialerLineOffset]);
                dialer.Value.IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.IncomingCall + dialerLineOffset]);

                dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerToggleFb + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerOnFb + dialerLineOffset]);
                dialer.Value.AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[dialerJoinMap.AutoAnswerOffFb + dialerLineOffset]);

                dialer.Value.DialStringFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.DialStringFb + dialerLineOffset]);
                dialer.Value.CallerIDNumberFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.CallerIDNumberFB + dialerLineOffset]);
                dialer.Value.CallerIDNameFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.CallerIDNameFB + dialerLineOffset]);
                dialer.Value.LastDialedFeedback.LinkInputSig(trilist.StringInput[dialerJoinMap.LastNumberDialedFb + dialerLineOffset]);


                dialer.Value.CallStateFeedback.LinkInputSig(trilist.UShortInput[dialerJoinMap.CallState + dialerLineOffset]);

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