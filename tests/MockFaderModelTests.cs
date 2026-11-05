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
