using System.IO;
using System.Reflection;
using Velopack;

namespace EqlPathfinder.App;

/// <summary>
/// In-app updates from a Velopack release feed: releases.win.json (regular releases) and, for copies in the beta,
/// releases.beta.json too. The feed is a static web address, or for this PC the releases\ folder it builds into.
/// Checks 20 s after start and every 3 hours, and for a folder feed also as soon as a new release lands in it.
/// A downloaded update installs the next time Pathfinder starts, by itself once it's safe (App.MaybeInstallUpdate: the game
/// closed a while, Pathfinder out of sight), or right away from "Restart to update".
/// </summary>
internal sealed class Updater : IDisposable
{
    public enum Outcome { Disabled, UpToDate, Ready, Failed }

    /// <summary>Regular releases (Velopack's default channel on Windows) and beta releases (tools\release.ps1 -Beta).</summary>
    public const string StableChannel = "win", BetaChannel = "beta";

    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(20), Every = TimeSpan.FromHours(3);
    private readonly UpdateManager? _mgr;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Timer? _timer, _debounce;
    private FileSystemWatcher? _watch;
    private UpdateManager? _pendingFrom;
    private bool _betaMissingLogged;

    /// <summary>
    /// In the beta: check the beta channel as well as the regular one and take whichever is newer. Never moves to an
    /// older version, so leaving the beta keeps this build until a regular release passes it.
    /// </summary>
    public bool Beta { get; set; }

    /// <summary>This build is a beta (its version has a pre-release tag, like 0.9.0-beta.1).</summary>
    public bool OnBetaBuild => CurrentVersion?.Contains('-') == true;

    /// <summary>Raised on a worker thread when an update finished downloading in the background.</summary>
    public event Action<VelopackAsset>? Downloaded;

    public string Feed { get; }
    public bool Enabled => _mgr != null;
    public string? CurrentVersion => _mgr?.CurrentVersion?.ToString();
    public VelopackAsset? Pending { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>The feed this build was released with (tools\release.ps1 -Feed); settings.json can override it.</summary>
    public static string BuiltInFeed =>
        typeof(Updater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "UpdateFeed")?.Value ?? "";

    private static string NotesPath => Path.Combine(AppSettings.DataDir, "update-notes.md");

    /// <summary>
    /// A feed for this PC only (first line of update-feed.txt in the data folder). The developer's copy points at
    /// the local releases folder so it gets each build before friends do; settings saves never touch this file.
    /// </summary>
    private static string? LocalFeed()
    {
        try
        {
            var path = Path.Combine(AppSettings.DataDir, "update-feed.txt");
            return File.Exists(path) ? File.ReadLines(path).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) : null;
        }
        catch (IOException) { return null; }
    }

    public Updater(string configuredFeed)
    {
        Feed = !string.IsNullOrWhiteSpace(configuredFeed) ? configuredFeed.Trim() : LocalFeed() ?? BuiltInFeed;
        if (Feed.Length == 0) return;
        try
        {
            // Always name the channel: a copy installed from a beta package would otherwise only ever look at the beta feed.
            var mgr = Manager(StableChannel);
            if (!mgr.IsInstalled) return; // a dev build run from bin\ can't update itself
            _mgr = mgr;
            Pending = mgr.UpdatePendingRestart;
        }
        catch (Exception ex) { AppSettings.LogError("update feed " + Feed, ex); }
    }

