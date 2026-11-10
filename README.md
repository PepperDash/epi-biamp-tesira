![3Series Tested](https://img.shields.io/badge/3-Tested%20on%203%20series-purple.svg)
![4Series-Tested](https://img.shields.io/badge/4-Tested%20on%204%20series-teal.svg)


# Tesira DSP Essentials Plugin (c) 2023

> [!WARNING]
> 1. INVALID TAGS BREAK THIS RELEASE
> 2. Do not update Biamp FW independent of Crestron Software and Essentials

> [!IMPORTANT]
> Biamp FW v4.7.2 requires Essentials v1.15.5, or newer
>
> Biamp FW v.4.7.2 includes a change of "deprecated SSH ciphers" which eliminates the ability to connect over SSH using Essentials v1.15.4 and prior.  Essentials v1.15.5 is compiled with the latest Crestron libraries, which are rleied upon for the SSH connection and resolves the issue.

## License

Provided under MIT license

## Overview

> The Tesira plugin provides device control over the Biamp Tesira family of DSPs with regards
> to the most commonly used and requested attriute and control types.

## Compatibility

This plugin has been tested with both Crestron 3-Series and 4-Series processors.  It implements all available essentials interfaces relevant to a device of this type.  This includes but is not limited to `AudioCodecBase`, `IHasDspPresets`, `IBasicVolumeWithFeedback`, `IRoutingWithFeedback` and `IDeviceInfoProvider`.  Documentation for which controls implement each interface will be documented in each relevant controls section.  Additionally, every component implements `IKeyed` and all devices are added to the `DeviceManager` unpon instantiation.

## Upgrading from v3.x to v4

v4 runs on Essentials v3 (net8, 4-Series). v3.x (latest `v3.8.2`) is the Essentials v2 line. Most changes are fixes that make documented behaviour work. Check the items below against an existing system before upgrading.

### Behaviour changes

| Change | Who is affected | What to do |
|---|---|---|
| **Meters are opt-in.** A meter streams only while its subscribe join is high, or after `Subscribe()` is called on the meter device. v3.x subscribed every meter at startup. | Programs that read meter levels (bridge level join or `IMeterFeedback`) without raising the meter's subscribe join | Raise the subscribe join (Legacy d3501 + offset; Standalone d1) while the meter is shown, or call `Subscribe()`. Without a SIMPL bridge, set `"subscribeOnStart": true` on the meter. For level-threshold feedback, prefer a logic meter in the design |
| **Raw level (dB text) moved from serial 300 to serial 400** on the Legacy map, so it no longer overwrites fader labels at bridgeIndex 100+ (#151). It is also on the Standalone map at serial 2. | Programs reading s301+ for raw level | Move to s401 + offset |
| **Room combiners link bridgeIndex 1–66 only** on the Legacy map. Higher indexes would overlap the router joins at 2601 and are logged and not linked. | Configs with a room combiner bridgeIndex above 66 | Renumber the combiner, or bridge it standalone |
| **Subscription watchdog** checks every 30 s (was every 2 minutes): its sentinels, plus 2 components in rotation. A `+OK` reply (the DSP had dropped subscriptions, for example after a design push) restores every subscription at once, without a reconnect. Components that can never subscribe (missing tag, bad attribute or index) are quarantined and logged once; a `+OK` releases quarantines (a missing design quarantines working components too), and `QuarantinedControls` lists them. | Everyone; no action needed | Optional: set `watchdogSentinels` to one component per partition. See *Subscription watchdog* |
| **Commands are dropped while the Tesira is disconnected,** with one log line per outage. When it reconnects, subscriptions report the DSP's actual state. Commands sent during an outage (a route, a level) are not replayed. | Programs that relied on queued commands reaching the DSP after a reconnect | Re-send after `IsOnline` goes high if that matters |
| **Command queue:** capacity 10,000 (was 2,000). It never blocks when full: it drops, with one warning per episode. Subscriptions and the plugin's own reads wait behind user commands, and writes keep their order. | Nobody | None |

### Joins and features that now work

These were documented in v3.x but didn't work. Programs that worked around them may see a change.

| Area | Now |
|---|---|
| Crosspoint On/Off joins | Set the crosspoint. They sent no value before, and the DSP rejected them |
| Meter subscribe join | Subscribes on high and unsubscribes on low. Before, only the unsubscribe worked |
| Preset recall by number (analog) | Recalls the preset. Before, it never matched |
| Router (`routerControlBlocks`) | Routes and reports every output: new Legacy joins at a/s 2601 + ((bridgeIndex − 1) × 64) + (output − 1), standalone a(joinStart + N − 1). The existing switcher join routes the first output. Before, no route was sent and feedback was dropped |
| Room combiner | Subscribes to `levelOut`; `group` and the level limits read the right room |
| Crosspoint Off feedback and labels; meter offsets per bridgeIndex | Linked on both bridge maps |

### New, opt-in

| Feature | Config |
|---|---|
| Save presets from the bridge (hold a preset join) with a 2 s *Preset Saved* pulse | `presetHoldTimeMs` (default `0`: off) |
| Logic Selector blocks as switchers | `logicSelectorControlBlocks` |
| Watchdog tuning | `watchdogIntervalSecs` (30), `watchdogPatrolSize` (2), `watchdogSentinels` |
| Periodic re-read of level limits | `levelRangePollIntervalSecs` (0: off) |
| Fault monitoring (active fault list, count, severity) | `faultPollIntervalSecs` (0: off), the FaultPollingEnable join, or `SetFaultPolling` (see [Fault monitoring](#fault-monitoring)) |

## Feature Notes

Version 2.0.0+ now offers developers the opportunity to individually bridge each control rather than the entire DSP object.  This should lead to more flexibility in development.  In documentation below, all references to the original object type will be referred to as **Legacy**, while new object data will be referred to as **Standalone**

When utilizing a standalone object within a Eisc bridge, it is addressed utilizing first the name of the base device, followed by a pair of two hyphens, then the key of the component.  

For a parent device with a key of ```dsp-1``` and a component with the key of ```Fader01```, the **Standalone** component key is ``dsp-1--Fader01``.

The only exception to this rule is for the Base **Standalone** object, which has a key suffix of **DeviceInfo**

``` javascript
"type": "eiscApiAdvanced"
```

### Subscription watchdog

The Tesira drops subscriptions without closing the session when a design is pushed or a partition restarts, often on blocks the push didn't touch. The plugin checks for this the way Biamp's own control modules do:

- Every `watchdogIntervalSecs` (default 30), it re-subscribes the **sentinels** plus the next `watchdogPatrolSize` (default 2) components in rotation, so a check costs a few commands whatever the design size.
- `-ERR ALREADY_SUBSCRIBED` means the subscription is still there. **`+OK` means the DSP had dropped it, so every subscription is restored at once**, without clearing the command queue or reconnecting.
- A check whose subscribe can never succeed (missing instance tag, bad attribute or index, attribute that can't be subscribed) **quarantines** that component: it is logged once and leaves the rotation. `devprops <slot> <key>` lists them in `QuarantinedControls`.
- A design that is missing for a while (pushed, or a unit swapped) answers "address not found" for every tag, so working components get quarantined too. Each check therefore also probes one quarantined component in rotation, and a `+OK` (dropped subscriptions) **releases every quarantine** before restoring: components whose tags really are missing are quarantined again by later checks. A quarantined component whose check shows every one of its subscriptions present (for example after a manual `Resubscribe`) is released too, as is everything on a new session.
- No reply to a check still drops and re-opens the session, as before.
- Subscriptions (startup, restores, checks) wait behind control commands in the plugin's command queue, so a restore burst never delays a button press.

Set `watchdogSentinels` to one component per partition: a push drops a partition's subscriptions together, so a sentinel in each one catches every push within one check. Without it, the first subscribed component is the sentinel and other partitions are caught when the patrol reaches them.

```json
"watchdogIntervalSecs": 30,
"watchdogPatrolSize": 2,
"watchdogSentinels": [ "RmaRoom", "RmbRoom" ]
```

### Fault monitoring

The plugin can report the Tesira's active faults, the same information Biamp's software and device management show: for example `INDICATOR_MAJOR_IN_DEVICE` / "one or more Dante flows inactive". It's off by default. The Tesira can't subscribe to faults, so when it's on, the plugin polls `DEVICE get activeFaultList`. Polls wait behind control commands in the queue and are skipped while the DSP is offline.

Ways to turn it on:

| How | Example | Lasts |
|---|---|---|
| Config (the default at startup) | `"faultPollIntervalSecs": 60` in the DSP's `properties`. 0 or missing means off; 1-9 is raised to 10, and more than 86400 (1 day) is lowered to 86400 | Always |
| Bridge join FaultPollingEnable (d11) | Rising edge: on, at the config interval or 60 s. Falling edge: off | Until the program restarts. The join never changes the config |
| Console | `devjson {"deviceKey":"dsp-1","methodName":"SetFaultPolling","params":[60]}`. Use 0 to turn it off | Until the program restarts. To keep it, set `faultPollIntervalSecs` in the config |

The enable join acts only on a change. A SIMPL program that leaves it low doesn't turn off monitoring that the config turned on, including the low state the EISC sends when it comes online. `devjson {"deviceKey":"dsp-1","methodName":"PollFaults"}` (or a pulse on d13) polls once.

**SIMPL restarts.** The join follows the SIMPL program's state. If a SIMPL program turned polling on with the join and then restarts, it comes back with the join low, and that falling edge turns polling off. To keep polling on across a SIMPL restart, either:

- raise the join again after startup, for example from a value the program saves and reads back from its own config. The rising edge turns polling back on and polls at once. Until that first reply arrives, the fault joins read as no faults, so if the program alarms on a fault clearing, have it ignore them until it finishes loading; or
- set `faultPollIntervalSecs` in the Essentials config instead. Config-enabled polling never turns off on a SIMPL restart, and the plugin re-sends the current values when the EISC reconnects.

The joins are on the device join map (both the **Legacy** and the **Standalone** `DeviceInfo` object): see [DeviceInfo](#deviceinfo). From code, use `FaultMonitor` on the DSP device: its feedbacks, `Current` (the parsed list) and the `FaultsChanged` event. A fault appearing, changing or clearing is also logged.

## Installation

This plugin is provided as a published [nuget package](https://www.nuget.org/packages/PepperDash.Essentials.Plugin.BiampTesira) for your convenience.  

Place the **\*.cplz** file in the /users/programxx/plugins folder, and restart your program.

***

## Controls and Configs

### DeviceInfo

This object is built by default with a Biamp Tesira DSP and provides a method to send hardware-specific data across the bridge to SIMPL

This data is only accessible by linking the device to the bridge.  The component key is always `DeviceInfo` - therefore when a Biamp Tesira DSP with the key `dsp-1` is instantiated, a DeviceInfo component
with the key of `dsp-1--DeviceInfo` will be added to the `DeviceManager` and will be accessible to any Essentials Bridge.

This device will also allow a direct passthru connection to the device if necessary and a method by which to trigger control resubscription.

If you do not choose to bridge a **Standalone** object, limited data is available on the **Legacy** object.

> This control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`IDeviceInfoProvider`** and **`IKeyed`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description          |
| ----------- | --------------- | --------- | -------------------- |
| 1           | 1               | R         | Device Online        |
| N/A         | 1               | W         | Resubscribe Controls |
| 11          | 11              | RW        | Fault monitoring enable: rising edge on, falling edge off. Feedback: polling active |
| 12          | 12              | R         | Has fault            |
| 13          | 13              | W         | Poll faults now      |

#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description |
| ----------- | --------------- | --------- | ----------- |
| 11          | 11              | R         | Active fault count |
| 12          | 12              | R         | Fault severity: 0 none, 1 minor, 2 major, 3 critical |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description         |
| ----------- | --------------- | --------- | ------------------- |
| N/A         | 1               | R         | Device Name         |
| N/A         | 2               | RW        | Command Passthrough |
| N/A         | 3               | R         | Serial Number       |
| N/A         | 4               | R         | Firmware Version    |
| N/A         | 5               | R         | Hostname            |
| N/A         | 6               | R         | IP Address          |
| N/A         | 7               | R         | MAC Address         |
| 11          | 11              | R         | Fault ID: the most severe indicator, e.g. `INDICATOR_MAJOR_IN_DEVICE` |
| 12          | 12              | R         | Fault name, e.g. "Major Fault in Device" |
| 13          | 13              | R         | Active faults' names, comma-separated |

The fault joins report only while [fault monitoring](#fault-monitoring) is on.

#### Config Notes

> This configuration matches a standard essentials device configuration at the base level, with only the type being different.  This may have the type **```tesira```**, **```tesiraforte```**, **```tesiraserver```**, **```tesiradsp```**, or **```tesira-dsp```**.

```json
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
                "password": "default",
                "disableSshEcho": true
            }
        }
    }
}
```
> The `disableSshEcho` property is valid in Essentials 2.

***

### Level / Mute

Controls objects with the attribute type of "level" or "mute" and subscribes to them as necessary.

This component only reports the level of the audio signal relative the the adjustable range

Within the **Legacy** object, this Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + fader ```bridgeIndex``` as defined by the config.

Within the **Standalone** object, this join map represents a single control as defined by the key in the bridge.

>This Control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`IKeyed`** and **`IBasicVolumeWithFeedback`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description     |
| ----------- | --------------- | --------- | --------------- |
| 200         | 6               | R         | Channel Visible |
| 400         | 5               | RW        | Mute Toggle     |
| 600         | 3               | RW        | Mute On         |
| 800         | 4               | RW        | Mute Off        |
| 1000        | 1               | W         | Volume Up       |
| 1200        | 2               | W         | Volume Down     |

#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description                                         |
| ----------- | --------------- | --------- | --------------------------------------------------- |
| 200         | 1               | RW        | Volume Level                                        |
| 400         | 2               | R         | Icon (0 - Level, 1 - Mic)                           |
| 600         | 3               | R         | ControlType (0 Mute/Level, 1 LevelOnly, 2 MuteOnly) |
| 800         | 4               | R         | Permissions (Pass From Config)                      |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description                                      |
| ----------- | --------------- | --------- | ------------------------------------------------ |
| 200         | 1               | R         | Control Label (Pass From Config)                 |
| 400         | 2               | R         | Raw Level in dB (e.g., "-12 dB", "-50 dB")      |

#### Config Example

> All Level/Mute configs must be part of a dictionary called **faderControlBlocks**.  

```json
"faderControlBlocks": {
    "LevelControl01": {
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
        "permissions" : 0,
        "bridgeIndex" : 1
    }
}
```

#### Config Notes

**enabled** - enables the control to be subscribed and controlled.
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
**bridgeIndex** - The index of the control on a **Legacy** object

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--LevelControl01```.

***

### Switcher

> [!NOTE]
> Version 2.5.0 added the ability to configure SourceSelectors and Routers as different objects.  It is recommended to use this mechanism in new projects.

Controls objects with the attribute type of `sourceSelection` and subscribes to them as necessary; `input` is not subscribed to.

Within the **Legacy** object, this Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + fader ```bridgeIndex``` as defined by the config.

Within the **Standalone** object, this join map represents a single control as defined by the key in the bridge.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`IKeyed`** and **`IRoutingWithFeedback`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description |
| ----------- | --------------- | --------- | ----------- |
| 150         | 1               | W         | Poll        |


#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description      |
| ----------- | --------------- | --------- | ---------------- |
| 150         | 1               | RW        | Source Selection |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description                                       |
| ----------- | --------------- | --------- | ------------------------------------------------- |
| 150         | 1               | R         | Control Label (Pass From Config)                  |
| N/A         | 2               | R         | Source List XSIG (If Configured) or Routed Source |

#### Config Example

> All sourceSelector and router configs must be part of a dictionary called **switcherControlBlocks**. Router configs require that the "type" be set to "router"

```json
"switcherControlBlocks" : {
    "SwitcherControl01" : {
        "enabled" : true,
        "label" : "switcher01",
        "index1" : 1
        "switcherInstanceTag" : "SourceSelector1",
        "type": "router",
        "bridgeIndex" : 1,
        "showRoutedStringFeedback": true,
        "pollIntervalMs" : 90000,
        "switcherInputs" : {
            "1" : {"label": "Input1" },
            "2" : {"label": "Input2" },
        },
        "switcherOutputs" : {
            "1" : { "label": "Output1" },
            "2" : { "label": "Output2" },
        }
    }
}
```

#### Config Notes

> **enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**index1** - Index 1 of the control point.
**type** - Valid Types are **sourceSelector** or **router**
**switcherInstanceTag** - Instance tag of the sourceSelection control
**bridgeIndex** - The index of the control on a **Legacy** object
**switcherInputs** - This is a dictionary of labels for inputs of the switcher.  The keys **must** be integers.
**switcherOutputs** - This is a dictionary of labels for outputs of the switcher.  This is earmarked for future usage.  The keys **must** be integers.

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--SwitcherControl01```.

***

### Router

Controls a Router block's `input` attribute, one route per output: output N can be set to any input M, and M = 0 clears it. Each output listed in `routerOutputs` is subscribed, so its feedback follows changes made anywhere (presets, Tesira software, other control systems). After every route the plugin reads that output back, because the DSP can merge changes made within one publish interval.

Within the **Legacy** object, each router has two kinds of joins:

- The switcher slot (join 150 + offset, where offset = ((`bridgeIndex` − 1) × 2) + 1) carries the router's **first** output (the lowest number in `routerOutputs`), its label and a poll.
- Per-output joins: output N of the router with `bridgeIndex` B is join **2601 + ((B − 1) × 64) + (N − 1)**. Each router holds up to 64 outputs, so bridgeIndex 1 uses 2601–2664, bridgeIndex 2 uses 2665–2728, and so on up to bridgeIndex 7 (2985–3048); higher bridgeIndex routers are logged and not linked (use the standalone bridge). The block sits between the room combiners and the dialers, within an EISC's 4000 joins.

Within the **Standalone** object, output N is analog **joinStart + N − 1** (route) and serial **joinStart + 100 + N − 1** (routed label), up to 64 outputs. Output 1 shares the old *Source Selection* join.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`IKeyed`** and **`IRoutingWithFeedback`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description            |
| ----------- | --------------- | --------- | ---------------------- |
| 150         | 1               | W         | Poll every output      |

#### Analogs

| Legacy Join                      | Standalone Join | Type (RW) | Description                                                        |
| -------------------------------- | --------------- | --------- | ------------------------------------------------------------------ |
| 150                              | 1               | RW        | First output: input to route (0 clears) and routed-input feedback  |
| 2601 + ((B − 1) × 64) + (N − 1)  | 1 + (N − 1)     | RW        | Output N: input to route (0 clears) and routed-input feedback      |

#### Serials

| Legacy Join                      | Standalone Join | Type (RW) | Description                                       |
| -------------------------------- | --------------- | --------- | ------------------------------------------------- |
| 150                              | 1               | R         | Control Label (Pass From Config)                  |
| N/A                              | 2               | R         | Source List XSIG (If Configured) or Routed Source |
| 2601 + ((B − 1) × 64) + (N − 1)  | 101 + (N − 1)   | R         | Output N: label of the routed input               |

#### Config Example

> All router configs must be part of a dictionary called **routerControlBlocks**.

```json
{
    "routerControlBlocks" : {
        "RouterControl01" : {
            "enabled" : true,
            "label" : "switcher01",
            "routerInstanceTag" : "Router1",
            "bridgeIndex" : 1,
            "showRoutedStringFeedback": true,
            "routerInputs" : {
                "1" : {"label": "Input1" },
                "2" : {"label": "Input2" }
            },
            "routerOutputs" : {
                "1" : {"label": "Output1" },
                "2" : {"label": "Output2" }
            }
        }
    }
}
```

#### Config Notes

> **enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**index1** - With the legacy `routerOutput` only: that output's Tesira number (default: the next number after `routerOutputs`).
**routerInstanceTag** - Instance tag of the router block
**bridgeIndex** - The index of the control on a **Legacy** object (counts from 1)
**routerInputs** - Labels for the router's inputs. The keys **must** be integers (Tesira input numbers). Only these inputs, and 0 (clear), can be routed.
**routerOutputs** - Labels for the router's outputs. The keys **must** be integers (Tesira output numbers, from 1). Each one is subscribed, routable and bridged.
**routerOutput** - Legacy single output (label only; its number is `index1`). It becomes an ordinary output alongside `routerOutputs`; prefer `routerOutputs`.

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--RouterControl01```.

***

### SourceSelector

Controls and subscribes to objects with the attribute type of `sourceSelector`.  

Within the **Legacy** object, this Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + fader ```bridgeIndex``` as defined by the config.

Within the **Standalone** object, this join map represents a single control as defined by the key in the bridge.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`IKeyed`** and **`IRoutingWithFeedback`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description |
| ----------- | --------------- | --------- | ----------- |
| 150         | 1               | W         | Poll        |


#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description      |
| ----------- | --------------- | --------- | ---------------- |
| 150         | 1               | RW        | Source Selection |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description                                       |
| ----------- | --------------- | --------- | ------------------------------------------------- |
| 150         | 1               | R         | Control Label (Pass From Config)                  |
| N/A         | 2               | R         | Source List XSIG (If Configured) or Routed Source |

#### Config Example

> All sourceSelector and router configs must be part of a dictionary called **routerControlBlocks**.

```json
{
    "sourceSelectorControlBlocks" : {
        "sourceSelectorControl01" : {
            "enabled" : true,
            "label" : "sourceSelector01",
            "index1" : 1,
            "sourceSelectorInstanceTag" : "SourceSelector1",
            "bridgeIndex" : 1,
            "showRoutedStringFeedback": true,
            "pollIntervalMs" : 90000,
            "sourceSelectorInputs" : {
                "1" : {"label": "Input1" },
                "2" : {"label": "Input2" },
            }
        }
    }
}
```

#### Config Notes

> **enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**index1** - Index of the output number on the sourceSelector that you wish to control.
**sourceSelectorInstanceTag** - Instance tag of the sourceSelection control
**bridgeIndex** - The index of the control on a **Legacy** object
**sourceSelectorInputs** - This is a dictionary of labels for inputs of the source selector block.  The keys **must** be integers.

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--sourceSelectorControl01```.

***

### LogicSelector

Controls a Tesira **Logic Selector** block as a switcher. A Logic Selector has interlocked boolean channels: selecting one turns the others off, and selecting none turns the active one off. It behaves exactly like a [SourceSelector](#sourceselector): the same interfaces (**`IKeyed`**, **`IRoutingWithFeedback`**), the same Legacy and Standalone joins, and the same routing and feedback. Selection `0` means no channel.

Logic selectors share the DSP's switcher joins with source selectors, so give each a `bridgeIndex` that doesn't clash with a source selector's.

The plugin subscribes once per block, to the `states` attribute, which the Tesira publishes as one line for all channels (`[false true false false]`). It selects a channel with `set state <channel> true`.

#### Config Example

> All logic selector configs must be part of a dictionary called **logicSelectorControlBlocks**.

```json
{
    "logicSelectorControlBlocks" : {
        "logicSelector01" : {
            "enabled" : true,
            "label" : "Logic Selector 1",
            "logicSelectorInstanceTag" : "LogicSelector1",
            "bridgeIndex" : 3,
            "logicSelectorInputs" : {
                "1" : {"label": "Channel 1" },
                "2" : {"label": "Channel 2" },
                "3" : {"label": "Channel 3" },
                "4" : {"label": "Channel 4" }
            }
        }
    }
}
```

#### Config Notes

> **logicSelectorInstanceTag** - instance tag of the Logic Selector block (`sourceSelectorInstanceTag` also works).
**logicSelectorInputs** - labels for the block's channels; the keys **must** be channel numbers starting at 1 (`sourceSelectorInputs` also works). A selected channel without a label shows as "Input *n*".
All other settings are as for a SourceSelector.

***

### State

Controls objects with the attribute type of "state" and subscribes to them as necessary.

Within the **Legacy** object, this Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + (```bridgeIndex``` − 1); `bridgeIndex` counts from 1, and a state with `bridgeIndex` 0 is logged and not linked.

Within the **Standalone** object, this join map represents a single control as defined by the key in the bridge.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interface **`IKeyed`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description  |
| ----------- | --------------- | --------- | ------------ |
| 1300        | 1               | RW        | State Toggle |
| 1450        | 2               | RW        | State On     |
| 1600        | 3               | RW        | State Off    |

#### Analogs

None

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description                      |
| ----------- | --------------- | --------- | -------------------------------- |
| 1300        | 1               | R         | Control Label (Pass From Config) |

#### Config Example

> All state configs must be part of a dictionary called **stateControlBlocks**.  

```json
{
    "stateControlBlocks" : {
        "StateControl1" : {
            "enabled" : true,
            "label" : "State01",
            "stateInstanceTag" : "LogicState1",
            "subscriptionInstanceTag" : "LogicMeter1",
            "index" : 1,
            "bridgeIndex" : 1
        }
    }
}
```

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--StateControl1```.

#### Config Notes

**enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**index** - Index of the control point.
**stateInstanceTag** - Instance tag of the state control
**subscriptionInstanceTag** - (Optional) Instance tag of the meter control for subscribing to feedback. If null or empty, will attempt to subscribe to stateInstanceTag
**bridgeIndex** - The index of the control on a **Legacy** object

***

### Presets

If you intend to ONLY do direct preset calling by string, this object is NOT required to recall presets. This activity is provided by the base level device object for the Tesira DSP. It is also provided by the **Standalone** object ```Presets```.  This config object is required regardless of control object if preset control iby index is required.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`IHasDspPresetSave`** (which extends `IDspPresets`) and **`IKeyed`**

By default, a preset's digital join recalls the preset on press. To save presets from the bridge, set `presetHoldTimeMs` on the DSP (see Config Notes). Then a short press recalls the preset on release, and holding the join for `presetHoldTimeMs` saves the DSP's current state to that preset. When the DSP confirms the save, the *Preset Saved* join pulses high for 2 seconds. A failed save (for example, `-ERR PRESET_NOT_FOUND`) is logged and does not pulse.

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description                                                        |
| ----------- | --------------- | --------- | ------------------------------------------------------------------ |
| 100         | 1               | W         | Select Preset By Index (with `presetHoldTimeMs`: press recalls, hold saves) |
| N/A         | 1               | R         | Preset is Available By Index                                       |
| 100         | 101             | R         | Preset Saved By Index: pulses high for 2 seconds when a save is confirmed |

#### Analogs

None

#### Serials

| Legacy Join | Join | Type (RW) | Description           |
| ----------- | ---- | --------- | --------------------- |
| 100         | 1    | R         | Preset Name by Index  |
| 100         | 1    | W         | Recall Preset By Name |

#### Config Example

> All preset configs must be part of a dictionary called **presets**.  


```json
{
    "presetHoldTimeMs" : 3000,
    "presets" : {
        "SomeUniqueKey": {
            "label" : "Default",
            "presetName" : "Default Levels",
            "presetId" : 1101,
            "presetIndex" : 1
        }
    }
}
```

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--Presets```.

#### Config Notes

**label** - Passed directly across the eisc as the *Label* value.
**presetName** - the actual name of the preset as defined in biamp software
**presetID** - the ID of the preset as defined in biamp software
**presetIndex** - the index of the preset for the digital press recall
**presetHoldTimeMs** - (optional, on the DSP, not per preset) milliseconds a preset join must be held to save that preset. `0` (the default) disables saving from the bridge, and the join recalls on press.

> If a `presetName` is defined, you don't need a `presetId` and vice versa.  One or the other will be fine.
> If you are utilizing the "select preset by name" methodology, no presets need be defined in config.

***

### Dialer

VoIP Controls are tested

POTS Controls are added, but as of yet untested.

DTMF is automatically managed based on current hook state.

Within the **Legacy** object, this Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + (1 + (50 * (n- 1))), where n is the index of the dialer as defined by ```bridgeIndex```.

For example, Incoming Call for Line 1 would be at join 3101, while the incoming call for Line 2 would be at join 3151.

Within the **Standalone** object, this join map represents a single control as defined by the key in the bridge.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`AudioCodecBase`** and **`IKeyed`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description           |
| ----------- | --------------- | --------- | --------------------- |
| 3100        | 1               | R         | Incoming Call         |
| 3106        | 2               | W         | Answer                |
| 3107        | 3               | W         | End Call              |
| 3110        | 4               | W         | Keypad 0              |
| 3111        | 5               | W         | Keypad 1              |
| 3112        | 6               | W         | Keypad 2              |
| 3113        | 7               | W         | Keypad 3              |
| 3114        | 8               | W         | Keypad 4              |
| 3115        | 9               | W         | Keypad 5              |
| 3116        | 10              | W         | Keypad 6              |
| 3117        | 11              | W         | Keypad 7              |
| 3118        | 12              | W         | Keypad 8              |
| 3119        | 13              | W         | Keypad 9              |
| 3120        | 14              | W         | Keypad *              |
| 3121        | 15              | W         | Keypad #              |
| 3122        | 16              | W         | Keypad Clear          |
| 3123        | 17              | W         | Keypad Backspace      |
| 3124        | 18              | RW        | Dial                  |
| 3125        | 19              | RW        | Auto Answer On        |
| 3126        | 20              | RW        | Auto Answer Off       |
| 3127        | 21              | RW        | Auto Answer Toggle    |
| 3129        | 22              | RW        | On Hook               |
| 3130        | 23              | RW        | Off Hook              |
| 3132        | 24              | RW        | Do Not Disturb Toggle |
| 3133        | 25              | RW        | Do Not Disturb On     |
| 3134        | 26              | RW        | Do Not Disturb Off    |

#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description     |
| ----------- | --------------- | --------- | --------------- |
| 3100        | 1               | R         | CallState Value |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description        |
| ----------- | --------------- | --------- | ------------------ |
| 3100        | 1               | RW        | Dial String        |
| 3101        | 2               | RW        | Dialer Label       |
| 3102        | 3               | RW        | Last Number Dialed |
| 3104        | 4               | R         | Caller ID Number   |
| 3105        | 5               | R         | Caller ID Name     |

#### Config Example

> All dialer configs must be part of a dictionary called **dialerControlBlocks**.  

```json
{
    "dialerControlBlocks" : {
        "Dialer1" : {
            "enabled" : true,
            "label" : "Dialer 01",
            "isVoip" : true,
            "dialerInstanceTag" : "Dialer1",
            "controlStatusInstanceTag" : "VoIPControlStatus1",
            "index" : 1,
            "callAppearance" : 1,
            "clearOnHangup" : true,
            "appendDtmf" : false,
            "bridgeIndex" : 1
        }
    }
}
```

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--Dialer1```.

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
**bridgeIndex** - The index of the control on a **Legacy** object

***

### Meter

Enables metering on the sepecified meter.  
Using this is a bad idea, please avoid unless specifically requested.  

Meters are opt-in: a meter streams only while something has asked for it. Raise the meter's subscribe join (or call `Subscribe()` on the meter device) to start it, and lower the join (or call `UnSubscribe()`) to stop it. A standalone program without a SIMPL bridge can set `"subscribeOnStart": true` on the meter instead, so it streams from session start; lowering the join or calling `UnSubscribe()` still stops it. A meter that was asked for is subscribed again after a reconnect, design push or resubscribe; one that wasn't stays off. Before v4, every meter was subscribed when the session started, whether or not anything used it. For level-threshold feedback, prefer a logic meter in the design, which publishes only when its state changes.  

Within the **Legacy** object this Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + (```bridgeIndex``` − 1); `bridgeIndex` counts from 1, and a meter with `bridgeIndex` 0 is logged and not linked.

Within the **Standalone** object, this join map represents a single control as defined by the key in the bridge.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interface **`IKeyed`**


#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description  |
| ----------- | --------------- | --------- | ------------ |
| 3501        | 1               | RW        | Meter subscribe: high subscribes, low unsubscribes (one join per meter); feedback: subscribed |

#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description    |
| ----------- | --------------- | --------- | -------------- |
| 3501        | 1               | R         | Meter Feedback |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description                      |
| ----------- | --------------- | --------- | -------------------------------- |
| 3501        | 1               | R         | Control Label (Pass From Config) |

#### Config Example

> All state configs must be part of a dictionary called **meterControlBlocks**.  

```json
"meterControlBlocks" : {
    "Meter1" : {
        "enabled" : true,
        "label" : "State01",
        "meterInstanceTag" : "Meter1",
        "index" : 1,
        "bridgeIndex" : 1,
        "subscribeOnStart" : false
    }
}
```

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--Meter1```.

#### Config Notes

**enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**meterInstanceTag** - Instance tag of the meter control
**index** - Index of the control point.
**bridgeIndex** - The index of the control on a **Legacy** object
**subscribeOnStart** - Optional, default `false`. `true` streams the meter from session start without a subscribe join or `Subscribe()` call, for standalone programs. The subscribe join and `UnSubscribe()` still stop it.

***

### Crosspoint State Control

Provides a control point for a single crosspoint on a MatrixMixer.  

Within the **Legacy** object, each crosspoint takes a block of 3 joins: join number = Join Map Number + (`bridgeIndex` − 1) × 3. `bridgeIndex` counts from 1; a crosspoint with `bridgeIndex` 0 is logged and not linked.

Within the **Standalone** object, the joins are numbered differently from the Legacy object (On, Off, Toggle), as listed below.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interface **`IKeyed`**


#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description                                  |
| ----------- | --------------- | --------- | -------------------------------------------- |
| 2001        | 3               | RW        | Crosspoint Toggle (feedback: crosspoint on)  |
| 2002        | 1               | RW        | Crosspoint On (feedback: crosspoint on)      |
| 2003        | 2               | RW        | Crosspoint Off (feedback: crosspoint off)    |

#### Analogs

None

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description                      |
| ----------- | --------------- | --------- | -------------------------------- |
| 2001        | 1               | R         | Control Label (Pass From Config) |

#### Config Example

> All state configs must be part of a dictionary called **crosspointStateControlBlocks**.  

```json
"crosspointStateControlBlocks" : {
    "Crosspoint1" : {
        "enabled" : true,
        "label" : "Crosspoint1-2",
        "matrixInstanceTag" : "Meter1",
        "index1" : 1,
        "index2" : 2,
        "bridgeIndex" : 1
    }
}
```

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--Crosspoint1```.

#### Config Notes

**enabled** - enables the control to be subscribed and controlled  
**label** - Passed directly across the eisc as the *Label* value  
**index1** - Input of the crosspoint to be controlled  
**index2** - Output of the crosspoint to be controlled  
**stateInstanceTag** - Instance tag of the meter block  
**bridgeIndex** - The index of the control on a **Legacy** object

***

### RoomCombiner

Controls Biamp Tesira RoomCombiner objects.  Control of LevelOut, MuteOut, and group selection is supported.

> MuteOut is **not** a subscribed control for this block.  Polling is simulated on each trigger of the mute, but there is no unsolicited feedback for mute status.

In order to keep the types of joins similar to a standard fader object, the choice was made to not provide direct control for wall states in this component.  If direct wall state control is required, utilize a `State Control` block.

Within the **Legacy** object, this Join map represents a control that is part of an array of controls.  Each join number = Join Map Number + roomCombiner ```bridgeIndex``` as defined by the config. Each combiner takes six joins (offset ((`bridgeIndex` − 1) × 6)); `bridgeIndex` 1–66 fits before the router joins at 2601, and a higher one is logged and not linked.

Within the **Standalone** object, this join map represents a single control as defined by the key in the bridge.

>This control implements [Essentials](https://github.com/PepperDash/Essentials) interfaces **`IBasicVolumeWithFeedback`** and **`IKeyed`**

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description     |
| ----------- | --------------- | --------- | --------------- |
| 2206        | 6               | R         | Channel Visible |
| 2203        | 5               | RW        | Mute Toggle     |
| 2204        | 3               | RW        | Mute On         |
| 2205        | 4               | RW        | Mute Off        |
| 2201        | 1               | W         | Volume Up       |
| 2202        | 2               | W         | Volume Down     |

#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description                                           |
| ----------- | --------------- | --------- | ----------------------------------------------------- |
| 2201        | 1               | RW        | Volume Level                                          |
| 2202        | 2               | R         | Icon (0 - Level, 1 - Mic)                             |
| 2204        | 3               | RW        | Set/Get the Combine Group Number for Room Combination |
| 2203        | 4               | R         | Permissions (Pass From Config)                        |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description                      |
| ----------- | --------------- | --------- | -------------------------------- |
| 2201        | 1               | R         | Control Label (Pass From Config) |

#### Config Example

> All RoomCombiner configs must be part of a dictionary called **roomCombinerControlBlocks**.  

```json
"roomCombinerControlBlocks": {
    "Room01": {
        "enabled": true,
        "label": "Room 1",
        "isMic": false,
        "roomIndex" : 1,
        "roomCombinerInstanceTag": "RoomCombiner1",
        "unmuteOnVolChange" : true,
        "incrementAmount" : "2.0",
        "permissions" : 0,
        "bridgeIndex" : 1
    }
```

#### Config Notes

**enabled** - enables the control to be subscribed and controlled.
**label** - Passed directly across the eisc as the *Label* value.
**isMic** - drives the *icon* feedback.
**roomIndex** - Index of the room on the combiner block.
**roomCombinerInstanceTag** - Instance tag of the room combiner control.
**unmuteOnVolChange** - if *true*, will unmute a muted control when the level increases.
**incrementAmount** - the value in decimals by which a mute increment or decrement command will manipulate the level.
**permissions** - Passed directly across the eisc as the *Permissions* value.
**bridgeIndex** - The index of the control on a **Legacy** object

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--Room01```.

***

### Tesira Expanders

Provides status updates for tesira expander devices related to health and device properties

Tesira Expander block is not supported in the Legacy bridge API.  In order to utilize this control, one must directly bridge the control using the key ```<dspKey>--Expanders```

Within the **Standalone** object, this join map represents each element in a list of Expander Devices

#### Digitals

| Legacy Join | Standalone Join | Type (RW) | Description   |
| ----------- | --------------- | --------- | ------------- |
| N/A         | 1               | R         | Device Online |

#### Analogs

| Legacy Join | Standalone Join | Type (RW) | Description |
| ----------- | --------------- | --------- | ----------- |
| N/A         | N/A             | N/A       | N/A         |

#### Serials

| Legacy Join | Standalone Join | Type (RW) | Description            |
| ----------- | --------------- | --------- | ---------------------- |
| N/A         | 1               | R         | Expander Hostname      |
| N/A         | 2               | R         | Expander Serial Number |
| N/A         | 3               | R         | Expander Firmware      |
| N/A         | 4               | R         | Expaner MAC Address    |

#### Config Example

> All Tesira Expander config objects must be part of a dictionary called **tesiraExpanderBlocks**.  

```json
"tesiraExpanderBlocks": {
    "Expander01": {
        "hostname" : "MIC-01"
        "index" : 1
    },
    "Expander02": {
        "hostname" : "AMP-01"
        "index" : 2
    }
}
```

#### Config Notes

**hostname** - The hostname of the expander to be tracked.  This value must match the configured expander
**index** - The index within the bridged control where this device ins mapped.  The joinmap of a device is equal to the join multipled by the index

In the provided example config object, given a base object key of ```dsp-1```, this control would have a standalone key of ```dsp-1--Expanders```.

***

## Full Example Essentials Device Config

>This config will create an internal loopback EISC on IPID D1 for a ssh-controlled tesira.

```json
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
                    "faderControlBlocks": {
                        "Fader1": {
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
                            "permissions" : 0,
                            "bridgeIndex" : 1

                        },
                        "Fader2": {
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
                            "permissions" : 1,
                            "bridgeIndex" : 2
                        },
                        "Fader3": {
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
                            "permissions" : 2,
                            "bridgeIndex" : 3
                        },
                        "Fader4": {
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
                            "permissions" : 0,
                            "bridgeIndex" : 4
                        }
                    },
                    "dialerControlBlocks" : {
                        "Dialer1" : {
                            "enabled" : true,
                            "label" : "Dialer 1",
                            "isVoip" : true,
                            "dialerInstanceTag" : "Dialer1",
                            "controlStatusInstanceTag" : "VoIPControlStatus1",
                            "index" : 1,
                            "callAppearance" : 1,
                            "clearOnHangup" : true,
                            "appendDtmf" : false
                            "bridgeIndex" : 1
                        }
                    },
                    "stateControlBlocks" : {
                        "State1" : {
                            "enabled" : true,
                            "label" : "State01",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 1,
                            "bridgeIndex" : 1
                        },
                        "State2" : {
                            "enabled" : true,
                            "label" : "State02",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 2,
                            "bridgeIndex" : 2
                        },
                        "State3" : {
                            "enabled" : true,
                            "label" : "State02",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 3,
                            "bridgeIndex" : 3
                        },
                        "State4" : {
                            "enabled" : true,
                            "label" : "State02",
                            "stateInstanceTag" : "LogicState1",
                            "index" : 4
                            "bridgeIndex" : 4
                        }
                    },
                    "switcherControlBlocks" : {
                        "switcherControl1" : {
                            "enabled" : true,
                            "label" : "switcher01",
                            "switcherInstanceTag" : "SourceSelector1",
                            "index1" : 1,
                            "bridgeIndex" : 1,
                            "switcherInputs" : {
                                "1" : {"label": "Input1" },
                                "2" : {"label": "Input2" },
                            },
                            "switcherOutputs" : {
                                "1" : {"label": "Output1" },
                                "2" : {"label": "Output2" },
                            }
                        }
                    },
                    "crosspointStateControlBlocks" : {
                        "Crosspoint1" : {
                            "enabled" : true,
                            "label" : "Crosspoint1-2",
                            "matrixInstanceTag" : "Meter1",
                            "index1" : 1,
                            "index2" : 2,
                            "bridgeIndex" : 1
                        }
                    },
                    "meterControlBlocks" : {
                        "Meter1" : {
                            "enabled" : true,
                            "label" : "State01",
                            "meterInstanceTag" : "Meter1",
                            "index" : 1,
                            "bridgeIndex" : 1
                        }
                    },
                    "roomCombinerControlBlocks": {
                        "Room01": {
                            "enabled": true,
                            "label": "Room 1",
                            "isMic": false,
                            "roomIndex" : 1,
                            "roomCombinerInstanceTag": "RoomCombiner1",
                            "unmuteOnVolChange" : true,
                            "incrementAmount" : "2.0",
                            "permissions" : 0,
                            "bridgeIndex" : 1
                        }
                     },
                    "presets" : {
                        "1": {
                            "label" : "Default",
                            "preset" : "Default Levels"
                            "presetIndex" : 1  
                        },
                        "2" : {
                            "label" : "High",
                            "Preset" : "Noise Reduction High"
                            "presetIndex" : 2
                        }
                    },
                    "tesiraExpanderBlocks": {
                        "Expander01": {
                            "hostname" : "MIC-01"
                            "index" : 1
                        },
                        "Expander02": {
                            "hostname" : "AMP-01"
                            "index" : 2
                        }
                    }
                }
            },
            {
                "key": "eisc-Dsp",
                "uid": 4,
                "name": "Bridge Dsp",
                "group": "api",
                "type": "eiscApiAdvanced",
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

***

## RoadMap

A Method by which we can continue even with *"bad"* instance tags in the config.  Currently, this causes everything to fail upon subscription.
<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 3.0.0
- 3.0.0
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "GeneratedKey",
    "uid": 1,
    "name": "GeneratedName",
    "type": "TesiraDspProperties",
    "group": "Group",
    "properties": {
        "CommunicationMonitorProperties": "SampleValue",
        "Control": "SampleValue",
        "faderControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "levelInstanceTag": "SampleString",
                "muteInstanceTag": "SampleString",
                "index1": 0,
                "index2": 0,
                "hasMute": true,
                "hasLevel": true,
                "isMic": true,
                "useAbsoluteValue": true,
                "unmuteOnVolChange": true,
                "incrementAmount": "SampleString",
                "permissions": 0,
                "bridgeIndex": "SampleValue",
                "volumeRepeatRateMs": 0,
                "volumeHoldTimeoutMs": 0
            }
        },
        "dialerControlBlocks": {
            "SampleString": {
                "enabled": true,
                "isVoip": true,
                "label": "SampleString",
                "displayNumber": "SampleString",
                "dialerInstanceTag": "SampleString",
                "controlStatusInstanceTag": "SampleString",
                "index": 0,
                "clearOnHangup": true,
                "appendDtmf": true,
                "redialEnable": true,
                "autoAnswerRingCount": "SampleString",
                "bridgeIndex": "SampleValue",
                "phonebook": {
                    "enabled": true,
                    "entryCount": 0
                },
                "callAppearances": {
                    "0": {
                        "label": "SampleString",
                        "bridgeIndex": "SampleValue"
                    }
                }
            }
        },
        "switcherControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "type": "SampleString",
                "switcherInstanceTag": "SampleString",
                "index1": 0,
                "bridgeIndex": "SampleValue",
                "switcherInputs": {
                    "SampleValue": {
                        "label": "SampleString"
                    }
                },
                "switcherOutputs": {
                    "SampleValue": {
                        "label": "SampleString"
                    }
                },
                "showRoutedStringFeedback": true,
                "pollIntervalMs": 0
            }
        },
        "routerControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "routerInstanceTag": "SampleString",
                "index1": 0,
                "routerInputs": {
                    "SampleValue": {
                        "label": "SampleString"
                    }
                },
                "routerOutput": {
                    "label": "SampleString"
                },
                "routerOutputs": {
                    "SampleValue": {
                        "label": "SampleString"
                    }
                },
                "showRoutedStringFeedback": true,
                "pollIntervalMs": 0,
                "bridgeIndex": "SampleValue"
            }
        },
        "sourceSelectorControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "type": "SampleString",
                "sourceSelectorInstanceTag": "SampleString",
                "sourceSelectorInputs": {
                    "SampleValue": {
                        "label": "SampleString"
                    }
                },
                "sourceSelectorOutput": {
                    "label": "SampleString"
                },
                "showSelectedStringFeedback": true,
                "bridgeIndex": "SampleValue"
            }
        },
        "logicSelectorControlBlocks": {
            "SampleString": {
                "logicSelectorInstanceTag": "SampleString",
                "logicSelectorInputs": {
                    "SampleValue": {
                        "label": "SampleString"
                    }
                }
            }
        },
        "presets": {
            "SampleString": {
                "label": "SampleString",
                "presetName": "SampleString",
                "presetId": 0,
                "presetIndex": 0
            }
        },
        "stateControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "stateInstanceTag": "SampleString",
                "subscriptionInstanceTag": "SampleString",
                "index": 0,
                "bridgeIndex": "SampleValue"
            }
        },
        "meterControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "meterInstanceTag": "SampleString",
                "index": 0,
                "meterData": {
                    "meterMinimum": 0,
                    "meterMaximum": 0,
                    "defaultPollTime": 0
                },
                "bridgeIndex": "SampleValue",
                "subscribeOnStart": true
            }
        },
        "logicMeterControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "meterInstanceTag": "SampleString",
                "index": 0,
                "bridgeIndex": "SampleValue"
            }
        },
        "crosspointStateControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "matrixInstanceTag": "SampleString",
                "index1": 0,
                "index2": 0,
                "bridgeIndex": "SampleValue",
                "pollEnable": true,
                "pollTimeMs": 0
            }
        },
        "roomCombinerControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "roomCombinerInstanceTag": "SampleString",
                "roomIndex": 0,
                "isMic": true,
                "useAbsoluteValue": true,
                "umuteOnVolChange": true,
                "incerementAmount": "SampleString",
                "permissions": 0,
                "bridgeIndex": "SampleValue",
                "volumeRepeatRateMs": 0,
                "volumeHoldTimeoutMs": 0
            }
        },
        "dtmfDecodeControlBlocks": {
            "SampleString": {
                "enabled": true,
                "label": "SampleString",
                "dtmfDecodeInstanceTag": "SampleString",
                "index": 0,
                "bridgeIndex": "SampleValue"
            }
        },
        "tesiraExpanderBlocks": {
            "SampleString": {
                "hostname": "SampleString",
                "index": 0
            }
        },
        "resubscribeString": "SampleString",
        "levelRangePollIntervalSecs": 0,
        "faultPollIntervalSecs": 0,
        "watchdogIntervalSecs": 0,
        "watchdogPatrolSize": 0,
        "watchdogSentinels": [
            "SampleString"
        ],
        "volumeHoldTimeoutMs": 0,
        "presetHoldTimeMs": 0
    }
}
```
<!-- END Config Example -->
<!-- START Supported Types -->

<!-- END Supported Types -->
<!-- START Join Maps -->
### Join Maps

#### Digitals

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Device Online |
| 3501 | R | High to Subscribe - Low to Unsubscribe |
| 150 | R | Poll Current Route |
| 1300 | R | State Toggle and Feedback |
| 1450 | R | State On and Feedback |
| 1600 | R | State On and Feedback |
| 2203 | R | Fader Mute Toggle and Feedback |
| 2204 | R | Fader Mute On and Feedback |
| 2205 | R | Fader Mute Off and Feedback |
| 2201 | R | Fader Level Increment |
| 2202 | R | Fader Level Decrement |
| 2206 | R | Fader Visible and In Use |
| 1 | R | Device Online |
| 1 | R | Trigger control resubscription |
| 11 | R | Fault monitoring: rising edge turns polling on (config interval, or 60 s), falling edge turns it off. Runtime only, never written to config. Feedback: polling active |
| 12 | R | Fault monitoring: high while the DSP reports any active fault |
| 13 | R | Fault monitoring: poll the active fault list once now |
| 100 | R | Recall Preset Explicitly by Configured Index |
| 100 | R | Preset saved: pulses high for 2 seconds when the DSP confirms a save (per configured index) |
| 100 | R | Recall Preset by name |
| 2001 | R | State Toggle and Feedback |
| 2002 | R | State On and Feedback |
| 2003 | R | State On and Feedback |
| 400 | R | Fader Mute Toggle and Feedback |
| 600 | R | Fader Mute On and Feedback |
| 800 | R | Fader Mute Off and Feedback |
| 1000 | R | Fader Level Increment |
| 1200 | R | Fader Level Decrement |
| 200 | R | Fader Visible and In Use |
| 1 | R | Recall Preset Explicitly by Configured Index |
| 1 | R | Preset Preset and Configured |
| 101 | R | Preset saved: pulses high for 2 seconds when the DSP confirms a save (per configured index) |
| 1 | R | Recall Preset by name |
| 1 | R | State On and Feedback |
| 2 | R | State On and Feedback |
| 3 | R | State Toggle and Feedback |
| 1 | R | Device Online |
| 1 | R | Trigger control resubscription |
| 11 | R | Fault monitoring: rising edge turns polling on (config interval, or 60 s), falling edge turns it off. Runtime only, never written to config. Feedback: polling active |
| 12 | R | Fault monitoring: high while the DSP reports any active fault |
| 13 | R | Fault monitoring: poll the active fault list once now |
| 1 | R | Fader Level Increment |
| 2 | R | Fader Level Decrement |
| 3 | R | Fader Mute On and Feedback |
| 4 | R | Fader Mute Off and Feedback |
| 5 | R | Fader Mute Toggle and Feedback |
| 6 | R | Fader Visible and In Use |
| 1 | R | High to Subscribe - Low to Unsubscribe |
| 1 | R | Poll Current Route |
| 1 | R | State Toggle and Feedback |
| 2 | R | State On and Feedback |
| 3 | R | State On and Feedback |
| 1 | R | Poll every output's route |

#### Analogs

| Join | Type (RW) | Description |
| --- | --- | --- |
| 3501 | R | Meter Data |
| 2601 | R | Router output: set the input routed to this output (0 clears) and feedback of the routed input |
| 150 | R | Source Selector Index Set and Feedback |
| 2201 | R | Fader Level Set and Feedback |
| 2202 | R | Fader Type [Speaker / Microphone] |
| 2204 | R | Fader Control Data [Mic / Speaker] |
| 2203 | R | Fader Permissions |
| 11 | R | Fault monitoring: number of active faults |
| 12 | R | Fault monitoring: worst indicator severity (0 none, 1 minor, 2 major, 3 critical) |
| 200 | R | Fader Level Set and Feedback |
| 400 | R | Fader Type [Speaker / Microphone] |
| 600 | R | Fader Control Data [Mic / Speaker] |
| 800 | R | Fader Permissions |
| 11 | R | Fault monitoring: number of active faults |
| 12 | R | Fault monitoring: worst indicator severity (0 none, 1 minor, 2 major, 3 critical) |
| 1 | R | Fader Level Set and Feedback |
| 2 | R | Fader Type [Speaker / Microphone] |
| 3 | R | Fader Permissions |
| 4 | R | Fader Control Data [Mic / Speaker] |
| 1 | R | Meter Data |
| 1 | R | Source Selector Index Set and Feedback |
| 1 | R | Route to the first output, set and feedback (same join as OutputRoute for output 1) |
| 1 | R | Router output: set the input routed to this output (0 clears) and feedback of the routed input |

#### Serials

| Join | Type (RW) | Description |
| --- | --- | --- |
| 1 | R | Device Hostname |
| 2 | R | Device Serial Number |
| 3 | R | Device Firmware |
| 4 | R | Device Mac Address |
| 3501 | R | Meter Label |
| 2601 | R | Router output: label of the routed input |
| 150 | R | Source Selector Label |
| 1300 | R | State Label |
| 2201 | R | Fader Label |
| 1 | R | Device Name |
| 2 | R | Pass discrete commands directly to/from the device |
| 3 | R | Serial Number of the Device |
| 4 | R | Firmware of the Device |
| 5 | R | Hostname of the Device |
| 6 | R | IP Address of the Device |
| 7 | R | Mac Address of the Device |
| 11 | R | Fault monitoring: worst indicator id, e.g. INDICATOR_MAJOR_IN_DEVICE |
| 12 | R | Fault monitoring: worst indicator name, e.g. Major Fault in Device |
| 13 | R | Fault monitoring: active fault names, comma-separated |
| 100 | R | Preset Labels as configured for explicit preset recall |
| 2001 | R | State Label |
| 200 | R | Fader Label |
| 400 | R | Fader Raw Level with dB suffix |
| 1 | R | Preset Labels as configured for explicit preset recall |
| 1 | R | State Label |
| 1 | R | Device Name |
| 2 | R | Pass discrete commands directly to/from the device |
| 3 | R | Serial Number of the Device |
| 4 | R | Firmware of the Device |
| 5 | R | Hostname of the Device |
| 6 | R | IP Address of the Device |
| 7 | R | Mac Address of the Device |
| 11 | R | Fault monitoring: worst indicator id, e.g. INDICATOR_MAJOR_IN_DEVICE |
| 12 | R | Fault monitoring: worst indicator name, e.g. Major Fault in Device |
| 13 | R | Fault monitoring: active fault names, comma-separated |
| 1 | R | Fader Label |
| 1 | R | Meter Label |
| 1 | R | Source Selector Label |
| 2 | R | Source List XSIG -or- Routed String |
| 1 | R | State Label |
| 1 | R | Router Label |
| 2 | R | Source List XSIG -or- Routed String |
| 101 | R | Router output: label of the routed input |
<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- IRoutingWithFeedback
- IHasNamedRoutingSlots
- IDtmfDecode
- IHasFeedback
- IStateFeedback
- ISubscribedComponent
- IPrivacy
- IHasStateControlWithFeedback
- IMeterFeedback
- IBasicVolumeWithFeedbackAdvanced
- IVolumeComponent
- IHasDspPresetSave // extends IDspPresets
- IBasicVolumeWithFeedback
- IDeviceInfoProvider
- IDisposable
- IHasDspPresetSave
- ICommunicationMonitor
- IRoutingSlotInfo
- IAudioCodecPhonebook
- IJoinCalls
- IBridgeAdvanced
- IKeyName
- IOnline
- IHasInputs<string>
- IRoutingInputs
- IRoutingOutputs
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- TesiraDspControlPoint
- EssentialsBridgeableDevice
- TesiraSourceSelectorControlBlockConfig
- // extends IDspPresets
        IDspPresetsDirectRecall
- TesiraDspSourceSelector
- StatusMonitorBase
- JoinMapBaseAdvanced
- MessengerBase
- TesiraDspDialerControlPoint
- AudioCodecBase
- EssentialsDevice
- System.IDisposable
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void SetSource(int data)
- public void SetDestination(int data)
- public void GetSourceNames()
- public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
- public void ClearRoute(object outputSelector, eRoutingSignalType signalType)
- public void GetState()
- public void StateOn()
- public void StateOff()
- public void StateToggle()
- public void PrivacyModeOff()
- public void PrivacyModeOn()
- public void PrivacyModeToggle()
- public bool RestoreSubscription()
- public void UnSubscribe()
- public void SetSubscribed(bool subscribe)
- public bool IsQuarantined(T component)
- public bool Quarantine(T component)
- public bool Release(T component)
- public void ClearQuarantine()
- public bool Contains(TComponent component)
- public void Begin(IEnumerable<TComponent> components)
- public void ExpectReply(TComponent component, TCommand command)
- public void MarkAttempted(TComponent component)
- public bool EndCapture()
- public bool EndCapture(out IReadOnlyList<TComponent> healthySenders)
- public void Close()
- public bool Tick()
- public void Reset()
- public void MuteOff()
- public void MuteOn()
- public void SetVolume(ushort level)
- public void GetVolume()
- public void GetMinLevel()
- public void GetMaxLevel()
- public void GetMute()
- public void MuteToggle()
- public void VolumeDown(bool press)
- public void VolumeUp(bool press)
- public void RunPresetNumber(ushort n)
- public void RunPreset(string name)
- public void RunPreset(int id)
- public void RecallPreset(string key)
- public void SavePreset(string key)
- public void GetState()
- public void StateOn()
- public void StateOff()
- public void StateToggle()
- public void MuteOff()
- public void MuteOn()
- public void SetVolume(ushort level)
- public void SetRoomGroup(ushort group)
- public void GetVolume()
- public void GetMinLevel()
- public void GetMaxLevel()
- public void GetMute()
- public void GetRoomGroup()
- public void MuteToggle()
- public void VolumeDown(bool press)
- public void VolumeUp(bool press)
- public void GetDeviceInfo()
- public void OnDeviceInfoChanged()
- public void UpdateDeviceInfo()
- public void Set(bool press)
- public void Press()
- public void Release()
- public void Cancel()
- public void Dispose()
- public void Start()
- public void Stop()
- public void SetPolling(int intervalSecs)
- public void PollNow()
- public void LinkFaultJoins(BasicTriList trilist, JoinDataComplete enable, JoinDataComplete hasFault,
            JoinDataComplete pollNow, JoinDataComplete count, JoinDataComplete severity,
            JoinDataComplete faultId, JoinDataComplete faultName, JoinDataComplete faultList)
- public void SetFaultPolling(int intervalSecs)
- public void PollFaults()
- public void SendLine(string s, bool bypassTxQueue = false)
- public void SendLineRaw(string s, bool bypassTxQueue = false)
- public void RunPresetNumber(ushort n)
- public void EnqueuePassthru(string data)
- public void RunPreset(string name)
- public void RunPreset(int id)
- public void RecallPreset(string key)
- public void SavePreset(string key)
- public void Resubscribe()
- public void UpdateDeviceInfo()
- public void SetSource(int data)
- public void GetSourceNames()
- public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
- public void ClearRoute(object outputSelector, eRoutingSignalType signalType)
- public void Pulse()
- public void Dispose()
- public void MarkNeeded(string key, bool fullSetup)
- public void MarkRestoreSent(string key)
- public void ExpectSubscribe(string key)
- public void Acknowledge(string key, bool isSubscribe)
- public void Fail(string key)
- public bool IsRestored(string key)
- public bool IsPending(string key)
- public bool NeedsFullSetup(string key)
- public bool Complete(string key)
- public void Clear()
- public void Set(bool isUp, bool press)
- public void Press(bool isUp)
- public void Release(bool isUp)
- public void Cancel()
- public void Dispose()
- public void Dispose()
- public void PollingTurnedOff()
- public bool IsFaultReply(string queuedAttributeCode)
- public bool ShouldApply(string queuedAttributeCode)
- public bool MarkUnavailable(string key, string target)
- public bool MarkAvailable(string key, string target, out bool targetRecovered)
- public bool Contains(string key)
- public void Clear()
- public object ReadValue()
- public uint GetOutputRoute(uint output)
- public void SetSource(int data)
- public void RouteOutput(uint output, uint input)
- public void SetDestination(int data)
- public void GetSourceNames()
- public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
- public void ClearRoute(object outputSelector, eRoutingSignalType signalType)
- public void HandleRouteChange(
            RoutingOutputPort outputPort,
            RoutingInputPort inputPort,
            eRoutingSignalType signalType)
- public void OnAppearanceCallStatusChanged(TesiraCallAppearance appearance)
- public void Conference(int callAppearance = 0)
- public void ConferenceAllActive()
- public void LeaveConference(int callAppearance = 0)
- public void JoinCall(CodecActiveCallItem activeCall)
- public void JoinAllCalls()
- public void SubscribeOnly()
- public void PollPhonebook()
- public void SetPhonebookEntry(int index, string name, string number)
- public void DialPhonebookEntry(int index)
- public void Dial(string number, int callAppearance = 0)
- public void EndCall(int callAppearance = 0)
- public void Hold(int callAppearance = 0)
- public void Resume(int callAppearance = 0)
- public void Redial(int callAppearance = 0)
- public void Flash()
- public void AutoAnswerOn()
- public void AutoAnswerOff()
- public void AutoAnswerToggle()
- public void DoNotDisturbOn()
- public void DoNotDisturbOff()
- public void DoNotDisturbToggle()
- public void Subscribe()
- public void Unsubscribe()
- public void ParseGetMessage(string attributeCode, string message)
- public void SetOffHookFromHookState(bool offHook)
- public void SetDialString(string data)
- public void Dial()
- public void Dial(string number)
- public void OnHook()
- public void OffHook()
- public void Answer()
- public void End()
- public void HoldCall()
- public void ResumeCall()
- public void HoldToggle()
- public void Conference()
- public void LeaveConference()
- public void Redial()
- public void Flash()
- public void SendDtmf(string digit)
- public void SendKeypad(TesiraDspDialer.EKeypadKeys data)
- public void SetData(string data, string macData)
- public void UpdateDeviceInfo()
- public bool SetLevel(ushort level)
- public bool SetMute(bool muted)
- public bool Step(bool up, ushort rawStep)
- public bool Step(bool up, ushort rawStep, out bool pastEnd)
- public void FireUpdate()
- public void Select()
- public void FireUpdate()
- public void RunPreset(string name)
- public void RecallPreset(string key)
- public void SavePreset(string key)
- public void UpdateDeviceInfo()
- public void SetIsOnline(bool online)
- public void SetCommStatus(string status)
- public void SetStatus(MonitorStatus status)
- public void Refresh()
- public bool HasOutput(uint output)
- public bool HasInput(uint input)
- public uint Get(uint output)
- public RouteResult Route(uint output, uint input)
- public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
- public void ClearRoute(object outputSelector, eRoutingSignalType signalType)
- public void SimulateRoute(uint output, uint input)
- public uint GetOutputRoute(uint output)
- public void ReportRoutes()
- public void FireUpdate()
- public void ForcePublishState()
- public void SetVolume(ushort level)
- public void VolumeUp(bool pressRelease)
- public void VolumeDown(bool pressRelease)
- public void MuteOn()
- public void MuteOff()
- public void MuteToggle()
- public void ForceFireUpdate()
- public void ForceFireUpdate()
- public void Add(object key)
- public void Remove(object key)
- public bool Has(object key)
- public void Clear()
- public bool Enqueue(T item, bool isBackground)
- public bool TryDequeue(out T item)
- public void Clear()
- public bool IsEchoOfOutstanding(string line)
- public void HandleResponse(string response)
- public void EnqueueCommand(QueuedCommand commandToEnqueue)
- public void EnqueueCommand(string command, bool sendLineRaw = false, int priority = (int)
- public void SendNextQueuedCommand()
- public void Dispose()
- public void Clear()
- public void ClearPending()
- public bool HasPendingLevelChange(ISubscribedComponent controlPoint)
- public void Enqueue(T item, int priority)
- public bool TryDequeue(out T item)
- public bool TryPeek(out T item)
- public void Clear()
- public void Tracks_Each_Key_Separately()
- public void Stays_Pending_Until_Every_Add_Is_Removed()
- public void Remove_Without_Add_Is_Ignored()
- public void Clear_Drops_Everything()
- public void Matches_Only_The_Verb_After_The_Instance_Tag(string? command, bool expected)
- public void TryGetRawLevelTarget_Finds_The_Tag_Attribute_And_Channel(string? command, string? expectedTag, string? expectedAttribute, int expectedIndex)
- public void RawKey_Is_Case_Insensitive_And_Per_Channel()
- public void Slot_key_resolves_to_the_inputs_index()
- public void Existing_callers_selector_still_resolves()
- public void Clear_route_sentinel_resolves_to_zero()
- public void Unknown_label_is_rejected_rather_than_sent_to_the_dsp()
- public void Null_selector_is_rejected()
- public void Null_input_selector_is_input_zero_so_a_cleared_route_reaches_the_dsp()
- public void Input_resolve_still_maps_slot_keys_and_rejects_unknown_labels()
- public void Untyped_resolve_passes_a_non_string_through()
- public void Classify_Matches_Hardware_Replies(string? response, ProbeReply expected)
- public void IsSubscribeCommand(string? command, bool expected)
- public void Default_Sentinel_Is_The_First_Candidate_And_The_Patrol_Rotates()
- public void Configured_Sentinels_Are_Probed_Every_Tick_And_Left_Out_Of_The_Patrol()
- public void Sentinels_That_Are_Not_Candidates_Fall_Back_To_The_First_Candidate()
- public void Quarantined_Components_Leave_The_Patrol_And_One_Is_Probed_Last()
- public void Quarantined_Probe_Rotates_One_Per_Tick()
- public void All_Quarantined_Still_Probes_One_So_A_Returning_Design_Is_Seen()
- public void Quarantined_Snapshot_And_Clear()
- public void Release_Returns_One_Component_To_The_Patrol()
- public void No_Candidates_Means_Nothing_To_Probe()
- public void Patrol_Smaller_Than_Size_Takes_What_There_Is()
- public void Closes_When_Every_Expected_Reply_Is_Healthy()
- public void Sender_Is_Healthy_Only_After_Its_Last_Reply_And_All_Healthy()
- public void A_Healthy_Reply_During_Capture_Does_Not_Settle_A_Sender_Still_Queueing()
- public void Senders_Answered_Healthy_During_Capture_Are_Reported_When_It_Ends()
- public void A_Rejected_Subscribe_Keeps_The_Sender_Unhealthy(bool replyDuringCapture)
- public void One_Config_Error_Keeps_The_Sender_Unhealthy_In_Either_Order(bool missingMuteFirst)
- public void A_Lost_Reply_Closes_The_Batch_At_Once()
- public void A_Subscribe_Queued_Before_The_Probe_Does_Not_Take_The_Probes_Place()
- public void Commands_From_Non_Members_Are_Not_Expected()
- public void Commands_Queued_After_Capture_Ends_Are_Not_Expected()
- public void A_Batch_Where_Nothing_Was_Queued_Closes_At_End_Of_Capture()
- public void Reply_Arriving_During_Capture_Does_Not_Close_Early()
- public void Reports_Members_Whose_Probe_Queued_Nothing()
- public void A_Probe_Rejected_By_A_Full_Queue_Is_Not_Reported_As_Silent()
- public void Contains_Is_False_Once_Closed()
- public void IsBackground_Puts_Only_Subscriptions_In_The_Background(string? command, bool expected)
- public void IsReadOrSubscription_Excludes_Writes(string? command, bool expected)
- public void Foreground_Items_Come_Out_First_Each_Lane_In_Order()
- public void A_Foreground_Item_Queued_Mid_Burst_Goes_Next()
- public void A_Steady_Foreground_Stream_Still_Lets_Background_Items_Through()
- public void A_Foreground_Run_With_Nothing_Waiting_Does_Not_Count()
- public void Capacity_Counts_Both_Lanes()
- public void Clear_Empties_Both_Lanes()
- public void SetCommand_Quotes_The_Tag_And_Sends_Output_Then_Input(uint output, uint input, string expected)
- public void Get_Subscribe_And_Unsubscribe_Name_The_Output()
- public void TryParseSubscriptionOutput_Reads_The_Output_From_The_Name(string? name, bool ok, uint output)
- public void SubscriptionName_Round_Trips()
- public void GetAttributeCode_Round_Trips()
- public void TryParseInput_Accepts_A_Single_Value(string? value, bool ok, uint input)
- public void Reports_Each_Target_Becoming_Unavailable_Once()
- public void Stays_Unavailable_Until_Every_Unavailable_Target_Answers()
- public void Reports_A_Target_Recovering_While_Another_Is_Still_Down()
- public void A_Successful_Reply_From_An_Available_Control_Changes_Nothing()
- public void Lists_Keys_Sorted_And_Ignores_Empty_Keys()
- public void Short_Press_Recalls_On_Release()
- public void Hold_Saves_And_The_Release_Does_Nothing()
- public void Hold_Timer_Firing_After_Release_Does_Not_Save()
- public void Stale_Hold_Callback_From_An_Earlier_Press_Is_Ignored()
- public void Cancel_Drops_A_Pending_Press_Without_Recall_Or_Save()
- public void Release_Without_Press_Does_Nothing()
- public void Each_Press_Gets_Exactly_One_Action()
- public void Real_Timer_Saves_After_The_Hold_Time()
- public void ActiveChannel_Returns_The_1_Based_True_Channel_Or_0(string? value, int expected)
- public void ChannelCount_Counts_The_Channels(string? value, int expected)
- public void Applies_Replies_Sent_Since_The_Last_Turn_Off()
- public void Drops_A_Periodic_Reply_That_Arrives_After_Polling_Is_Turned_Off()
- public void A_Stale_Periodic_Reply_Does_Not_Take_A_Later_One_Shots_Place()
- public void Applies_Every_One_Shot_While_Off()
- public void Drops_A_One_Shot_Sent_Before_Polling_Is_Turned_Off()
- public void Recognises_Its_Own_Tags(string? queued, bool expected)
- public void Normalize_Clamps_To_Ten_Seconds_Through_One_Day(int requested, int expected)
- public void The_Maximum_Fits_The_Timer()
- public void Defaults_without_a_deviceInfo_block_or_control_address()
- public void Control_address_from_a_reused_real_config_is_the_ip()
- public void Configured_values_win_and_blanks_fall_back()
- public void Doubles_The_Gap_Up_To_The_Maximum()
- public void Reset_Starts_Over()
- public void A_Maximum_Below_One_Retries_Every_Tick()
- public void A_Failed_Setup_Write_Is_Not_Undone_By_A_Later_Subscribe_Reply()
- public void Keeps_A_Control_Pending_Until_It_Is_Completed()
- public void Full_Setup_Once_Asked_For_Stays_Asked_For()
- public void A_Runtime_Outage_Needs_Subscriptions_Only()
- public void Full_Setup_Is_Forgotten_Once_Restored()
- public void Lists_Keys_Sorted_And_Ignores_Empty_Keys()
- public void A_Restore_Counts_Only_Once_Acknowledged()
- public void A_Success_Before_Any_Restore_Was_Sent_Does_Not_Count()
- public void A_Failed_Restore_Command_Undoes_It_Even_After_A_Success()
- public void An_Unavailable_Reply_After_The_Restore_Undoes_It()
- public void A_Rejected_Or_Discarded_Restore_Stays_Pending()
- public void Unknown_Keys_Are_Ignored()
- public void Once_A_Subscribe_Is_Queued_Only_A_Subscribe_Reply_Acknowledges()
- public void A_Read_Answered_Before_The_Subscribe_Was_Queued_No_Longer_Counts()
- public void A_Polled_Control_Is_Acknowledged_By_Its_State_Read()
- public void A_Subscribe_Expected_Before_Any_Restore_Is_Ignored()
- public void IsError_Recognizes_Every_Ttp_Error_Token(string? line, bool expected)
- public void IsUnavailable_Is_Only_Cannot_Deliver(string? line, bool expected)
- public void TargetOf_Returns_The_Commands_Instance_Tag(string? command, string expected)
- public void IsEchoOf_Matches_The_Command_Text_Only(string? line, string? command, bool expected)
- public void A_Signed_Tag_Echo_Looks_Like_An_Error_So_The_Echo_Check_Must_Come_First()
- public void VerbOf_Returns_The_Word_After_The_Instance_Tag(string? command, string expected)
- public void IsSuccess_Is_Ok_Or_Already_Subscribed(string? line, bool expected)
- public void Every_Output_Starts_Cleared()
- public void Routes_Each_Output_Independently()
- public void Input_Zero_Clears_Only_That_Output()
- public void The_Same_Route_Again_Is_Unchanged()
- public void Refuses_An_Unconfigured_Output_Or_Input()
- public void Snapshot_Is_A_Copy()
- public void Config_Class_Exists()
- public void Config_Block_Class_Exists_And_Is_Constructible(string className)
- public void Config_Property_Has_JsonPropertyAttribute(string jsonName)
- public void ExpanderBlock_Property_Has_JsonPropertyAttribute(string jsonName)
- public void MeterBlock_Property_Has_JsonPropertyAttribute(string jsonName)
- public void Block_Property_Is_Dictionary_Of_Expected_Value(string propertyName, string expectedValueType)
- public void Assembly_Loads_Successfully()
- public void Assembly_Name_Is_Expected()
- public void Factory_Count_Is_One()
- public void MockFactory_Exists()
- public void TesiraFactory_Exists()
- public void Factory_Has_Parameterless_Constructor()
- public void MockRouter_Implements_The_Real_Routers_Interfaces(string interfaceName)
- public void MockConfig_Takes_The_Real_RouterControlBlocks_Format()
- public void MockRouter_Has_Its_Bench_And_Routing_Methods(string method, int parameterCount)
- public void Press_Sends_One_Step_And_Arms_Hold_Timer()
- public void Delay_Then_Repeat_Steps_While_Held_Without_New_Up_Press()
- public void Callbacks_Queued_Before_Release_Do_Nothing_After_Release()
- public void Release_While_A_Step_Is_Being_Sent_Does_Not_Rearm_The_Repeat()
- public void Stale_Delay_Callback_Does_Nothing_After_Release_And_Repress()
- public void Releasing_One_Direction_Keeps_The_Other_Hold_Timeout_Armed()
- public void Hold_Timeout_Releases_Only_Its_Direction()
- public void Stale_Hold_Callback_Does_Not_Release_A_Newer_Press()
- public void Cancel_Stops_Repeats_In_Both_Directions_But_Keeps_Hold_Timers()
- public void Zero_Hold_Timeout_Disables_The_Safety_Stop()
- public void Real_Timers_Repeat_Then_Stop_On_Release()
- public void Parses_The_Hardware_Reply()
- public void Parses_The_Terse_Hardware_Reply_Like_The_Verbose_One()
- public void Terse_Indicators_Are_Named_From_Their_Text(string reply, int severity, string id)
- public void No_Fault_Reports_The_None_Indicator_Like_UDM()
- public void Worst_Indicator_Wins_And_Faults_Are_Counted_Across_Indicators()
- public void Signature_Changes_Only_With_The_Active_Faults()
- public void Returns_Null_For_Anything_But_A_Value_List(string? reply)
- public void Unknown_Indicator_With_Faults_Counts_As_A_Fault()
- public void Empty_List_Is_No_Fault()
- public void Factory_Sets_MinimumEssentialsFrameworkVersion_To_3_0_0()
- public void Factory_Sets_TypeNames()
- public void Factory_Source_Contains_TypeName(string typeName)
- public void No_Duplicate_TypeNames_In_Factory_Source()
- public void Level_and_mute_block_changes_both()
- public void Unchanged_values_report_no_change()
- public void Mute_only_block_keeps_level_zero_and_ignores_level_changes()
- public void Level_only_block_stays_unmuted_and_ignores_mute_changes()
- public void Percent_scales_to_raw(ushort percent, ushort raw)
- public void Fader_config_defaults_to_level_and_mute()
- public void Fader_config_reads_the_real_fader_flags()
- public void Step_is_incrementAmount_dB_over_the_range(string amount, double min, double max, ushort raw)
- public void No_usable_amount_keeps_the_legacy_step(string amount)
- public void An_empty_or_inverted_range_keeps_the_legacy_step()
- public void Steps_stop_at_the_ends_of_the_range()
- public void A_mute_only_block_never_steps()
- public void Fader_config_reads_the_real_volume_keys_with_real_defaults()
- public void A_reused_real_fader_config_with_a_null_hold_timeout_deserializes()
- public void The_device_hold_timeout_defaults_as_the_real_driver()
- public void Concurrent_steps_are_not_lost()
- public void A_step_past_an_end_lands_on_it_and_reports_it()
- public void A_mute_only_block_reports_no_past_end_step()
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- StateFeedback
- SubscribedFeedback
- StateFeedback
- PrivacyModeIsOnFeedback
- SubscribedFeedback
- MuteFeedback
- VisibleFeedback
- CrosspointStateFeedback
- MuteFeedback
- VisibleFeedback
- PollingEnabledFeedback
- HasFaultFeedback
- Feedback
- AutoAnswerFeedback
- DoNotDisturbFeedback
- ConferenceActiveFeedback
- LineReadyFeedback
- DialToneDetectedFeedback
- BusyToneDetectedFeedback
- RingBackToneDetectedFeedback
- RingDetectedFeedback
- DialingFeedback
- OffHookFeedback
- IncomingCallFeedback
- HoldCallFeedback
- InConferenceFeedback
- IsOnline
- MuteFeedback
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->
### Int Feedbacks

- SourceIndexFeedback
- MeterFeedback
- MeterFeedback
- VolumeLevelFeedback
- TypeFeedback
- ControlTypeFeedback
- PermissionsFeedback
- VolumeLevelFeedback
- TypeFeedback
- PermissionsFeedback
- RoomGroupFeedback
- ActiveFaultCountFeedback
- FaultSeverityFeedback
- SourceIndexFeedback
- SourceIndexFeedback
- ConferenceCountFeedback
- CallStateFeedback
- SourceIndexFeedback
- VolumeLevelFeedback
<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->
### String Feedbacks

- SourceNamesFeedback
- RoutedSourceNameFeedback
- LastDetectedToneFeedback
- NameFeedback
- RawLevelFeedback
- IpAddressFeedback
- HostnameFeedback
- SerialNumberFeedback
- FirmwareFeedback
- MacAddressFeedback
- MakeFeedback
- ModelFeedback
- LabelFeedback
- FaultIdFeedback
- FaultNameFeedback
- FaultListFeedback
- CommandPassthruFeedback
- SourceNamesFeedback
- RoutedSourceNameFeedback
- SourceNamesFeedback
- RoutedSourceNameFeedback
- LastDialedFeedback
- NameFeedback
- DisplayNumberFeedback
- DialStringFeedback
- CallerIdNumberFeedback
- CallerIdNameFeedback
- CallerIdTimestampFeedback
- NameFeedback
- RoutedSourceNameFeedback
<!-- END String Feedbacks -->
