using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Devices.Common.VideoCodec.Cisco;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;

namespace Tesira_DSP_EPI
{
    public class TesiraDspDeviceInfo : TesiraDspControlPoint, IDeviceInfoProvider, IHasDspPresets
    {
        /// <summary>
        /// Feedback Collection for Component
        /// </summary>

        public DeviceInfo DeviceInfo { get; private set; }

        public event DeviceInfoChangeHandler DeviceInfoChanged;

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
        public TesiraDspDeviceInfo(TesiraDsp parent, List<IDspPreset> presets)
            : base("DEVICE", "DEVICE", 0, 0, parent, String.Format(KeyFormatter, parent.Key, "DeviceInfo"), "DeviceInfo", null)
        {
            _parent = parent;
            Presets = presets;

            DeviceInfo = new DeviceInfo();

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


        public void GetIpConfig()
        {
            SendFullCommand("get", "ipStatus", "control", 999);
        }

        public void GetSerial()
        {
            SendFullCommand("get", "serialNumber", "", 999);            
        }

        public void GetFirmware()
        {
            SendFullCommand("get", "version", "", 999);            
        }


        public override void ParseGetMessage(string attributeCode, string message)
        {
            Debug.Console(2, this, "Parsing Message - '{0}' : Message has an attributeCode of {1}", message, attributeCode);
            // Parse an "+OK" message
            const string pattern = "([\"\'])(?:(?=(\\\\?))\\2.)*?\\1";

            if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;

            var matches = Regex.Matches(message, pattern);

            if (matches == null) return;

            switch (attributeCode)
            {
                case ("ipStatus") :
                {
                    Hostname = matches[0].Value.Trim('"');
                    MacAddress = matches[3].Value.Trim('"');
                    IpAddress = matches[1].Value.Trim('"');
                    DeviceInfo.HostName = Hostname;
                    DeviceInfo.MacAddress = MacAddress;
                    DeviceInfo.IpAddress = IpAddress;
                    UpdateDeviceInfo();
                    break;
                }
                case("serialNumber") :
                {
                    SerialNumber = matches[0].Value.Trim('"');
                    DeviceInfo.SerialNumber = SerialNumber;
                    UpdateDeviceInfo();
                    break;
                }
                case ("version") :
                    Firmware = matches[0].Value.Trim('"');
                    DeviceInfo.FirmwareVersion = Firmware; 
                    UpdateDeviceInfo();
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

            foreach (var preset in Presets)
            {
                var p = preset as TesiraPreset;
                if (p == null) continue;
                var runPresetIndex = p.PresetData.PresetIndex;
                var presetIndex = runPresetIndex;
                trilist.StringInput[(uint)(presetJoinMap.PresetNameFeedback.JoinNumber + presetIndex)].StringValue = p.PresetData.Label;
                trilist.SetSigTrueAction((uint)(presetJoinMap.PresetSelection.JoinNumber + presetIndex), () => RunPresetNumber((ushort)presetIndex));
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

        #region Presets

        public void RunPresetNumber(ushort n)
        {
            Debug.Console(2, this, "Attempting to run preset {0}", n);

            foreach (var preset in Presets.OfType<TesiraPreset>().Where(preset => preset.Index == n))
            {
                Debug.Console(2, this, "Found a matching Preset - {0}", preset.PresetData.PresetId);
                RecallPreset(preset);
            }

        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="name">Preset Name</param>
        public void RunPreset(string name)
        {
            Debug.Console(2, this, "Running Preset By Name - {0}", name);
            _parent.SendLine(string.Format("DEVICE recallPresetByName \"{0}\"", name));
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPresetByName \"{0}\"", name));
        }

        /// <summary>
        /// Sends a command to execute a preset
        /// </summary>
        /// <param name="id">Preset Id</param>
        public void RunPreset(int id)
        {
            Debug.Console(2, this, "Running Preset By ID - {0}", id);
            _parent.SendLine(string.Format("DEVICE recallPreset {0}", id));
            //CommandQueue.EnqueueCommand(string.Format("DEVICE recallPreset {0}", id));
        }

        public void RecallPreset(IDspPreset preset)
        {
            Debug.Console(2, this, "Running preset {0}", preset.Name);
            var tesiraPreset = preset as TesiraPreset;
            if (tesiraPreset == null) return;
            if (!String.IsNullOrEmpty(tesiraPreset.PresetName))
            {
                RunPreset(tesiraPreset.PresetData.PresetName);
            }
            else
            {
                RunPreset(tesiraPreset.PresetData.PresetId);
            }
        }

        #endregion

        #region IDeviceInfoProvider Members


        public void UpdateDeviceInfo()
        {
            var args = new DeviceInfoEventArgs(DeviceInfo);

           var raiseEvent = DeviceInfoChanged;

            if (raiseEvent != null)
            {
                raiseEvent(Parent, args);
            }
        }

        #endregion

        #region IHasDspPresets Members

        public List<IDspPreset> Presets { get; private set; }


        #endregion
    }
}