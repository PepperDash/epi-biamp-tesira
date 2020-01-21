# Tesira-DSP-EPI

> The Tesira plugin provides device control over the Biamp Tesira family of DSPs with regards 
> to the most commonly used and requested attriute and control types.

## Installation

Navigate to the BUILDS folder in the repository.  Place the .cplz file into the Plugins folder for Essentials and reset the application.

## Controls and Configs

### Base Device

This is data relevant to the device as a whole.  This includes directly setting presets, passing controls directly, and recalling presets by name.

#### Digitals

| Join | Type (RW) | Description   |
| ---- | --------- | -----------   |
| 1    | R         | Device Online |

#### Serials

| Join | Type (RW) | Description          |
| ---- | --------- | -----------          |
| 1    | RW        | ControlPassthru      |
| 100  | W         | Select Preset By Name|

#### Config Notes

> This configuration matches a standard essentials device configuration at the base level, with only the type being different.  This must have the type **tesiraDSP**

``` javascript
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
        }
    }
}
```

***

### Level / Mute

Controls objects with the attribute type of "level" or "mute" and subscribes to them as necessary.

This module only reports the level of the audio signal relative the the adjustable range

This Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + Fader/Mute Index as defined by the config.

#### Digitals

| Join | Type (RW) | Description     |
| ---- | --------- | -----------     |
| 200  | R         | Channel Visible |
| 400  | RW        | Mute Toggle     |
| 600  | RW        | Mute On         |
| 800  | RW        | Mute Off        |
| 1000 | W         | Volume Up       |
| 1200 | W         | Volume Down     |


#### Analogs

| Join | Type (RW) | Description                                    |
| ---- | --------- | -----------                                    |
| 200  | RW        | Volume Level                                   |
| 400  | R         | Icon (0 - Level, 1 - Mic)                      |
| 600  | R         | ControlType (0 Mute/Level, 1 LevelOnly, 2 MuteOnly) |
| 800  | R         | Permissions (Pass From Config)                 |

#### Serials

| Join | Type (RW) | Description                      |
| ---- | --------- | -----------                      |
| 200  | R         | Control Label (Pass From Config) |

#### Config Example

> All Level/Mute configs must be part of a dictionary called **levelControlBlocks**.  

``` javascript
"levelControlBlocks": {
    "01-Fader01": {
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
        "incrementAmount" : "2.0",
        "permissions" : 0
    }
```
#### Config Notes

> **enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**isMic** - drives the *icon* feedback.
**hasLevel** - in conjunction with *hasMute*, sets the *ControlType*.
**hasMute** - in conjunction with *hasStatus*, sets the *ControlType*.
**index1** - Index 1 of the control point.
**index2** - Index 2 of the control point.
**levelInstanceTag** - Instance tag of the level control.
**muteInstanceTag** - Instance tag of the mute control.
**unmuteOnVolChange** - if *true*, will unmute a muted control when the level increases.
**incrementAmount** - the value in decimals by which a mute increment or decrement command will manipulate the level.
**permissions** - Passed directly across the eisc as the *Permissions* value.

***

### SourceSelector

Controls objects with the attribute type of "sourceSelection" and subscribes to them as necessary.

 This Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + SourceSelector Index as defined by the config.

#### Digitals

None


#### Analogs

| Join | Type (RW) | Description      |
| ---- | --------- | -----------      |
| 150  | RW        | Source Selection |

#### Serials

| Join | Type (RW) | Description                      |
| ---- | --------- | -----------                      |
| 150  | R         | Control Label (Pass From Config) |

#### Config Example

> All sourceSelector configs must be part of a dictionary called **switcherControlBlocks**.  

``` javascript
"switcherControlBlocks" : {
    "Switcher01" : {
        "enabled" : true,
        "label" : "switcher01",
        "index1" : 1
        "switcherInstanceTag" : "SourceSelector1",
    }
}
```

#### Config Notes

> **enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**index1** - Index 1 of the control point.
**switcherInstanceTag** - Instance tag of the sourceSelection control

***

### State

Controls objects with the attribute type of "state" and subscribes to them as necessary.

This Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + State Index as defined by the config.

#### Digitals

| Join | Type (RW) | Description  |
| ---- | --------- | -----------  |
| 1300 | RW        | State Toggle |
| 1450 | RW        | State On     |
| 1600 | RW        | State Off    |

