using System.IO;
using System.Net.Http;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.App;

/// <summary>
/// Fetches community/v1.json, published every 30 minutes from shared play, for Ask and Home.
/// Hosted on the same R2 origin as the update feed; the request sends no player data. Cached locally for startup and offline use.
/// </summary>
internal sealed class CommunityFeed : IDisposable
{
    /// <summary>
    /// Rebuilt every 30 minutes; polling skips downloads while the cached copy is younger than <see cref="MinAge"/>
    /// so slight timer jitter does not miss a cycle.
    /// </summary>
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(30), MinAge = TimeSpan.FromMinutes(25);
    private static string CachePath => Path.Combine(AppSettings.DataDir, "community-v1.json");

    private readonly App _app;
    private readonly AppSettings _settings;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Timer _timer;
    private bool _loggedBadFile;

    public CommunityFeed(App app, AppSettings settings, string version)
    {
        _app = app;
        _settings = settings;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EQLPathfinder/" + version);
        _timer = new Timer(_ => _ = RefreshAsync(), null, TimeSpan.FromSeconds(20), Every);
    }

    /// <summary>The numbers in use; null while the feature is off or no file has arrived.</summary>
    public CommunityData? Current { get; private set; }

    /// <summary>Raised (on any thread) when <see cref="Current"/> changes.</summary>
    public event Action? Changed;

    private bool On => true;

    private static string Url => Environment.GetEnvironmentVariable("EQLPATHFINDER_COMMUNITY") is { Length: > 0 } e ? e : ShareProtocol.DefaultCommunity;

    /// <summary>The copy from last time, if the feature is on. UI thread, at start.</summary>
    public void LoadCached()
    {
        if (!On || !File.Exists(CachePath)) return;
        try
        {
            if (CommunityData.Parse(File.ReadAllText(CachePath)) is { } data) Set(data);
        }
        catch (Exception ex) { AppSettings.LogError("community: reading the saved copy", ex); }
    }

    /// <summary>Fetches the file when the saved copy is older than <see cref="MinAge"/> (or always, with <paramref name="force"/>); drops it when the feature is off.</summary>
    public async Task RefreshAsync(bool force = false)
    {
        if (!On)
        {
            if (Current != null) Set(null);
            return;
        }
        if (!await _gate.WaitAsync(0)) return;
        try
        {
            var saved = File.Exists(CachePath) ? File.GetLastWriteTimeUtc(CachePath) : DateTime.MinValue;
            if (Current == null && saved > DateTime.MinValue) LoadCached();
            if (!force && Current != null && DateTime.UtcNow - saved < MinAge) return;
            var json = await _http.GetStringAsync(Url);
            if (CommunityData.Parse(json) is not { } data)
            {
                // A newer schema than this version reads (or a broken upload): keep what we have; an update brings the reader.
                if (!_loggedBadFile) AppSettings.LogError("community", new InvalidDataException("community/v1.json isn't a file this version reads."));
                _loggedBadFile = true;
                return;
            }
            Directory.CreateDirectory(AppSettings.DataDir);
            var tmp = CachePath + ".tmp";
            await File.WriteAllTextAsync(tmp, json);
            File.Move(tmp, CachePath, overwrite: true);
            if (Current == null || Current.File.Generated != data.File.Generated) Set(data);
        }
        catch (HttpRequestException) { }      // offline: the saved copy stands, the next look tries again
        catch (TaskCanceledException) { }
        catch (Exception ex) { AppSettings.LogError("community", ex); }
        finally { _gate.Release(); }
    }

    private void Set(CommunityData? data)
    {
        Current = data;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _http.Dispose();
    }
}