    public void Start()
    {
        if (_mgr == null) return;
        _timer = new Timer(_ => _ = CheckAsync(), null, FirstCheck, Every);
        if (!Directory.Exists(Feed)) return;
        // A folder feed: notice a new release within seconds of tools\release.ps1 finishing.
        var debounce = _debounce = new Timer(_ => _ = CheckAsync());
        _watch = new FileSystemWatcher(Feed, "releases.*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        FileSystemEventHandler later = (_, _) => debounce.Change(TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan);
        _watch.Changed += later;
        _watch.Created += later;
        _watch.Renamed += (s, e) => later(s, e);
        _watch.EnableRaisingEvents = true;
    }

    private readonly Velopack.Locators.IVelopackLocator? _locator;

    private UpdateManager Manager(string channel) => new(Feed, new UpdateOptions { ExplicitChannel = channel }, _locator);

    /// <summary>A copy that pretends to be installed at <paramref name="locator"/>'s version (Velopack's test locator).</summary>
    internal Updater(string feed, Velopack.Locators.IVelopackLocator locator)
    {
        Feed = feed;
        _locator = locator;
        _mgr = Manager(StableChannel);
    }

    /// <summary>
    /// The newest release to move to: the regular channel's, or in the beta the newer of the regular and beta channels'.
    /// Null when nothing is newer than this build (never an older version).
    /// </summary>
    internal async Task<(UpdateInfo Info, UpdateManager From)?> FindAsync()
    {
        if (_mgr == null) return null;
        var (info, from) = (await _mgr.CheckForUpdatesAsync().ConfigureAwait(false), _mgr);
        if (Beta)
        {
            try
            {
                var beta = Manager(BetaChannel);
                if (await beta.CheckForUpdatesAsync().ConfigureAwait(false) is { IsDowngrade: false } b &&
                    (info == null || b.TargetFullRelease.Version > info.TargetFullRelease.Version))
                    (info, from) = (b, beta);
            }
            catch (Exception ex)
            {
                // No beta has been published yet (no releases.beta.json): the regular channel still counts.
                if (!_betaMissingLogged) AppSettings.LogError("beta feed (none published yet?)", ex);
                _betaMissingLogged = true;
            }
        }
        return info is { IsDowngrade: false } ? (info, from) : null;
    }

    /// <summary>Checks the feed (and the beta feed, in the beta) and downloads anything newer. Safe to call from any thread.</summary>
    public async Task<Outcome> CheckAsync(bool interactive = false)
    {
        if (_mgr == null) return Outcome.Disabled;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (await FindAsync().ConfigureAwait(false) is not var (info, from)) return Pending != null ? Outcome.Ready : Outcome.UpToDate;
            var target = info.TargetFullRelease;
            if (Pending != null && Pending.Version.ToString() == target.Version.ToString()) return Outcome.Ready;
            await from.DownloadUpdatesAsync(info).ConfigureAwait(false);
            Pending = target;
            _pendingFrom = from;
            LastError = null;
            try { File.WriteAllText(NotesPath, $"{target.Version}\n{target.NotesMarkdown}"); } catch (IOException) { }
            if (!interactive) Downloaded?.Invoke(target);
            return Outcome.Ready;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AppSettings.LogError("update check", ex);
            return Outcome.Failed;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Exits, installs the downloaded update and starts the new version (with <paramref name="args"/>).</summary>
    public void RestartToUpdate(string[]? args = null)
    {
        if (_mgr != null && Pending != null) (_pendingFrom ?? _mgr).ApplyUpdatesAndRestart(Pending, args);
    }

    /// <summary>The first line of a release's notes, for a notification.</summary>
    public static string Headline(string? markdown)
    {
        var line = (markdown ?? "").Split('\n').Select(l => l.Trim().TrimStart('#', '-', '*', ' ')).FirstOrDefault(l => l.Length > 0) ?? "";
        return line.Length > 160 ? line[..157] + "…" : line;
    }

    /// <summary>Notes saved when the update that just installed was downloaded (version, then markdown).</summary>
    public static string? NotesFor(string version)
    {
        try
        {
            if (!File.Exists(NotesPath)) return null;
            var text = File.ReadAllText(NotesPath);
            int nl = text.IndexOf('\n');
            return nl > 0 && text[..nl].Trim() == version ? text[(nl + 1)..] : null;
        }
        catch (IOException) { return null; }
    }

    public void Dispose()
    {
        _watch?.Dispose();
        _timer?.Dispose();
        _debounce?.Dispose();
    }
}