#### Analogs

None

#### Serials

| Join | Type (RW) | Description                      |
| ---- | --------- | -----------                      |
| 1300 | R         | Control Label (Pass From Config) |

#### Config Example

> All state configs must be part of a dictionary called **stateControlBlocks**.  

``` javascript
"stateControlBlocks" : {
    "state01" : {
        "enabled" : true,
        "label" : "State01",
        "stateInstanceTag" : "LogicState1",
        "index" : 1
    }
}
```

#### Config Notes


> **enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**index** - Index of the control point.
**stateInstanceTag** - Instance tag of the state control

***

### Presets

> All state configs must be part of a dictionary called **presets**.  

> I fyou intend to ONLY do direct preset calling by string, this object is NOT required to recall presets. This activity is provided by the base level device object for the Tesira DSP.

#### Digitals

| Join | Type (RW) | Description            |
| ---- | --------- | -----------            |
| 100  | W         | Select Preset By Index |

#### Analogs

None

#### Serials

| Join | Type (RW) | Description                                |
| ---- | --------- | -----------                                |
| 100  | R         | Preset Name by Index                       |

#### Config Example

``` javascript
"presets" : {
    "Preset01 ": {
        "label" : "Default",
        "preset" : "Default Levels"  
    }
}
```

#### Config Notes

**label** - Passed directly across the eisc as the *Label* value.
**preset** - the actual name of the preset as defined in biamp software

***

### Dialer

VoIP Controls are tested

POTS Controls are added, but as of yet untested.

DTMF is automatically managed based on current hook state.

This Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + (1 + (50 * (n- 1))), where n is the index of the dialer as defined by config.

For example, Incoming Call for Line 1 would be at join 3101, while the incoming call for Line 2 would be at join 3151.

#### Digitals

| Join | Type (RW) | Description           |
| ---- | --------- | -----------           |
| 3100 | R         | Incoming Call         |
| 3106 | W         | Answer                |
| 3107 | W         | End Call              |
| 3110 | W         | Keypad 0              |
| 3111 | W         | Keypad 1              |
| 3112 | W         | Keypad 2              |
| 3113 | W         | Keypad 3              |
| 3114 | W         | Keypad 4              |
| 3115 | W         | Keypad 5              |
| 3116 | W         | Keypad 6              |
| 3117 | W         | Keypad 7              |
| 3118 | W         | Keypad 8              |
| 3119 | W         | Keypad 9              |
| 3120 | W         | Keypad *              |
| 3121 | W         | Keypad #              |
| 3122 | W         | Keypad Clear          |
| 3123 | W         | Keypad Backspace      |
| 3124 | RW        | Dial                  |
| 3125 | RW        | Auto Answer On        |
| 3126 | RW        | Auto Answer Off       |
| 3127 | RW        | Auto Answer Toggle    |
| 3129 | RW        | On Hook               |
| 3130 | RW        | Off Hook              |
| 3132 | RW        | Do Not Disturb Toggle |
| 3133 | RW        | Do Not Disturb On     |
| 3134 | RW        | Do Not Disturb Off    |

#### Analogs

| Join | Type (RW) | Description     |
| ---- | --------- | -----------     |
| 3100 | R         | CallState Value |

#### Serials

| Join | Type (RW) | Description        |
| ---- | --------- | -----------        |
| 3100 | RW        | Dial String        |
| 3101 | RW        | Dialer Label      |
| 3102 | RW        | Last Number Dialed |
| 3104 | R         | Caller ID Number   |
| 3105 | R         | Caller ID Name     |

#### Config Example

> All dialer configs must be part of a dictionary called **dialerControlBlocks**.  

``` javascript
"dialerControlBlocks" : {
    "audioDialer01" : {
        "enabled" : true,
        "label" : "Dialer 01"
        "isVoip" : true,
        "dialerInstanceTag" : "Dialer1",
        "controlStatusInstanceTag" : "VoIPControlStatus1",
        "index" : 1,
        "callAppearance" : 1,
        "clearOnHangup" : true,
        "appendDtmf" : false
    }
}
```

#### Config Notes

