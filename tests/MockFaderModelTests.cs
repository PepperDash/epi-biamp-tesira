using FluentAssertions;
using Newtonsoft.Json;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class MockFaderModelTests
{
    [Fact]
    public void Level_and_mute_block_changes_both()
    {
        var m = new MockFaderModel(hasLevel: true, hasMute: true, initialLevelPercent: 50, initialMute: false);

        m.Level.Should().Be(32767);
        m.SetLevel(1000).Should().BeTrue();
        m.Level.Should().Be(1000);
        m.SetMute(true).Should().BeTrue();
        m.IsMuted.Should().BeTrue();
    }

    [Fact]
    public void Unchanged_values_report_no_change()
    {
        var m = new MockFaderModel(true, true, 0, true);

        m.SetLevel(0).Should().BeFalse();
        m.SetMute(true).Should().BeFalse();
    }

    [Fact]
    public void Mute_only_block_keeps_level_zero_and_ignores_level_changes()
    {
        // A privacy or ceiling-mic mute block: the DSP has no level attribute on it.
        var m = new MockFaderModel(hasLevel: false, hasMute: true, initialLevelPercent: 80, initialMute: true);

        m.Level.Should().Be(0, "a block without a level never reports one");
        m.SetLevel(40000).Should().BeFalse();
        m.Level.Should().Be(0);
        m.IsMuted.Should().BeTrue();
        m.SetMute(false).Should().BeTrue();
    }

    [Fact]
    public void Level_only_block_stays_unmuted_and_ignores_mute_changes()
    {
        var m = new MockFaderModel(hasLevel: true, hasMute: false, initialLevelPercent: 25, initialMute: true);

        m.IsMuted.Should().BeFalse("a block without a mute never reports muted");
        m.SetMute(true).Should().BeFalse();
        m.IsMuted.Should().BeFalse();
        m.SetLevel(5).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 32767)]
    [InlineData(100, 65535)]
    [InlineData(150, 65535)]
    public void Percent_scales_to_raw(ushort percent, ushort raw)
    {
        MockFaderModel.PercentToRaw(percent).Should().Be(raw);
    }

    [Fact]
    public void Fader_config_defaults_to_level_and_mute()
    {
        var cfg = JsonConvert.DeserializeObject<TesiraDspMockFaderConfig>("{\"label\":\"Room\"}");

        cfg.HasLevel.Should().BeTrue("existing mock configs don't set it");
        cfg.HasMute.Should().BeTrue();
    }

    [Fact]
    public void Fader_config_reads_the_real_fader_flags()
    {
        // The same keys a real faderControlBlocks entry uses, so a real config drops in.
        var cfg = JsonConvert.DeserializeObject<TesiraDspMockFaderConfig>(
            "{\"label\":\"Privacy\",\"muteInstanceTag\":\"PRIVACY\",\"hasLevel\":false,\"hasMute\":true}");

        cfg.HasLevel.Should().BeFalse();
        cfg.HasMute.Should().BeTrue();
    }
}

public class MockFaderStepTests
{
    [Theory]
    // 2 dB over -100..12 (112 dB): 2/112 * 65535 = 1170.27
    [InlineData("2.0", -100, 12, 1170)]
    // 1 dB over -40..12, as a program fader: 1/52 * 65535 = 1260.29
    [InlineData("1", -40, 12, 1260)]
    // a tiny step still moves the level
    [InlineData("0.0001", -100, 12, 1)]
    public void Step_is_incrementAmount_dB_over_the_range(string amount, double min, double max, ushort raw)
    {
        MockFaderModel.RawStep(amount, min, max).Should().Be(raw);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("Infinity")]
    public void No_usable_amount_keeps_the_legacy_step(string amount)
    {
        MockFaderModel.RawStep(amount, -100, 12).Should().Be(MockFaderModel.LegacyRawStep);
    }

    [Fact]
    public void An_empty_or_inverted_range_keeps_the_legacy_step()
    {
        MockFaderModel.RawStep("2.0", 12, 12).Should().Be(MockFaderModel.LegacyRawStep);
        MockFaderModel.RawStep("2.0", 12, -100).Should().Be(MockFaderModel.LegacyRawStep);
    }

