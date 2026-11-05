using FluentAssertions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

public class MockRouteTableTests
{
    private static MockRouteTable Table() => new(new uint[] { 2, 1 }, new uint[] { 1, 2, 3 });

    [Fact]
    public void Every_Output_Starts_Cleared()
    {
        var table = Table();

        table.Get(1).Should().Be(0);
        table.Get(2).Should().Be(0);
        table.Outputs.Should().Equal(1u, 2u);
    }

    [Fact]
    public void Routes_Each_Output_Independently()
    {
        var table = Table();

        table.Route(1, 3).Should().Be(RouteResult.Changed);
        table.Route(2, 1).Should().Be(RouteResult.Changed);

        table.Get(1).Should().Be(3);
        table.Get(2).Should().Be(1);
    }

    [Fact]
    public void Input_Zero_Clears_Only_That_Output()
    {
        var table = Table();
        table.Route(1, 3);
        table.Route(2, 1);

        table.Route(1, 0).Should().Be(RouteResult.Changed);

        table.Get(1).Should().Be(0);
        table.Get(2).Should().Be(1);
    }

    [Fact]
    public void The_Same_Route_Again_Is_Unchanged()
    {
        var table = Table();
        table.Route(1, 2);

        table.Route(1, 2).Should().Be(RouteResult.Unchanged);
    }

    [Fact]
    public void Refuses_An_Unconfigured_Output_Or_Input()
    {
        var table = Table();

        table.Route(3, 1).Should().Be(RouteResult.UnknownOutput);
        table.Route(1, 9).Should().Be(RouteResult.UnknownInput);
        table.Get(1).Should().Be(0);
        table.Get(3).Should().Be(0);
    }

    [Fact]
    public void Snapshot_Is_A_Copy()
    {
        var table = Table();
        var before = table.Snapshot();

        table.Route(1, 2);

        before[1].Should().Be(0);
        table.Snapshot()[1].Should().Be(2);
    }
}
