using Xunit;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The cap on headless Edges at once on this PC (docs/page-tests.md): a full house waits and then says so, and a slot held by a dead run frees itself.</summary>
public class EdgeSlotsTests
{
    private static string Name() => "EqlPathfinderTest" + Guid.NewGuid().ToString("N");

    /// <summary>A run of its own holding a slot, on a thread of its own (one thread takes a named mutex again without waiting). Disposing gives it back.</summary>
    private static IDisposable Holder(EdgeSlots slots)
    {
        var taken = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var thread = new Thread(() => { using var _ = slots.Acquire(); taken.Set(); release.Wait(); });
        thread.Start();
        Assert.True(taken.Wait(TimeSpan.FromSeconds(5)));
        return new Release(release, thread);
    }

    private sealed class Release(ManualResetEventSlim release, Thread thread) : IDisposable
    {
        public void Dispose() { release.Set(); thread.Join(); }
    }

    [Fact]
    public void ARunWhenEverySlotIsTakenWaitsAndThenFailsSayingSo()
    {
        var slots = new EdgeSlots(Name(), 2, TimeSpan.FromMilliseconds(300));
        using var a = Holder(slots);
        var b = Holder(slots);
        var e = Assert.Throws<PageFailed>(() => slots.Acquire());
        Assert.Contains("all 2 browser slots", e.Message);
        b.Dispose();
        slots.Acquire().Dispose();
    }

    [Fact]
    public async Task ARunThatWaitsGoesOnceASlotIsGivenBack()
    {
        var slots = new EdgeSlots(Name(), 1, TimeSpan.FromSeconds(10));
        var held = Holder(slots);
        var waiting = Task.Run(() => slots.Acquire().Dispose());
        await Task.Delay(300);
        Assert.False(waiting.IsCompleted);
        held.Dispose();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(waiting.IsCompletedSuccessfully);
    }

    [Fact]
    public void ASlotLeftHeldByARunThatDiedFreesItself()
    {
        var slots = new EdgeSlots(Name(), 1, TimeSpan.FromMilliseconds(300));
        var t = new Thread(() => slots.Acquire());   // takes the slot and ends without giving it back
        t.Start();
        t.Join();
        slots.Acquire().Dispose();
    }
}
