using System.Threading;
using Velopack;

namespace EqlPathfinder.App;

public static class Program
{
    /// <summary>Set by a second launch to ask the running copy to show its window.</summary>
    internal static readonly string ShowSignalName = @"Local\EQLPathfinder.Show" + Instance;

    /// <summary>"" for the real copy; a suffix for an isolated developer copy (EQLPATHFINDER_DATA), so the two never meet.</summary>
    private static string Instance =>
        AppSettings.IsolatedDataDir == null ? "" : "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(AppSettings.IsolatedDataDir.ToLowerInvariant())))[..12];

    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: handles Velopack's install/update/uninstall hooks, installs an update downloaded
        // last session, then returns.
        VelopackApp.Build().OnRestarted(v => App.UpdatedTo = v.ToString()).Run();

        if (args.Length >= 2 && args[0] == "--snapshot") { DevSnapshots.Run(args); return; }
        if (args.Length >= 4 && args[0] == "--meter-snapshot") { DevSnapshots.RunMeter(args); return; }
        if (args.Length >= 2 && args[0] == "--updater-test") { UpdaterSelfTest.Run(args[1]); return; }
        // After an update Pathfinder installed by itself: come back the way it was (in the tray or minimized), not as Settings says.
        if (args.Length >= 2 && args[0] == App.StartAfterUpdateArg && args[1] is "Tray" or "Minimized") App.StartOverride = args[1];

        using var single = new Mutex(true, @"Local\EQLPathfinder.SingleInstance" + Instance, out bool first);
        if (!first)
        {
            // Already running (usually in the tray): bring its window up instead.
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var show))
                using (show) show.Set();
            else
                System.Windows.MessageBox.Show("EQL Pathfinder is already running. Look for the compass in the system tray.",
                    "EQL Pathfinder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
