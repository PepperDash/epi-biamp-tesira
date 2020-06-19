using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;

namespace Tesira_DSP_EPI
{
    //Mixer1 get crosspointLevelState 1 1
    //Mixer1 set crosspointLevelState 1 1 true
    //Mixer1 toggle crosspointLevelState 1 1
    public class TesiraDspMatrixMixer : TesiraDspControlPoint
    {
        public static readonly string AttributeCode = "crosspointLevelState";

        bool _State;
        public BoolFeedback StateFeedback { get; set; }

		public TesiraDspMatrixMixer(uint key, TesiraMatrixMixerBlockConfig config, TesiraDsp parent)
            : base(config.matrixInstanceTag, string.Empty, config.index1, config.index2, parent)
        {
            Key = string.Format("{0}--{1}", Parent.Key, key);
            Label = config.label;
            Enabled = config.enabled;

            StateFeedback = new BoolFeedback(() => _State);

            /*CrestronConsole.AddNewConsoleCommand(s => StateOn(), "mixerstateon", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => StateOff(), "mixerstateoff", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => StateToggle(), "mixerstatetoggle", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => GetState(), "mixerstateget", "", ConsoleAccessLevelEnum.AccessOperator);*/
        }

        public void GetState()
        {
            Debug.Console(2, this, "GetState sent to {0}", this.Key);
            SendFullCommand("get", AttributeCode, String.Empty, 1);
        }

        public void StateOn()
        {
            Debug.Console(2, this, "StateOn sent to {0}", this.Key);
            SendFullCommand("set", AttributeCode, "true", 1);
            GetState();
        }

        public void StateOff()
        {
            Debug.Console(2, this, "StateOff sent to {0}", this.Key);
            SendFullCommand("set", AttributeCode, "false", 1);
            GetState();
        }

        public void StateToggle()
        {
            Debug.Console(2, this, "StateToggle sent to {0}", this.Key);
            SendFullCommand("toggle", AttributeCode, String.Empty, 1);
            GetState();
        }

        public override void ParseGetMessage(string attributeCode, string message)
        {
            try
            {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message
                string pattern = "[^ ]* (.*)";

                Match match = Regex.Match(message, pattern);

                if (match.Success)
                {

                    string value = match.Groups[1].Value;

                    Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                    if (message.IndexOf("+OK") > -1)
                    {
                        if (attributeCode.Equals(AttributeCode, StringComparison.InvariantCultureIgnoreCase))
                        {
                            _State = bool.Parse(value);
                            Debug.Console(2, this, "New Value: {0}", _State);
                            this.StateFeedback.FireUpdate();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
            }
        }

    }
}