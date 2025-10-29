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
            try
            {
                this.LogVerbose("Parsing Message: {message}. AttributeCode: {attributeCode}", message, attributeCode);
                // Parse an "+OK" message

                if (message.IndexOf("+OK", StringComparison.OrdinalIgnoreCase) <= -1) return;

                var matches = parseRegex.Matches(message);

                if (matches == null || matches.Count == 0)
                {
                    this.LogWarning("No matches found when parsing device info message: {message}", message);
                    return;
                }

                // Add detailed logging for debugging
                this.LogDebug("Raw response for {attributeCode}: {message}", attributeCode, message);
                this.LogDebug("Regex matches for {attributeCode}: [{matches}]",
                    attributeCode,
                    string.Join(", ", matches.Cast<Match>().Select((m, i) => $"[{i}]='{m.Value}'")));

                switch (attributeCode)
                {
                    case "networkStatus":
                        {
                            this.LogVerbose("Network Status match count: {count}", matches.Count);

                            // More defensive parsing - check bounds before accessing
                            if (matches.Count >= 1 && !string.IsNullOrEmpty(matches[0].Value))
                            {
                                try
                                {
                                    Hostname = matches[0].Value.Trim('"');
                                    this.LogDebug("Parsed Hostname: {hostname}", Hostname);
                                }
                                catch (Exception ex)
                                {
                                    this.LogError("Error parsing hostname from match[0]: {value}. Error: {error}", matches[0].Value, ex.Message);
                                }
                            }

                            if (matches.Count >= 4 && !string.IsNullOrEmpty(matches[3].Value))
                            {
                                try
                                {
                                    MacAddress = matches[3].Value.Trim('"');
                                    this.LogDebug("Parsed MacAddress: {macAddress}", MacAddress);
                                }
                                catch (Exception ex)
                                {
                                    this.LogError("Error parsing MAC address from match[3]: {value}. Error: {error}", matches[3].Value, ex.Message);
                                }
                            }

                            if (matches.Count >= 5 && !string.IsNullOrEmpty(matches[4].Value))
                            {
                                try
                                {
                                    IpAddress = matches[4].Value.Trim('"');
                                    this.LogDebug("Parsed IpAddress: {ipAddress}", IpAddress);
                                }
                                catch (Exception ex)
                                {
                                    this.LogError("Error parsing IP address from match[4]: {value}. Error: {error}", matches[4].Value, ex.Message);
                                }
                            }

                            // Only update DeviceInfo if we got valid values
                            if (!string.IsNullOrEmpty(Hostname))
                                DeviceInfo.HostName = string.IsNullOrEmpty(DeviceInfo.HostName) ? Hostname : DeviceInfo.HostName;
                            if (!string.IsNullOrEmpty(MacAddress))
                                DeviceInfo.MacAddress = string.IsNullOrEmpty(DeviceInfo.MacAddress) ? MacAddress : DeviceInfo.MacAddress;
                            if (!string.IsNullOrEmpty(IpAddress))
                                DeviceInfo.IpAddress = string.IsNullOrEmpty(DeviceInfo.IpAddress) ? IpAddress : DeviceInfo.IpAddress;

                            OnDeviceInfoChanged();
                            break;
                        }
                    case "serialNumber":
                        {
                            this.LogVerbose("Serial Number match count: {count}", matches.Count);
                            if (matches.Count >= 1 && !string.IsNullOrEmpty(matches[0].Value))
                            {
                                try
                                {
                                    SerialNumber = matches[0].Value.Trim('"');
                                    this.LogDebug("Parsed SerialNumber: {serialNumber}", SerialNumber);
                                    DeviceInfo.SerialNumber = string.IsNullOrEmpty(DeviceInfo.SerialNumber) ? SerialNumber : DeviceInfo.SerialNumber;
                                    OnDeviceInfoChanged();
                                }
                                catch (Exception ex)
                                {
                                    this.LogError("Error parsing serial number from match[0]: {value}. Error: {error}", matches[0].Value, ex.Message);
                                }
                            }
                            else
                            {
                                this.LogWarning("No serial number found in response: {message}", message);
                            }
                            break;
                        }
                    case "version":
                        {
                            this.LogVerbose("Firmware match count: {count}", matches.Count);
                            if (matches.Count >= 1 && !string.IsNullOrEmpty(matches[0].Value))
                            {
                                try
                                {
                                    Firmware = matches[0].Value.Trim('"');
                                    this.LogDebug("Parsed Firmware: {firmware}", Firmware);
                                    DeviceInfo.FirmwareVersion = string.IsNullOrEmpty(DeviceInfo.FirmwareVersion) ? Firmware : DeviceInfo.FirmwareVersion;
                                    OnDeviceInfoChanged();
                                }
                                catch (Exception ex)
                                {
                                    this.LogError("Error parsing firmware version from match[0]: {value}. Error: {error}", matches[0].Value, ex.Message);
                                }
                            }
                            else
                            {
                                this.LogWarning("No firmware version found in response: {message}", message);
                            }
                            break;
                        }

                    case "discoveredServers":
                        {
                            this.LogDebug("Discovered servers response: {message}", message);

                            try
                            {
                                // Extract the content between [[ and ]]
                                var startIndex = message.IndexOf("[[", StringComparison.Ordinal);
                                var endIndex = message.LastIndexOf("]]", StringComparison.Ordinal);

                                if (startIndex == -1 || endIndex == -1)
                                {
                                    this.LogWarning("Could not find array markers in discoveredServers response: {message}", message);
                                    break;
                                }

                                var arrayContent = message.Substring(startIndex + 2, endIndex - startIndex - 2);
                                this.LogDebug("Extracted array content: {arrayContent}", arrayContent);

                                // Split by "] [" to separate multiple device entries
                                var deviceEntries = arrayContent.Split(new[] { "] [" }, StringSplitOptions.RemoveEmptyEntries);

                                this.LogDebug("Found {count} device entries", deviceEntries.Length);

                                foreach (var deviceEntry in deviceEntries)
                                {
                                    // Clean up the entry (remove any remaining brackets)
                                    var cleanEntry = deviceEntry.Trim('[', ']');
                                    this.LogDebug("Processing device entry: {entry}", cleanEntry);

                                    // Use regex to parse the individual entry
                                    var deviceMatches = parseRegex.Matches(cleanEntry);

                                    if (deviceMatches.Count >= 2)
                                    {
                                        var serverIp = deviceMatches[0].Value.Trim('"');
                                        var serverHostname = deviceMatches[1].Value.Trim('"');

                                        this.LogDebug("Found device - IP: {serverIp}, Hostname: {serverHostname}", serverIp, serverHostname);

                                        // Check if this device matches our IP address
                                        if (!string.Equals(serverIp, IpAddress, StringComparison.OrdinalIgnoreCase))
                                        {
                                            continue;
                                        }

                                        this.LogDebug("Found matching server. Hostname: {serverHostname}", serverHostname);

                                        // Extract deviceType by splitting on spaces and finding numeric values
                                        // Format: "10.11.50.192" "nycd-tesira01" false 1 18
                                        var parts = cleanEntry.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        this.LogDebug("Split entry into {count} parts: {parts}", parts.Length, string.Join(", ", parts));

                                        // Look for the last numeric value (deviceType)
                                        for (int i = parts.Length - 1; i >= 0; i--)
                                        {
                                            var part = parts[i].Trim();
                                            if (int.TryParse(part, out var deviceType))
                                            {
                                                Model = deviceType.ToString();
                                                this.LogVerbose("Set model to deviceType: {model}", Model);
                                                break;
                                            }
                                        }

                                        break; // Found our device, no need to continue
                                    }
                                    else
                                    {
                                        this.LogWarning("Expected at least 2 matches for device entry, got {count}: {entry}", deviceMatches.Count, cleanEntry);
                                    }
                                }

                                ModelFeedback.FireUpdate();
                                MakeFeedback.FireUpdate();
                                OnDeviceInfoChanged();
                            }
                            catch (Exception ex)
                            {
                                this.LogError(ex, "Error parsing discoveredServers response: {message}", message);
                            }
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                this.LogError("Error parsing device info message: {message}. Error: {error}", message, e);
                this.LogDebug(e, "Stack Trace: ");
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


            trilist.SetStringSigAction(joinMap.CommandPassThru.JoinNumber, (s) => Parent.SendLineRaw(s));

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