> **enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**index** - Index of the line you wish to manage.
**dialerInstanceTag** - Instance tag of the dialer control
**controlStatusInstanceTag** - Instance tag of the controlStatus control
**isVoip** - sets the device type to VoIP for internal configuration.
**callAppearance** - the index of the call appearance you wish to contorl in a VoIP line.
**clearOnHangup** - if *true* will clear the *Dial String* whenever the line goes on hook.
**appendDtmf** - if **true** will append DTMF digit presses to *Dial String*

***

## Full Example EFS Config

>This config will create an internal loopback EISC on IPID D1 for a ssh-controlled tesira.

```javascript
{
    "system": {},
    "system_url": "http://portal-QA.devcloud.pepperdash.com/templates/0f50640b-bc89-42d5-998f-81d137d3fc98#/template_summary",
    "template": {
        "devices": [
            {
                "key": "processor",
                "uid": 0,
                "type": "rmc3",
                "name": "RMC3",
                "group": "processor",
                "supportedConfigModes": [
                    "compliance",
                    "essentials"
                ],
                "supportedSystemTypes": [
                    "hudType",
                    "presType",
                    "vtcType",
                    "custom"
                ],
                "supportsCompliance": true,
                "properties": {}
            },
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
                            "incrementAmount" : "2.0",
                            "permissions" : 0
                        },
                        "Fader02": {
                            "enabled": true,
                            "isMic": false,
                            "hasLevel": true,
                            "hasMute": true,
                            "index1" : 1,
                            "index2" : 0,
                            "label": "VTC",
                            "levelInstanceTag": "VTCRXVOL",
                            "muteInstanceTag": "VTCRXVOL",
                            "unmuteOnVolChange" : true,
                            "incrementAmount" : "2.0"
                            "permissions" : 1
                        },
                        "Fader03": {
                            "enabled": true,
                            "isMic": false,
                            "hasLevel": true,
                            "hasMute": true,
                            "index1" : 1,
                            "index2" : 0,
                            "label": "ATC",
                            "levelInstanceTag": "ATCRXVOL",
                            "muteInstanceTag": "ATCRXVOL",
                            "unmuteOnVolChange" : true,
                            "incrementAmount" : "2.0"
                            "permissions" : 2"
                        },
                        "Fader04": {
                            "enabled": true,
                            "isMic": false,
                            "hasLevel": true,
                            "hasMute": true,
                            "index1" : 1,
                            "index2" : 0,
                            "label": "PGM",
                            "levelInstanceTag": "PGMVOL",
                            "muteInstanceTag": "PGMVOL",
                            "unmuteOnVolChange" : true,
                            "incrementAmount" : "2.0"
                            "permissions" : 0
                        }
                    },
                    "dialerControlBlocks" : {
                        "audioDialer01" : {
                            "enabled" : true,
                            "label" : "Dialer 1",
                            "isVoip" : true,
                            "dialerInstanceTag" : "Dialer1",
                            "controlStatusInstanceTag" : "VoIPControlStatus1",
                            "index" : 1,
                            "callAppearance" : 1,
                            "clearOnHangup" : true,
                            "appendDtmf" : false
                        }
                    },
                    "stateControlBlocks" : {
                        "state01" : {
                            "enabled" : true,
                            "label" : "State01",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 1
                        },
                        "state02" : {
                            "enabled" : true,
                            "label" : "State02",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 2
                        },
                        "state03" : {
                            "enabled" : true,
                            "label" : "State02",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 3
                        },
                        "state04" : {
                            "enabled" : true,
                            "label" : "State02",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 4
                        }
                    },
                    "switcherControlBlocks" : {
                        "Switcher01" : {
                            "enabled" : true,
                            "label" : "switcher01",
                            "switcherInstanceTag" : "SourceSelector1",
                            "index1" : 1
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
        ],
        "info": {
            "comment": "",
            "lastModifiedDate": "2017-03-06T23:14:40.290Z",
            "lastUid": 12,
            "processorType": "Rmc3",
            "requiredControlSofwareVersion": "",
            "systemType": "MPR"
        },
        "rooms": [],
        "tieLines": []
    }
}
```

## RoadMap

1. "Generic" control - so we don't have to develop new features for controls that only you will use.
2. RouterBlock Control - It's a rarely used control, but it essentially a n > n audio switcher
3. Crosspoint Control - Mute/Unmute and Increase/Decrease volume levels on the crosspoint of matrix switches.