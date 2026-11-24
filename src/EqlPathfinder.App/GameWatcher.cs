using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace EqlPathfinder.App;

/// <summary>
/// Watches for the EverQuest Legends client (eqgame.exe). The overlay only shows while it runs, so
/// Pathfinder can stay in the tray all the time without covering the desktop.
/// </summary>
internal sealed class GameWatcher : IDisposable
{
    private const string ProcessName = "eqgame";
    private readonly DispatcherTimer _timer;

    public GameWatcher()
    {
        (Running, var started) = Check();
        if (Running) StartedAt = started ?? DateTime.Now;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) =>
        {
            var (now, start) = Check();
            if (now == Running) return;
            Running = now;
            StartedAt = now ? start ?? DateTime.Now : null;
            Changed?.Invoke(now);
        };
        _timer.Start();
    }

    public bool Running { get; private set; }
    /// <summary>When the running client started (or when Pathfinder first saw it, if Windows won't say).</summary>
    public DateTime? StartedAt { get; private set; }
    public event Action<bool>? Changed;

    public static bool IsRunning() => Check().Running;

    private static (bool Running, DateTime? Started) Check()
    {
        var ps = Process.GetProcessesByName(ProcessName);
        DateTime? started = null;
        foreach (var p in ps)
        {
            try { if (started == null || p.StartTime < started) started = p.StartTime; }
            catch (Exception) { /* a client run as administrator hides its start time */ }
            p.Dispose();
        }
        return (ps.Length > 0, started);
    }

    /// <summary>Hand memory back to Windows while nothing is happening (the game is closed).</summary>
    public static void TrimMemory()
    {
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        try { EmptyWorkingSet(Process.GetCurrentProcess().Handle); } catch { /* best effort */ }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr process);

    public void Dispose() => _timer.Stop();
}
