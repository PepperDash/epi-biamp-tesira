using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core.DeviceInfo;
using Tesira_DSP_EPI.Bridge.JoinMaps;
using PepperDash.Essentials.Core.Bridges;

namespace Tesira_DSP_EPI
{
    public class TesiraDspDeviceInfo : TesiraDspControlPoint, IDeviceInfoProvider
    {
        /// <summary>
        /// Feedback Collection for Component
        /// </summary>

        public DeviceInfo DeviceInfo { get; private set; }

        public event DeviceInfoChangeHandler DeviceInfoChanged;

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
        public TesiraDspDeviceInfo(TesiraDsp parent)
            : base("DEVICE", "DEVICE", 0, 0, parent, String.Format(KeyFormatter, parent.Key, "DeviceInfo"), "DeviceInfo", 0)
        {

            DeviceInfo = new DeviceInfo();


            Init();
        }

        private void Init()
        {
            NameFeedback = new StringFeedback(() => Parent.Name);
            IpAddressFeedback = new StringFeedback(() => IpAddress);
            HostnameFeedback = new StringFeedback(() => Hostname);
            SerialNumberFeedback = new StringFeedback(() => SerialNumber);
            FirmwareFeedback = new StringFeedback(() => Firmware);
            MacAddressFeedback = new StringFeedback(() => MacAddress);



            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(Parent.CommunicationMonitor.IsOnlineFeedback);
            Feedbacks.Add(Parent.CommandPassthruFeedback);
            Feedbacks.Add(IpAddressFeedback);
            Feedbacks.Add(HostnameFeedback);
            Feedbacks.Add(SerialNumberFeedback);
            Feedbacks.Add(FirmwareFeedback);
            Feedbacks.Add(MacAddressFeedback);
        }


        public void GetIpConfig()
        {
            Debug.Console(2, this, "Getting IPConfig");
            SendFullCommand("get", "networkStatus", null, 999);
        }

        public void GetSerial()
        {
            Debug.Console(2, this, "Getting Serial");

            SendFullCommand("get", "serialNumber", null, 999);            
        }

        public void GetFirmware()
        {
            Debug.Console(2, this, "Getting Firmware");

            SendFullCommand("get", "version", null, 999);            
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
                case ("networkStatus"):
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



            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            //var comm = DspDevice as IBasicCommunication;
            trilist.SetSigTrueAction(joinMap.Resubscribe.JoinNumber, Parent.Resubscribe);




            Parent.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            Parent.CommandPassthruFeedback.LinkInputSig(trilist.StringInput[joinMap.CommandPassThru.JoinNumber]);
            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.Name.JoinNumber]);
            SerialNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.SerialNumber.JoinNumber]);
            FirmwareFeedback.LinkInputSig(trilist.StringInput[joinMap.Firmware.JoinNumber]);
            HostnameFeedback.LinkInputSig(trilist.StringInput[joinMap.Hostname.JoinNumber]);
            IpAddressFeedback.LinkInputSig(trilist.StringInput[joinMap.IpAddress.JoinNumber]);
            MacAddressFeedback.LinkInputSig(trilist.StringInput[joinMap.MacAddress.JoinNumber]);


            trilist.SetStringSigAction(joinMap.CommandPassThru.JoinNumber, Parent.SendLineRaw);

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (var feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }

            };

        }


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

    }
}