using FluentAssertions;
using Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Mock;
using Xunit;

namespace Pepperdash.Essentials.Plugins.DSP.Biamp.Tesira.Tests;

/// <summary>
/// The in-order change queue behind the mock selector and router: changes apply one at a time, in
/// order, with no lock held while a change is applied.
/// </summary>
public class MockChangeQueueTests
{
    [Fact]
    public void Applies_Changes_In_Order()
    {
        var applied = new List<int>();
        var queue = new MockChangeQueue<int>(applied.Add);

        queue.Post(1);
        queue.Post(2);

        applied.Should().Equal(1, 2);
    }

    [Fact]
    public void A_Change_Posted_While_Applying_Is_Applied_After_The_Current_One()
    {
        var applied = new List<int>();
        MockChangeQueue<int>? queue = null;
        queue = new MockChangeQueue<int>(change =>
        {
            applied.Add(change);
            if (change == 1) queue!.Post(2); // e.g. a subscriber reacting to the change
            applied.Add(-change);            // the current change finishes first
        });

        queue.Post(1);

        applied.Should().Equal(1, -1, 2, -2);
    }

    [Fact]
    public void A_Throwing_Change_Does_Not_Stop_The_Rest_And_Is_Rethrown()
    {
        var applied = new List<int>();
        MockChangeQueue<int>? queue = null;
        queue = new MockChangeQueue<int>(change =>
        {
            if (change == 1)
            {
                queue!.Post(2);
                throw new InvalidOperationException("subscriber failed");
            }
            applied.Add(change);
        });

        var act = () => queue.Post(1);

        act.Should().Throw<InvalidOperationException>();
        applied.Should().Equal(2);

        queue.Post(3);
        applied.Should().Equal(2, 3); // not left stuck
    }

    [Fact]
    public async Task Two_Queues_Posting_To_Each_Other_From_Two_Threads_Do_Not_Deadlock()
    {
        // Each queue's change posts to the other queue, as a subscriber of one mock block routing
        // another would. With a lock held while applying, this could deadlock.
        using var bothApplying = new Barrier(2);
        var appliedA = 0;
        var appliedB = 0;
        MockChangeQueue<int>? a = null;
        MockChangeQueue<int>? b = null;
        a = new MockChangeQueue<int>(change =>
        {
            Interlocked.Increment(ref appliedA);
            if (change == 0) { bothApplying.SignalAndWait(TimeSpan.FromSeconds(5)); b!.Post(1); }
        });
        b = new MockChangeQueue<int>(change =>
        {
            Interlocked.Increment(ref appliedB);
            if (change == 0) { bothApplying.SignalAndWait(TimeSpan.FromSeconds(5)); a!.Post(1); }
        });

        var ta = Task.Run(() => a.Post(0));
        var tb = Task.Run(() => b.Post(0));

        // Throws TimeoutException if either queue blocks the other.
        await Task.WhenAll(ta, tb).WaitAsync(TimeSpan.FromSeconds(10));
        appliedA.Should().Be(2);
        appliedB.Should().Be(2);
    }
}