    [Fact]
    public void Steps_stop_at_the_ends_of_the_range()
    {
        var m = new MockFaderModel(true, true, 100, false);
        m.Step(up: true, 1170).Should().BeFalse("already at the top");
        m.Step(up: false, 1170).Should().BeTrue();
        m.Level.Should().Be(65535 - 1170);

        var low = new MockFaderModel(true, true, 0, false);
        low.SetLevel(500);
        low.Step(up: false, 1170).Should().BeTrue();
        low.Level.Should().Be(0, "a step past the bottom stops at it");
    }

    [Fact]
    public void A_mute_only_block_never_steps()
    {
        new MockFaderModel(hasLevel: false, hasMute: true, 50, false).Step(true, 1170).Should().BeFalse();
    }

    [Fact]
    public void Fader_config_reads_the_real_volume_keys_with_real_defaults()
    {
        var none = JsonConvert.DeserializeObject<TesiraDspMockFaderConfig>("{}");
        none.IncrementAmount.Should().BeNull();
        none.MinLevel.Should().Be(-100);
        none.MaxLevel.Should().Be(12);
        none.UnmuteOnVolChange.Should().BeFalse();
        none.VolumeRepeatRateMs.Should().Be(100);
        none.VolumeHoldTimeoutMs.Should().BeNull("unset inherits the device's timeout");

        var real = JsonConvert.DeserializeObject<TesiraDspMockFaderConfig>(
            "{\"incrementAmount\":\"2.0\",\"unmuteOnVolChange\":true,\"minLevel\":-40,\"maxLevel\":12}");
        real.IncrementAmount.Should().Be("2.0");
        real.UnmuteOnVolChange.Should().BeTrue();
        real.MinLevel.Should().Be(-40);
    }

    [Fact]
    public void A_reused_real_fader_config_with_a_null_hold_timeout_deserializes()
    {
        var cfg = JsonConvert.DeserializeObject<TesiraDspMockFaderConfig>("{\"volumeHoldTimeoutMs\":null}");
        cfg.VolumeHoldTimeoutMs.Should().BeNull();
        JsonConvert.DeserializeObject<TesiraDspMockFaderConfig>("{\"volumeHoldTimeoutMs\":0}")
            .VolumeHoldTimeoutMs.Should().Be(0, "an explicit 0 disables the timeout and is kept");
    }

    [Fact]
    public void The_device_hold_timeout_defaults_as_the_real_driver()
    {
        JsonConvert.DeserializeObject<TesiraDspMockPropertiesConfig>("{}").VolumeHoldTimeoutMs.Should().Be(10000);
        JsonConvert.DeserializeObject<TesiraDspMockPropertiesConfig>("{\"volumeHoldTimeoutMs\":4000}")
            .VolumeHoldTimeoutMs.Should().Be(4000);
    }

    [Fact]
    public void Concurrent_steps_are_not_lost()
    {
        // VolumeRamp steps from timer threads, outside its lock
        var m = new MockFaderModel(true, true, 0, false);
        System.Threading.Tasks.Parallel.For(0, 500, _ => m.Step(up: true, 10));
        m.Level.Should().Be(5000);
        System.Threading.Tasks.Parallel.For(0, 200, i => { if (i % 2 == 0) m.Step(false, 10); else m.Step(true, 10); });
        m.Level.Should().Be(5000);
    }

    [Fact]
    public void A_step_past_an_end_lands_on_it_and_reports_it()
    {
        var m = new MockFaderModel(true, true, 0, false);
        m.SetLevel(65000);
        m.Step(up: true, 1170, out var past).Should().BeTrue("it still lands on the end");
        past.Should().BeTrue();
        m.Level.Should().Be(65535);
        m.Step(up: true, 1170, out past).Should().BeFalse();
        past.Should().BeTrue("a step from the end is past it too, so the repeat stops");

        m.SetLevel(1170);
        m.Step(up: false, 1170, out past).Should().BeTrue();
        past.Should().BeFalse("landing exactly on the end is not past it");
        m.Level.Should().Be(0);
    }

    [Fact]
    public void A_mute_only_block_reports_no_past_end_step()
    {
        new MockFaderModel(false, true, 0, false).Step(false, 1170, out var past).Should().BeFalse();
        past.Should().BeFalse();
    }
}
