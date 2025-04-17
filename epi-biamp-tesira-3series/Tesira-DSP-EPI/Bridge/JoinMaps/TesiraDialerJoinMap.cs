using PepperDash.Essentials.Core;


namespace Tesira_DSP_EPI.Bridge.JoinMaps
{
    /// <summary>
    /// Dialer Joinmap for Advanced Bridge - Meant for holistic DSP Object
    /// </summary>
    public class TesiraDialerJoinMapAdvanced : JoinMapBaseAdvanced
    {
        [JoinName("IncomingCall")] public JoinDataComplete IncomingCall =
            new JoinDataComplete(new JoinData {JoinNumber = 3100, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Call Incoming",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Answer")] public JoinDataComplete Answer =
            new JoinDataComplete(new JoinData {JoinNumber = 3106, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Answer Incoming Call",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("EndCall")] public JoinDataComplete EndCall =
            new JoinDataComplete(new JoinData {JoinNumber = 3107, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "End Call",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadNumeric")] public JoinDataComplete KeyPadNumeric =
            new JoinDataComplete(new JoinData {JoinNumber = 3110, JoinSpan = 10},
                new JoinMetadata
                {
                    Description = "Keypad Digits 0-9",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadStar")] public JoinDataComplete KeyPadStar =
            new JoinDataComplete(new JoinData {JoinNumber = 3120, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad *",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadPound")] public JoinDataComplete KeyPadPound =
            new JoinDataComplete(new JoinData {JoinNumber = 3121, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad #",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadClear")] public JoinDataComplete KeyPadClear =
            new JoinDataComplete(new JoinData {JoinNumber = 3122, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad Clear",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadBackspace")] public JoinDataComplete KeyPadBackspace =
            new JoinDataComplete(new JoinData {JoinNumber = 3123, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad Backspace",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadDial")] public JoinDataComplete KeyPadDial =
            new JoinDataComplete(new JoinData {JoinNumber = 3124, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad Dial and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerOn")] public JoinDataComplete AutoAnswerOn =
            new JoinDataComplete(new JoinData {JoinNumber = 3125, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Auto Answer On and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerOff")] public JoinDataComplete AutoAnswerOff =
            new JoinDataComplete(new JoinData {JoinNumber = 3126, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Auto Answer Off and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerToggle")] public JoinDataComplete AutoAnswerToggle =
            new JoinDataComplete(new JoinData {JoinNumber = 3127, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Auto Answer Toggle and On Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OnHook")] public JoinDataComplete OnHook =
            new JoinDataComplete(new JoinData {JoinNumber = 3129, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "On Hook Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OffHook")] public JoinDataComplete OffHook =
            new JoinDataComplete(new JoinData {JoinNumber = 3130, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Off Hook Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DoNotDisturbToggle")] public JoinDataComplete DoNotDisturbToggle =
            new JoinDataComplete(new JoinData {JoinNumber = 3132, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Do Not Disturb Toggle and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DoNotDisturbOn")] public JoinDataComplete DoNotDisturbOn =
            new JoinDataComplete(new JoinData {JoinNumber = 3133, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Do Not Disturb On Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DoNotDisturbOff")] public JoinDataComplete DoNotDisturbOff =
            new JoinDataComplete(new JoinData {JoinNumber = 3134, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Do Not Disturb Of Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });
        [JoinName("HoldToggle")]
        public JoinDataComplete HoldToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 3135, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Hold Toggle and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("HoldCall")]
        public JoinDataComplete HoldCall =
            new JoinDataComplete(new JoinData { JoinNumber = 3136, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Hold Call Set and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ResumeCall")]
        public JoinDataComplete ResumeCall =
            new JoinDataComplete(new JoinData { JoinNumber = 3137, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Resume Call Set and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });



        [JoinName("CallState")] public JoinDataComplete CallState =
            new JoinDataComplete(new JoinData {JoinNumber = 3100, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Call State Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("DialString")] public JoinDataComplete DialString =
            new JoinDataComplete(new JoinData {JoinNumber = 3100, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Dial String Send and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("Label")] public JoinDataComplete Label =
            new JoinDataComplete(new JoinData {JoinNumber = 3101, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Dialer Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("LastNumberDialerFb")] public JoinDataComplete LastNumberDialerFb =
            new JoinDataComplete(new JoinData {JoinNumber = 3102, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Last Number Dialed Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("CallerIdNumberFb")] public JoinDataComplete CallerIdNumberFb =
            new JoinDataComplete(new JoinData {JoinNumber = 3104, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Caller ID Number",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("CallerIdNameFb")] public JoinDataComplete CallerIdNameFb =
            new JoinDataComplete(new JoinData {JoinNumber = 3105, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Caller ID Name",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("DisplayNumber")] public JoinDataComplete DisplayNumber =
            new JoinDataComplete(new JoinData {JoinNumber = 3106, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "This Line's Number",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });


        public TesiraDialerJoinMapAdvanced(uint joinStart)
            : base(joinStart, typeof (TesiraDialerJoinMapAdvanced))
        {
        }
    }

    /// <summary>
    /// Dialer Joinmap for Advanced Bridge - Meant for bridging the dialer as a standalone device
    /// </summary>
    public class TesiraDialerJoinMapAdvancedStandalone : JoinMapBaseAdvanced
    {
        [JoinName("IncomingCall")] public JoinDataComplete IncomingCall =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Call Incoming",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Answer")] public JoinDataComplete Answer =
            new JoinDataComplete(new JoinData {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Answer Incoming Call",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("EndCall")] public JoinDataComplete EndCall =
            new JoinDataComplete(new JoinData {JoinNumber = 3, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "End Call",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadNumeric")] public JoinDataComplete KeyPadNumeric =
            new JoinDataComplete(new JoinData {JoinNumber = 4, JoinSpan = 10},
                new JoinMetadata
                {
                    Description = "Keypad Digits 0-9",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadStar")] public JoinDataComplete KeyPadStar =
            new JoinDataComplete(new JoinData {JoinNumber = 14, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad *",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadPound")] public JoinDataComplete KeyPadPound =
            new JoinDataComplete(new JoinData {JoinNumber = 15, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad #",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadClear")] public JoinDataComplete KeyPadClear =
            new JoinDataComplete(new JoinData {JoinNumber = 16, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad Clear",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadBackspace")] public JoinDataComplete KeyPadBackspace =
            new JoinDataComplete(new JoinData {JoinNumber = 17, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad Backspace",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("KeyPadDial")] public JoinDataComplete KeyPadDial =
            new JoinDataComplete(new JoinData {JoinNumber = 18, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Keypad Dial and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerOn")] public JoinDataComplete AutoAnswerOn =
            new JoinDataComplete(new JoinData {JoinNumber = 19, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Auto Answer On and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerOff")] public JoinDataComplete AutoAnswerOff =
            new JoinDataComplete(new JoinData {JoinNumber = 20, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Auto Answer Off and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AutoAnswerToggle")] public JoinDataComplete AutoAnswerToggle =
            new JoinDataComplete(new JoinData {JoinNumber = 21, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Auto Answer Toggle and On Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OnHook")] public JoinDataComplete OnHook =
            new JoinDataComplete(new JoinData {JoinNumber = 22, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "On Hook Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("OffHook")] public JoinDataComplete OffHook =
            new JoinDataComplete(new JoinData {JoinNumber = 23, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Off Hook Set and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("DoNotDisturbOn")]
        public JoinDataComplete DoNotDisturbOn =
            new JoinDataComplete(new JoinData { JoinNumber = 24, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Do Not Disturb On Set and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DoNotDisturbOff")]
        public JoinDataComplete DoNotDisturbOff =
            new JoinDataComplete(new JoinData { JoinNumber = 25, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Do Not Disturb Off Set and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("DoNotDisturbToggle")]
        public JoinDataComplete DoNotDisturbToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 26, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Do Not Disturb Toggle and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });
        [JoinName("HoldCall")]
        public JoinDataComplete HoldCall =
            new JoinDataComplete(new JoinData { JoinNumber = 27, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Hold Call Set and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Resume Call")]
        public JoinDataComplete ResumeCall =
            new JoinDataComplete(new JoinData { JoinNumber = 28, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Resume Call Set and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("HoldToggle")]
        public JoinDataComplete HoldToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 29, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Hold Toggle and Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("CallState")] public JoinDataComplete CallState =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Call State Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("DialString")] public JoinDataComplete DialString =
            new JoinDataComplete(new JoinData {JoinNumber = 1, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Dial String Send and Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("Label")] public JoinDataComplete Label =
            new JoinDataComplete(new JoinData {JoinNumber = 2, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Dialer Label",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("LastNumberDialerFb")] public JoinDataComplete LastNumberDialerFb =
            new JoinDataComplete(new JoinData {JoinNumber = 3, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Last Number Dialed Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("CallerIdNumberFb")] public JoinDataComplete CallerIdNumberFb =
            new JoinDataComplete(new JoinData {JoinNumber = 4, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Caller ID Number",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("CallerIdNameFb")] public JoinDataComplete CallerIdNameFb =
            new JoinDataComplete(new JoinData {JoinNumber = 5, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "Caller ID Name",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("DisplayNumber")] public JoinDataComplete DisplayNumber =
            new JoinDataComplete(new JoinData {JoinNumber = 6, JoinSpan = 1},
                new JoinMetadata
                {
                    Description = "This Line's Number",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });


        public TesiraDialerJoinMapAdvancedStandalone(uint joinStart)
            : base(joinStart, typeof (TesiraDialerJoinMapAdvancedStandalone))
        {
        }
    }

}