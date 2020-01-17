using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using System.Text.RegularExpressions;

namespace Tesira_DSP_EPI {
    public class TesiraDspStateControl : TesiraDspControlPoint, IKeyed {
        bool _State;

        public BoolFeedback StateFeedback { get; set; }

        public string StateCustomName { get; set; }

        public TesiraDspStateControl(string key, TesiraStateControlBlockConfig config, TesiraDsp parent)
            : base(config.stateInstanceTag, String.Empty, config.index, 0, parent) {
            Debug.Console(2, this, "New State Instance Tag = {0}", config.stateInstanceTag);
            Debug.Console(2, this, "Starting State {0} Initialize", key);
            Initialize(key, config);

        }


        /// <summary>
        /// Initializes this attribute based on config values and generates subscriptions commands and adds commands to the parent's queue.
        /// </summary>
        /// <param name="key">key of the control</param>
        /// <param name="label">friendly name of the control</param>
        /// <param name="hasMute">defines if the control has a mute</param>
        /// <param name="hasLevel">defines if the control has a level</param>
        public void Initialize(string key, TesiraStateControlBlockConfig config) {
            Key = string.Format("{0}--{1}", Parent.Key, key);
            DeviceManager.AddDevice(this);

            Debug.Console(2, this, "Adding StateControl '{0}'", Key);

            IsSubscribed = false;

            Label = config.label;

            StateFeedback = new BoolFeedback(() => _State);

            Enabled = config.enabled;

            //Subscribe();
        }

        public override void Subscribe() {
            StateCustomName = string.Format("{0}~state{1}", this.InstanceTag1, this.Index1);
            Debug.Console(2, this, "StateCustomName = {0}", StateCustomName);
            SendSubscriptionCommand(StateCustomName, "state", 250, 1);

            GetState();
        }

        /// <summary>
        /// Parses the response from the DspBase
        /// </summary>
        /// <param name="customName"></param>
        /// <param name="value"></param>
        public void ParseSubscriptionMessage(string customName, string value) {

            // Check for valid subscription response

            if (customName == StateCustomName) {
                _State = bool.Parse(value);
                StateFeedback.FireUpdate();
                IsSubscribed = true;
            }
        }

        /// <summary>
        /// Parses a non subscription response
        /// </summary>
        /// <param name="attributeCode">The attribute code of the command</param>
        /// <param name="message">The message to parse</param>
        public override void ParseGetMessage(string attributeCode, string message) {
            try {
                Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
                // Parse an "+OK" message
                string pattern = "[^ ]* (.*)";

                Match match = Regex.Match(message, pattern);

                if (match.Success) {

                    string value = match.Groups[1].Value;

                    Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

                    if (message.IndexOf("+OK") > -1) {
                        if (attributeCode == "state") {
                            _State = bool.Parse(value);
                            this.StateFeedback.FireUpdate();
                            IsSubscribed = true;
                        }
                    }
                }
            }
            catch (Exception e) {
                Debug.Console(2, "Unable to parse message: '{0}'\n{1}", message, e);
            }
        }

        public void GetState() {
            Debug.Console(2, this, "GetState sent to {0}", this.Key);
            SendFullCommand("get", "state", String.Empty, 1);
        }

        public void StateOn() {
            Debug.Console(2, this, "StateOn sent to {0}", this.Key);
            SendFullCommand("set", "state", "true", 1);
            GetState();
        }

        public void StateOff() {
            Debug.Console(2, this, "StateOff sent to {0}", this.Key);
            SendFullCommand("set", "state", "false", 1);
            GetState();
        }

        public void StateToggle() {
            Debug.Console(2, this, "StateToggle sent to {0}", this.Key);
            if (_State) {
                SendFullCommand("set", "state", "false", 1);
            }
            else if (!_State) {
                SendFullCommand("set", "state", "true", 1);
            }
            this.GetState();
        }
    }
}