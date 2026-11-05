#nullable disable
namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock
{
    /// <summary>
    /// The device info a mock DSP reports, resolved from its optional <c>deviceInfo</c> config block.
    /// Free of Crestron and Essentials types so it can be unit tested in the test host.
    /// </summary>
    /// <remarks>
    /// The real driver fills <c>DeviceInfo</c> from the DSP's replies: host name, MAC and IP from
    /// <c>DEVICE get networkStatus</c>, the serial number, and the firmware version. The mock reports
    /// the same fields. Unset ones default to the device key as host name, the control address in the
    /// config (or 127.0.0.1) as IP, a MAC from the documentation range (RFC 7042), and placeholder
    /// serial and firmware strings shaped like a real unit's ("00000000", "0.0.0.0").
    /// </remarks>
    internal sealed class MockDeviceInfoValues
    {
        internal const string DefaultMacAddress = "00:00:5e:00:53:00";
        internal const string DefaultIpAddress = "127.0.0.1";
        internal const string DefaultSerialNumber = "00000000";
        internal const string DefaultFirmwareVersion = "0.0.0.0";

        public string HostName { get; private set; }
        public string IpAddress { get; private set; }
        public string MacAddress { get; private set; }
        public string SerialNumber { get; private set; }
        public string FirmwareVersion { get; private set; }

        /// <param name="deviceKey">The mock device's key, the default host name.</param>
        /// <param name="config">The <c>deviceInfo</c> block; null when absent.</param>
        /// <param name="controlAddress">The control address in the config, if a real config was reused.</param>
        public static MockDeviceInfoValues Resolve(string deviceKey, TesiraDspMockDeviceInfoConfig config,
            string controlAddress)
        {
            return new MockDeviceInfoValues
            {
                HostName = Pick(config?.HostName, deviceKey),
                IpAddress = Pick(config?.IpAddress, Pick(controlAddress, DefaultIpAddress)),
                MacAddress = Pick(config?.MacAddress, DefaultMacAddress),
                SerialNumber = Pick(config?.SerialNumber, DefaultSerialNumber),
                FirmwareVersion = Pick(config?.FirmwareVersion, DefaultFirmwareVersion),
            };
        }

        private static string Pick(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
