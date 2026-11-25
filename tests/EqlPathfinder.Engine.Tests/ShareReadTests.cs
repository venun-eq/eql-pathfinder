using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>share-without-derived, Decision 1: a build made behind a read that failed or never ran withdraws nothing.</summary>
public class ShareReadTests
{
    [Fact]
    public async Task ABuildIsSettledOnlyOnceTheHistoryWasRead()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-shareread-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, TestLog.LiveName);
        File.Copy(TestLog.BefallenUnrest, log);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, TestLog.Logs(log), new TrackerOptions());

            // Nothing read yet, as after a read that failed or was cancelled before it kept anything.
            Assert.False((await svc.Query(s => s.ShareRead("0.0.0", new byte[16]))).Settled);

            await svc.ImportHistoryAsync();
            Assert.True((await svc.Query(s => s.ShareRead("0.0.0", new byte[16]))).Settled);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task AReadCancelledAfterItsFirstFileIsNotSettled()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-shareread-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var first = Path.Combine(dir, "a-" + TestLog.LiveName);
        var live = Path.Combine(dir, TestLog.LiveName);
        File.Copy(TestLog.BefallenUnrest, first);
        File.Copy(TestLog.BefallenUnrest, live);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            var logs = new CharacterLogs(TestLog.Character, TestLog.Server, live, [first, live]);
            using var svc = new TrackerService(store, logs, new TrackerOptions());
            using var cts = new CancellationTokenSource();
            // Two files of the same size: past 60% the first has ended and the second is being read.
            var progress = new CallbackProgress(pct => { if (pct > 60) cts.Cancel(); });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.ImportHistoryAsync(progress, cts.Token));
            Assert.True(svc.HasHistory);   // the first file's end was kept: without the rule the read looks settled
            Assert.False((await svc.Query(s => s.ShareRead("0.0.0", new byte[16]))).Settled);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private sealed class CallbackProgress(Action<double> on) : IProgress<double>
    {
        public void Report(double v) => on(v);
    }
}
