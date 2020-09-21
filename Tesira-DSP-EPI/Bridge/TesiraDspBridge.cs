﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;

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
            TesiraMeterJoinMap meterJoinMap = new TesiraMeterJoinMap(joinStart);
            TesiraMatrixMixerJoinMap matrixMixerJoinMap = new TesiraMatrixMixerJoinMap(joinStart);


            TesiraRoomCombinerJoinMap roomCombinerJoinMap = new TesiraRoomCombinerJoinMap(joinStart);

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
            foreach (var item in DspDevice.Switchers) {
                var switcher = item;
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

            Debug.Console(2, DspDevice, "There are {0} Meter Control Points", DspDevice.Meters.Count);
            for (int meterJoin = 0; meterJoin < DspDevice.Meters.Count; meterJoin++)
            {
                var joinActual = meterJoinMap.MeterJoin + meterJoin;                
                var meter = DspDevice.Meters.ElementAtOrDefault(meterJoin);
                if (meter.Key == null) continue;

                Debug.Console(2, DspDevice, "AddingMeterBridge {0} | Join:{1}", meter.Key, joinActual);
                meter.Value.MeterFeedback.LinkInputSig(trilist.UShortInput[(uint)joinActual]);
                meter.Value.LabelFeedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
                meter.Value.SubscribedFeedback.LinkInputSig(trilist.BooleanInput[(uint)joinActual]);

                trilist.SetSigTrueAction((uint)joinActual, meter.Value.Subscribe);
                trilist.SetSigFalseAction((uint)joinActual, meter.Value.UnSubscribe);
            }

            Debug.Console(2, DspDevice, "There are {0} MatrixMixer Control Points", DspDevice.MatrixMixers.Count);
            for (int matrixMixer = 0; matrixMixer < DspDevice.MatrixMixers.Count; matrixMixer++)
            {
                var toggleJoin = matrixMixerJoinMap.Toggle + (matrixMixer * 3);
                var onJoin = matrixMixerJoinMap.On + (matrixMixer * 3);
                var offJoin = matrixMixerJoinMap.Off + (matrixMixer * 3);

                var mixer = DspDevice.MatrixMixers.ElementAtOrDefault(matrixMixer);
                if (mixer.Key == null) continue;

                Debug.Console(2, DspDevice, "Adding MatrixMixer ControlPoint {0} | JoinStart:{1}", mixer.Key, toggleJoin);
                mixer.Value.StateFeedback.LinkInputSig(trilist.BooleanInput[(uint)toggleJoin]);

                trilist.SetSigTrueAction((uint)toggleJoin, mixer.Value.StateToggle);
                trilist.SetSigTrueAction((uint)onJoin, mixer.Value.StateOn);
                trilist.SetSigTrueAction((uint)offJoin, mixer.Value.StateOff);
            }

            Debug.Console(2, DspDevice, "There are {0} Room Combiner Control Points", DspDevice.RoomCombiners.Count);
            x = 0;
            foreach (KeyValuePair<string, TesiraDspRoomCombiner> roomCombiner in DspDevice.RoomCombiners)
            {
                Debug.Console(2, "Tesira Room Combiner {0} connect", x);
                var genericChannel = roomCombiner.Value as IBasicVolumeWithFeedback;
                if (roomCombiner.Value.Enabled)
                {
                    Debug.Console(2, DspDevice, "TesiraChannel {0} Is Enabled", x);
                    trilist.StringInput[roomCombinerJoinMap.Label + x].StringValue = roomCombiner.Value.Label;
                    trilist.UShortInput[roomCombinerJoinMap.Permissions + x].UShortValue = (ushort)roomCombiner.Value.Permissions;
                    trilist.BooleanInput[roomCombinerJoinMap.Visible + x].BoolValue = true;

                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[roomCombinerJoinMap.MuteToggleFb + x]);
                    genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[roomCombinerJoinMap.MuteOnFb + x]);
                    genericChannel.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[roomCombinerJoinMap.MuteOffFb + x]);
                    genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[roomCombinerJoinMap.VolumeFb + x]);

                    trilist.SetSigTrueAction(roomCombinerJoinMap.MuteToggle + x, () => genericChannel.MuteToggle());
                    trilist.SetSigTrueAction(roomCombinerJoinMap.MuteOn + x, () => genericChannel.MuteOn());
                    trilist.SetSigTrueAction(roomCombinerJoinMap.MuteOff + x, () => genericChannel.MuteOff());

                    trilist.SetBoolSigAction(roomCombinerJoinMap.VolumeUp + x, b => genericChannel.VolumeUp(b));
                    trilist.SetBoolSigAction(roomCombinerJoinMap.VolumeDown + x, b => genericChannel.VolumeDown(b));

                    trilist.SetUShortSigAction(roomCombinerJoinMap.Volume + x, u => { if (u > 0) { genericChannel.SetVolume(u); } });

                    trilist.SetUShortSigAction(roomCombinerJoinMap.Group + x, u => { if (u > 0) { roomCombiner.Value.SetRoomGroup(u); } });

                    roomCombiner.Value.RoomGroupFeedback.LinkInputSig(trilist.UShortInput[roomCombinerJoinMap.GroupFb + x]);

                }
                x++;
            }
        }
    }
}