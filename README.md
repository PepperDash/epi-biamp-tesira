# Tesira-DSP-EPI

##Config Example :

```javascript
{
    "key": "TesiraDsp-1",
    "name": "TesiraDspTesting",
    "type": "tesiraDsp",
    "group": "dsp",
    "properties": {
        "control": {
            "endOfLineString": "\n",
            "deviceReadyResponsePattern": "",
            "method": "ssh",
            "tcpSshProperties": {
                "address": "10.11.50.191",
                "port": 22,
                "autoReconnect": true,
                "AutoReconnectIntervalMs": 10000,
                "username": "default",
                "password": "default"
            }
        },
        "levelControlBlocks": {
            "Fader01": {
                "enabled": true,
                "isMic": false,
                "hasLevel": true,
                "hasMute": true,
                "index1" : 1,
                "index2" : 0,
                "label": "Room",
                "levelInstanceTag": "ROOMVOL",
                "muteInstanceTag": "ROOMVOL",
                "unmuteOnVolChange" : true,
                "incrementAmount" : "2.0"
            }
        },
        "dialerControlBlocks" : {
            "audioDialer01" : {
                "enabled" : true,
                "isVoip" : true,
                "dialerInstanceTag" : "Dialer1",
                "controlStatusInstanceTag" : "VoIPControlStatus1",
                "index" : 1,
                "callAppearance" : 1,
                "clearOnHangup" : true,
                "appendDtmf" : false
            }
        },
        "presets" : {
            "Preset01 ": {
                "label" : "Default",
                "preset" : "Default Levels"  
            },
            "Preset02" : {
                "label" : "High",
                "Preset" : "Noise Reduction High"
            }
        }
    }
},
{
    "key": "eisc-Dsp",
    "uid": 4,
    "name": "Bridge Dsp",
    "group": "api",
    "type": "eiscApi",
    "properties": {
        "control": {
            "tcpSshProperties": {
                "address": "127.0.0.2",
                "port": 0
            },
            "ipid": "D1",
            "method": "ipidTcp"
        },
        "devices": [
            {
                "deviceKey": "TesiraDsp-1",
                "joinStart": 1
            }
        ]
    }
}
```



##JoinMap

```.net
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
```