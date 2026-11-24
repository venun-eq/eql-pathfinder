using System.Diagnostics;
using System.Globalization;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// How many headless Edges run at once on this PC, whichever test run starts them: a worktree's check, ship's tests,
/// finish's clean copy, a suite's own parallel runs. When many start together each takes seconds of a core, and a run
/// that passed the harness's 20 seconds was stopped as hung (docs/page-tests.md). The slots are named mutexes: one held
/// by a process that was killed frees itself, and a slot is taken and given back on one thread.
/// </summary>
internal sealed class EdgeSlots(string prefix, Func<int> countNow, TimeSpan wait)
{
    public EdgeSlots(string prefix, int count, TimeSpan wait) : this(prefix, () => count, wait) { }

    /// <summary>Half the cores this run may use, at least 2, and about 4 while the game runs (PageLimits); asked at each wait.</summary>
    public static EdgeSlots Shared { get; } = new("EqlPathfinderEdgeSlot", PageLimits.Slots, TimeSpan.FromMinutes(10));

    /// <summary>Waits for a free slot. Throws <see cref="PageFailed"/> when none frees within the wait.</summary>
    public IDisposable Acquire()
    {
        var count = countNow();
        var start = Stopwatch.StartNew();
        var mutexes = new Mutex[count];
        for (var i = 0; i < count; i++) mutexes[i] = Open(i);
        try
        {
            while (true)
            {
                for (var i = 0; i < count; i++)
                {
                    bool got;
                    try { got = mutexes[i].WaitOne(0); }
                    catch (AbandonedMutexException) { got = true; }
                    if (!got) continue;
                    for (var j = 0; j < count; j++) if (j != i) mutexes[j].Dispose();
                    return new Slot(mutexes[i]);
                }
                if (start.Elapsed > wait)
                    throw new PageFailed($"The browser: all {count} browser slots on this PC stayed taken for {wait.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} seconds (other test runs are using them), so this run never started.");
                Thread.Sleep(50);
            }
        }
        catch
        {
            foreach (var m in mutexes) m.Dispose();
            throw;
        }
    }

    // Global\ so a scheduled task in another session shares the slots; the user's own session where that isn't allowed.
    private Mutex Open(int i)
    {
        try { return new Mutex(false, $"Global\\{prefix}{i}"); }
        catch (UnauthorizedAccessException) { return new Mutex(false, $"Local\\{prefix}{i}"); }
    }

    private sealed class Slot(Mutex held) : IDisposable
    {
        public void Dispose() { held.ReleaseMutex(); held.Dispose(); }
    }
}
