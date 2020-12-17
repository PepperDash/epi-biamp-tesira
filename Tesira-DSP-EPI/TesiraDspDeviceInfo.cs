using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;

namespace Tesira_DSP_EPI
{
    public class TesiraDspDeviceInfo : TesiraDspControlPoint
    {
        /// <summary>
        /// Feedback Collection for Component
        /// </summary>

        private readonly Dictionary<string, TesiraDspPresets> _presets; 

        readonly TesiraDsp _parent;

        private const string KeyFormatter = "{0}--{1}";

        private string _ipAddress;

        public string IpAddress
        {
            get { return _ipAddress; }
            private set
            {
                _ipAddress = value;
                IpAddressFeedback.FireUpdate();
                GetHostname();
            }
        }

        private string _macAddress;

        public string MacAddress
        {
            get { return _macAddress; }
            private set
            {
                _macAddress = value;
                MacAddressFeedback.FireUpdate();
            }
        }

        private string _hostname;

        public string Hostname
        {
            get { return _hostname; }
            private set
            {
                _hostname = value;
                HostnameFeedback.FireUpdate();
                GetSerial();
            }
        }

        private string _serialNumber;

        public string SerialNumber
        {
            get { return _serialNumber; }
            private set
            {
                _serialNumber = value;
                SerialNumberFeedback.FireUpdate();
                GetFirmware();
            }
        }

        private string _firmware;

        public string Firmware
        {
            get { return _firmware; }
            private set
            {
                _firmware = value;
                FirmwareFeedback.FireUpdate();
            }
        }


        public StringFeedback IpAddressFeedback { get; set; }
        public StringFeedback HostnameFeedback { get; set; }
        public StringFeedback SerialNumberFeedback { get; set; }
        public StringFeedback FirmwareFeedback { get; set; }
        public StringFeedback MacAddressFeedback { get; set; }

        /// <summary>
        /// Constructor for Device Info Object
        /// </summary>
        /// <param name="parent">Parent Device</param>
        /// <param name="presets">Dictionary of Presets</param>
        public TesiraDspDeviceInfo(TesiraDsp parent, Dictionary<string, TesiraDspPresets> presets)
            : base("DEVICE", "DEVICE", 0, 0, parent, String.Format(KeyFormatter, parent.Key, "DeviceInfo"), "DeviceInfo", null)
        {
            _presets = presets;
            _parent = parent;

            Init();
        }

        private void Init()
        {
            NameFeedback = new StringFeedback(() => _parent.Name);
            IpAddressFeedback = new StringFeedback(() => IpAddress);
            HostnameFeedback = new StringFeedback(() => Hostname);
            SerialNumberFeedback = new StringFeedback(() => SerialNumber);
            FirmwareFeedback = new StringFeedback(() => Firmware);
            MacAddressFeedback = new StringFeedback(() => MacAddress);

            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(_parent.CommunicationMonitor.IsOnlineFeedback);
            Feedbacks.Add(_parent.CommandPassthruFeedback);
            Feedbacks.Add(IpAddressFeedback);
            Feedbacks.Add(HostnameFeedback);
            Feedbacks.Add(SerialNumberFeedback);
            Feedbacks.Add(FirmwareFeedback);
            Feedbacks.Add(MacAddressFeedback);

            Debug.Console(2, this, "Tesira DeviceInfo \"{0}\" Device Created", Key);
        }

        public override void Subscribe()
        {
            GetDeviceInformation();
        }

        public void GetDeviceInformation()
        {
            GetIpConfig();
        }

        private void GetIpConfig()
        {
            SendFullCommand("get", "ipStatus", "control", 999);
        }

        private void GetHostname()
        {
            SendFullCommand("get", "hostname", "", 999);
        }

        private void GetSerial()
        {
            SendFullCommand("get", "serialNumber", "", 999);            
        }

        private void GetFirmware()
        {
            SendFullCommand("get", "version", "", 999);            
        }


        public override void ParseGetMessage(string attributeCode, string message)
        {
            Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
            // Parse an "+OK" message
            const string pattern = "[^ ]* (.*)";

            const string ipPattern = "\"(.*?)\"";

            var match = Regex.Match(message, pattern);

            if (!match.Success) return;

            var value = match.Groups[1].Value;

            Debug.Console(1, this, "Response: '{0}' Value: '{1}'", attributeCode, value);

            if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;

            switch (attributeCode)
            {
                case ("ipStatus") :
                {
                    var matches = Regex.Matches(value, ipPattern);
                    if (matches.Count != 6) return;
                    MacAddress = matches[0].Value;
                    IpAddress = matches[1].Value;
                    break;
                }
                case ("hostname") :
                {
                    Hostname = value;
                    break;
                }
                case("serialNumber") :
                {
                    SerialNumber = value;
                    break;
                }
                case ("version") :
                    Firmware = value;
                    break;

            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraDspDeviceJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraDspDeviceJoinMapAdvancedStandalone>(joinMapSerialized);

            var presetJoinMap = new TesiraPresetJoinMapAdvancedStandalone(joinStart);
            var presetJoinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(presetJoinMapSerialized))
            {
                presetJoinMap =
                    JsonConvert.DeserializeObject<TesiraPresetJoinMapAdvancedStandalone>(presetJoinMapSerialized);
            }

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
                bridge.AddJoinMap(Key, presetJoinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            //var comm = DspDevice as IBasicCommunication;
            trilist.SetSigTrueAction(joinMap.Resubscribe.JoinNumber, _parent.Resubscribe);

            trilist.SetStringSigAction(presetJoinMap.PresetName.JoinNumber, _parent.RunPreset);

            foreach (var preset in _presets)
            {
                var p = preset;
                var runPresetIndex = preset.Value.PresetIndex;
                var presetIndex = runPresetIndex - 1;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex)].StringValue = p.Value.Label;
                trilist.SetSigTrueAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex), () => _parent.RunPresetNumber((ushort)runPresetIndex));
            }


            _parent.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            _parent.CommandPassthruFeedback.LinkInputSig(trilist.StringInput[joinMap.CommandPassThru.JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Name.JoinNumber]);
            SerialNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.SerialNumber.JoinNumber]);
            FirmwareFeedback.LinkInputSig(trilist.StringInput[joinMap.Firmware.JoinNumber]);
            HostnameFeedback.LinkInputSig(trilist.StringInput[joinMap.Hostname.JoinNumber]);
            IpAddressFeedback.LinkInputSig(trilist.StringInput[joinMap.IpAddress.JoinNumber]);
            MacAddressFeedback.LinkInputSig(trilist.StringInput[joinMap.MacAddress.JoinNumber]);


            trilist.SetStringSigAction(joinMap.CommandPassThru.JoinNumber, _parent.SendLineRaw);

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

            };

        }
    }
}