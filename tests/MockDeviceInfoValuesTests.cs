using FluentAssertions;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class MockDeviceInfoValuesTests
{
    [Fact]
    public void Defaults_without_a_deviceInfo_block_or_control_address()
    {
        var v = MockDeviceInfoValues.Resolve("dsp-1", null, null);

        v.HostName.Should().Be("dsp-1");
        v.IpAddress.Should().Be("127.0.0.1");
        v.MacAddress.Should().Be("00:00:5e:00:53:00", "a documentation-range MAC, not a real unit's");
        v.SerialNumber.Should().Be("00000000");
        v.FirmwareVersion.Should().Be("0.0.0.0");
    }

    [Fact]
    public void Control_address_from_a_reused_real_config_is_the_ip()
    {
        MockDeviceInfoValues.Resolve("dsp-1", null, "192.0.2.10").IpAddress.Should().Be("192.0.2.10");
    }

    [Fact]
    public void Configured_values_win_and_blanks_fall_back()
    {
        var cfg = JsonConvert.DeserializeObject<TesiraDspMockPropertiesConfig>(
            "{\"deviceInfo\":{\"hostName\":\" BENCHDSP \",\"ipAddress\":\"192.0.2.20\",\"macAddress\":\"\"," +
            "\"serialNumber\":\"12345678\",\"firmwareVersion\":\"5.6.1.2\"}}");

        var v = MockDeviceInfoValues.Resolve("dsp-1", cfg.DeviceInfo, "192.0.2.10");

        v.HostName.Should().Be("BENCHDSP");
        v.IpAddress.Should().Be("192.0.2.20", "deviceInfo overrides the control address");
        v.MacAddress.Should().Be("00:00:5e:00:53:00", "a blank value falls back to the default");
        v.SerialNumber.Should().Be("12345678");
        v.FirmwareVersion.Should().Be("5.6.1.2");
    }
}
