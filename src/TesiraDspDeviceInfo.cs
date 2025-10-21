using System;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Bridge.JoinMaps.Standalone;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceInfo;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira
{
    public class TesiraDspDeviceInfo : TesiraDspControlPoint, IDeviceInfoProvider
    {
        /// <summary>
        /// Feedback Collection for Component
        /// </summary>

        public DeviceInfo DeviceInfo { get; private set; }

        public const string Make = "Biamp";
        public string Model { get; private set; }

        public event DeviceInfoChangeHandler DeviceInfoChanged;

        private const string keyFormatter = "{0}--{1}";

        private string ipAddress;

        public string IpAddress
        {
            get { return ipAddress; }
            private set
            {
                ipAddress = value;
                IpAddressFeedback.FireUpdate();
            }
        }

        private string macAddress;

        public string MacAddress
        {
            get { return macAddress; }
            private set
            {
                macAddress = value;
                MacAddressFeedback.FireUpdate();
            }
        }

        private string hostname;

        public string Hostname
        {
            get { return hostname; }
            private set
            {
                hostname = value;
                HostnameFeedback.FireUpdate();
            }
        }

        private string serialNumber;

        public string SerialNumber
        {
            get { return serialNumber; }
            private set
            {
                serialNumber = value;
                SerialNumberFeedback.FireUpdate();
            }
        }

        private string firmware;

        public string Firmware
        {
            get { return firmware; }
            private set
            {
                firmware = value;
                FirmwareFeedback.FireUpdate();
            }
        }


        public StringFeedback IpAddressFeedback { get; set; }
        public StringFeedback HostnameFeedback { get; set; }
        public StringFeedback SerialNumberFeedback { get; set; }
        public StringFeedback FirmwareFeedback { get; set; }
        public StringFeedback MacAddressFeedback { get; set; }
        public StringFeedback MakeFeedback { get; set; }
        public StringFeedback ModelFeedback { get; set; }

        /// <summary>
        /// Constructor for Device Info Object
        /// </summary>
        /// <param name="parent">Parent Device</param>
        public TesiraDspDeviceInfo(TesiraDsp parent)
            : base("DEVICE", "DEVICE", 0, 0, parent, string.Format(keyFormatter, parent.Key, "DeviceInfo"), "DeviceInfo", 0)
        {

            DeviceInfo = new DeviceInfo();
            Init();
        }

        private void Init()
        {
            NameFeedback = new StringFeedback("name", () => Parent.Name);
            IpAddressFeedback = new StringFeedback("ipAddress", () => IpAddress);
            HostnameFeedback = new StringFeedback("hostname", () => Hostname);
            SerialNumberFeedback = new StringFeedback("serialNumber", () => SerialNumber);
            FirmwareFeedback = new StringFeedback("firmware", () => Firmware);
            MacAddressFeedback = new StringFeedback("macAddress", () => MacAddress);
            MakeFeedback = new StringFeedback("make", () => Make);
            ModelFeedback = new StringFeedback("model", () => Model);




            Feedbacks.Add(NameFeedback);
            Feedbacks.Add(Parent.CommunicationMonitor.IsOnlineFeedback);
            Feedbacks.Add(Parent.CommandPassthruFeedback);
            Feedbacks.Add(IpAddressFeedback);
            Feedbacks.Add(HostnameFeedback);
            Feedbacks.Add(SerialNumberFeedback);
            Feedbacks.Add(FirmwareFeedback);
            Feedbacks.Add(MacAddressFeedback);
        }
        public void GetDeviceInfo()
        {
            GetFirmware();
            GetIpConfig();
            GetSerial();
            GetServers();
        }


        private void GetIpConfig()
        {
            this.LogVerbose("Getting IPConfig");
            SendFullCommand("get", "networkStatus", null, 999);
        }

        private void GetSerial()
        {
            this.LogVerbose("Getting Serial");

            SendFullCommand("get", "serialNumber", null, 999);
        }

        private void GetFirmware()
        {
            this.LogVerbose("Getting Firmware");

            SendFullCommand("get", "version", null, 999);
        }

        private void GetServers()
        {
            this.LogVerbose("Getting Servers");
            SendFullCommand("get", "discoveredServers", null, 999);
        }
        private const string pattern = "([\"\'])(?:(?=(\\\\?))\\2.)*?\\1";

        private static readonly Regex parseRegex = new Regex(pattern);

        public override void ParseGetMessage(string attributeCode, string message)
        {
            this.LogVerbose("Parsing Message: {message}. AttributeCode: {attributeCode}", message, attributeCode);
            // Parse an "+OK" message

            if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;

            var matches = parseRegex.Matches(message);

            if (matches == null) return;

            switch (attributeCode)
            {
                case "networkStatus":
                    {
                        Hostname = matches[0].Value.Trim('"');
                        MacAddress = matches[3].Value.Trim('"');
                        IpAddress = matches[4].Value.Trim('"');

                        DeviceInfo.HostName = string.IsNullOrEmpty(DeviceInfo.HostName) ? Hostname : DeviceInfo.HostName;
                        DeviceInfo.MacAddress = string.IsNullOrEmpty(DeviceInfo.MacAddress) ? MacAddress : DeviceInfo.MacAddress;
                        DeviceInfo.IpAddress = string.IsNullOrEmpty(DeviceInfo.IpAddress) ? IpAddress : DeviceInfo.IpAddress;

                        OnDeviceInfoChanged();
                        break;
                    }
                case "serialNumber":
                    {
                        SerialNumber = matches[0].Value.Trim('"');

                        DeviceInfo.SerialNumber = string.IsNullOrEmpty(DeviceInfo.SerialNumber) ? SerialNumber : DeviceInfo.SerialNumber;

                        OnDeviceInfoChanged();
                        break;
                    }
                case "version":
                    Firmware = matches[0].Value.Trim('"');

                    DeviceInfo.FirmwareVersion = string.IsNullOrEmpty(DeviceInfo.FirmwareVersion) ? Firmware : DeviceInfo.FirmwareVersion;

                    OnDeviceInfoChanged();
                    break;

                case "discoveredServers":
                    for (var i = 0; i <= matches.Count; i = i + 2)
                    {
                        if (!matches[i].Value.Trim('"').Equals(IpAddress)) continue;
                        var substring = message.Substring(4, message.Length - 4).Replace("]]", string.Empty).Replace("[[", string.Empty).Replace("][", "|");
                        var chunks = substring.Split('|');
                        var chunkSelector = i == 0 ? 0 : i / 2;
                        Model = chunks[chunkSelector].Split(' ').Last();
                    }
                    ModelFeedback.FireUpdate();
                    MakeFeedback.FireUpdate();
                    OnDeviceInfoChanged();
                    break;
            }

        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new TesiraDspDeviceJoinMapAdvancedStandalone(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<TesiraDspDeviceJoinMapAdvancedStandalone>(joinMapSerialized);

            bridge?.AddJoinMap(Key, joinMap);

            this.LogDebug("Linking to Trilist {trilistId:X}'", trilist.ID.ToString("X"));

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

        public void OnDeviceInfoChanged()
        {
            var args = new DeviceInfoEventArgs(DeviceInfo);
            DeviceInfoChanged?.Invoke(Parent, args);
        }


        #region IDeviceInfoProvider Members

        public void UpdateDeviceInfo()
        {
            GetDeviceInfo();
        }

        #endregion

    }
}