using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.App;

/// <summary>This install's signing key. The private half is kept in settings, encrypted for this Windows user (DPAPI).</summary>
internal static class ShareIdentity
{
    private static readonly byte[] Entropy = "EQL Pathfinder share key v1"u8.ToArray();

    public static ECDsa? Load(AppSettings s)
    {
        if (s.ShareKey is not { Length: > 0 } stored) return null;
        try
        {
            var pkcs8 = ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.CurrentUser);
            var key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(pkcs8, out _);
            CryptographicOperations.ZeroMemory(pkcs8);
            return key;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            AppSettings.LogError("share key unreadable (another Windows user, or settings copied from another PC)", ex);
            return null;
        }
    }

    public static ECDsa Create(AppSettings s)
    {
        var key = ShareProtocol.NewKey();
        var pkcs8 = key.ExportPkcs8PrivateKey();
        s.ShareKey = Convert.ToBase64String(ProtectedData.Protect(pkcs8, Entropy, DataProtectionScope.CurrentUser));
        s.ShareHome = AppSettings.DataDir;   // an isolated copy keeps only a key made in its own folder (AppSettings.ForgetCopiedSharing)
        CryptographicOperations.ZeroMemory(pkcs8);
        return key;
    }
}

/// <summary>What has been sent, per character code: unit ID → content hash. Kept beside settings.</summary>
internal sealed class ShareLedger
{
    private static string FilePath => Path.Combine(AppSettings.DataDir, "share-ledger.json");

    public string? Player { get; set; }
    public Dictionary<string, Dictionary<string, string>> Characters { get; set; } = new();

    public static ShareLedger Load(string? player)
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<ShareLedger>(File.ReadAllText(FilePath)) is { } l && l.Player == player) return l;
        }
        catch (Exception ex) { AppSettings.LogError("share ledger unreadable", ex); }
        return new ShareLedger { Player = player };
    }

    public Dictionary<string, string> For(string character) =>
        Characters.TryGetValue(character, out var d) ? d : Characters[character] = new();

    public void Save()
    {
        Directory.CreateDirectory(AppSettings.DataDir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this));
        File.Move(tmp, FilePath, overwrite: true);
    }

    public static void Forget()
    {
        try { File.Delete(FilePath); } catch (IOException) { }
    }
}

/// <summary>
/// Community sharing ("EQL Pathfinder: Sharing spec"). Off until the player turns it on. Then it registers this PC and,
/// every 10 minutes, sends each unit (crawl, instance, open-world stretch, reincarnation, relevel) that's new or changed
/// since the last upload. Settings are only changed on the UI thread.
/// </summary>
internal sealed class SharingService : IDisposable
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(10);
    private readonly App _app;
    private readonly AppSettings _settings;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Timer _timer;
    private ECDsa? _key;
    private DateTime _waitUntil;

    public SharingService(App app, AppSettings settings)
    {
        _app = app;
        _settings = settings;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EQLPathfinder/" + Version);
        _timer = new Timer(_ => _ = SyncAsync(), null, TimeSpan.FromSeconds(60), Every);
    }

    /// <summary>Raised (on any thread) whenever the status shown in Settings changes.</summary>
    public event Action? Changed;

    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public int Pending { get; private set; }
    public DateTime? LastUpload { get; private set; }
    public ShareMe? Server { get; private set; }

    /// <summary>
    /// Since when this copy's play is in the community numbers (everything: <see cref="DateTime.MinValue"/>), or null when it
    /// isn't. Sharing is on or paused (what was sent stays), and the server holds some of its crawls or hunts; before the
    /// server has answered, the ledger says whether any were sent. The file is rebuilt every 30 minutes, so for up to that
    /// long after a first upload it doesn't hold them yet. Read from any thread.
    /// </summary>
    public DateTime? SharedFrom => _settings.ShareEnabled && (Server is { } me
        ? me.Counts.Where(k => k.Key is "crawl" or "instance" or "stretch").Sum(k => k.Value) > 0
        : _sentAny ??= ShareLedger.Load(_settings.SharePlayer).Characters.Values.Any(c => c.Count > 0))
        ? _settings.ShareFrom ?? DateTime.MinValue : null;
    private bool? _sentAny;
    public ShareConfig? Config { get; private set; }
    public ShareCode? LinkCode { get; private set; }
    public ShareCode? EnrollCode { get; private set; }

    private string Version => _app.AppVersion ?? typeof(SharingService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private Uri Api => new(Environment.GetEnvironmentVariable("EQLPATHFINDER_API") is { Length: > 0 } e ? e
        : _settings.ShareApi is { Length: > 0 } s ? s : ShareProtocol.DefaultApi);

    private ShareClient Client(ECDsa key) => new(_http, Api, key);

    private Task Ui(Action a) => _app.Dispatcher.InvokeAsync(a).Task;
    private Task<T> Ui<T>(Func<T> f) => _app.Dispatcher.InvokeAsync(f).Task;

    /// <summary>The Settings card's view of sharing.</summary>
    public object Status() => new
    {
        kind = "shareStatus",
        enabled = _settings.ShareEnabled,
        paused = _settings.SharePaused,
        from = _settings.ShareFrom?.ToString("yyyy-MM-dd"),
        busy = Busy,
        pending = Pending,
        error = Error,
        notice = Notice,
        counts = Server?.Counts,
        pcs = Server?.Pcs,
        lastUpload = LastUpload ?? (Server?.LastUpload is { } lu ? DateTimeOffset.FromUnixTimeSeconds(lu).LocalDateTime : null),
        requireEnrollment = Config?.RequireEnrollment,
        link = LinkCode is { } l && DateTimeOffset.FromUnixTimeSeconds(l.Expires) > DateTimeOffset.UtcNow
            ? new { code = l.Code, expires = DateTimeOffset.FromUnixTimeSeconds(l.Expires).LocalDateTime } : null,
        enroll = EnrollCode is { } c && DateTimeOffset.FromUnixTimeSeconds(c.Expires) > DateTimeOffset.UtcNow
            ? new { code = c.Code, expires = DateTimeOffset.FromUnixTimeSeconds(c.Expires).LocalDateTime } : null,
    };

    /// <summary>A message from the Settings card: {type: "share", action, ...}. UI thread.</summary>
    public void Handle(JsonElement m)
    {
        string action = m.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
        string? Text(string name) => m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : null;
        switch (action)
        {
            case "status": _ = RefreshAsync(); break;
            case "on": _ = TurnOnAsync(Text("history") != "new", Text("enroll"), Text("link")); break;
            case "pause": _settings.SharePaused = true; _settings.Save(); Raise(); break;
            case "resume": _settings.SharePaused = false; _settings.Save(); _waitUntil = default; _ = SyncAsync(); break;
            case "sync": _waitUntil = default; _ = SyncAsync(); break;
            case "delete": _ = DeleteEverythingAsync(); break;
            case "link": _ = MakeLinkCodeAsync(); break;
            case "claim": if (Text("code") is { } code) _ = ClaimAsync(code); break;
            case "enroll": _ = MakeEnrollCodeAsync(); break;
        }
    }

    private void Raise() => Changed?.Invoke();

    private async Task Run(Func<Task> work, string what)
    {
        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(30))) return;
        try
        {
            Busy = true;
            Error = null;
            Notice = null;
            Raise();
            await work();
        }
        catch (ShareApiException ex) when (ex.Code is "daily_limit" or "slow_down" or "one_character_per_server")
        {
            // Brakes the server applies on purpose, not failures: wait as told and say so calmly. They stay out of error.log
            // (and so out of the usage counts' errors). daily_limit: a long history goes up over a few days;
            // one_character_per_server: another character on this game server shares from this connection today.
            _waitUntil = DateTime.Now.AddSeconds(ex.RetryAfter ?? 3600);
            Notice = ex.Message;
        }
        catch (ShareApiException ex)
        {
            Error = ex.Message;
            if (ex.Transient) _waitUntil = DateTime.Now.AddMinutes(5);
            else if (ex.Code == "unknown_install") await Ui(TurnedOffElsewhere);
            if (!ex.Transient) AppSettings.LogError("sharing: " + what, ex);
        }
        catch (Exception ex)
        {
            Error = "Sharing hit a problem; details are in error.log.";
            AppSettings.LogError("sharing: " + what, ex);
        }
        finally
        {
            Busy = false;
            _gate.Release();
            Raise();
        }
    }

    /// <summary>The server's switches (enrollment) and, when sharing is on, what it holds for this player.</summary>
    private Task RefreshAsync() => Run(async () =>
    {
        using var probe = ShareProtocol.NewKey();
        Config ??= await Client(probe).ConfigAsync();
        if (_settings.ShareEnabled && (_key ??= ShareIdentity.Load(_settings)) is { } key) Server = await Client(key).MeAsync();
    }, "status");

    /// <summary>Turns sharing on from outside the Settings card (the welcome tour, the Home card), with the invite code
    /// the player typed while sharing is invite-only.</summary>
    internal Task TurnOn(bool everything, string? code = null) =>
        TurnOnAsync(everything, string.IsNullOrWhiteSpace(code) ? null : code.Trim(), null);

    /// <summary>Whether the server wants an invite code to start sharing (null when it can't be reached): for the welcome
    /// tour, which runs before this service exists.</summary>
    internal static async Task<bool?> RequiresCodeAsync(AppSettings settings)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var probe = ShareProtocol.NewKey();
            var api = new Uri(Environment.GetEnvironmentVariable("EQLPATHFINDER_API") is { Length: > 0 } e ? e
                : settings.ShareApi is { Length: > 0 } s ? s : ShareProtocol.DefaultApi);
            return (await new ShareClient(http, api, probe).ConfigAsync()).RequireEnrollment;
        }
        catch (Exception ex)
        {
            AppSettings.LogError("sharing config (tour)", ex);
            return null;
        }
    }

    private Task TurnOnAsync(bool everything, string? enroll, string? link) => Run(async () =>
    {
        var key = _key ??= ShareIdentity.Load(_settings) ?? await Ui(() => { var k = ShareIdentity.Create(_settings); _settings.Save(); return k; });
        var client = Client(key);
        Config = await client.ConfigAsync();
        if (Config.RequireEnrollment && enroll == null && link == null)
        {
            Error = "Sharing is invite-only for now: it needs an invite code from someone who already shares, or a link code from your other PC.";
            return;
        }
        var reg = await client.RegisterAsync(Version, enroll, link);
        await Ui(() =>
        {
            if (reg.Salt is { Length: > 0 } salt) _settings.ShareSalt = salt;   // linked: the same character codes as the other PC
            _settings.ShareInstall = reg.Install;
            _settings.SharePlayer = reg.Player;
            _settings.ShareEnabled = true;
            _settings.SharePaused = false;
            _settings.ShareFrom = everything ? null : DateTime.Now;
            _settings.Save();
        });
        Notice = link != null ? "Linked: this PC shares as the same player as your other one." : "Sharing is on.";
        _waitUntil = default;
    }, "turning on").ContinueWith(_ => SyncAsync()).Unwrap();

    /// <summary>Sends every new or changed unit. Runs every 10 minutes, and right after turning on or resuming.</summary>
    public Task SyncAsync()
    {
        if (!_settings.ShareEnabled || _settings.SharePaused || DateTime.Now < _waitUntil || _app.Service == null) return Task.CompletedTask;
        // A copy on its own data (developer checks) never uploads, whatever its settings say: they're often a copy of the
        // real ones, key included. The sharing self-test is the exception: uploading is what it checks.
        if (AppSettings.IsolatedDataDir != null && Environment.GetEnvironmentVariable("EQLPATHFINDER_SELFTEST") != "share") return Task.CompletedTask;
        return Run(async () =>
        {
            if (_app.Service is not { } svc || (_key ??= ShareIdentity.Load(_settings)) is not { } key)
            {
                if (_key == null) Error = "This PC's sharing key can't be read (settings copied from another PC or Windows user?). Turn sharing off and on again.";
                return;
            }
            var client = Client(key);
            var salt = await Ui(() => _app.ShareSalt());
            var version = Version;
            var since = _settings.ShareFrom;
            // Trios are checked against the spells cast: build once the spell file is in, or the trios would change later.
            await Task.Run(() => svc.WaitForGameData(TimeSpan.FromSeconds(60)));
            var payload = await svc.Query(s => s.SharePayload(version, salt, since));
            var units = await Task.Run(() => ShareUnits.From(payload));
            var ledger = ShareLedger.Load(_settings.SharePlayer);
            var sent = ledger.For(payload.Character);
            var todo = units.Where(u => !sent.TryGetValue(u.Id, out var h) || h != u.Hash).ToList();
            Pending = todo.Count;
            Raise();
            foreach (var batch in ShareUnits.Batches(payload, todo))
            {
                await client.UploadAsync(batch);
                foreach (var u in batch.Rows) sent[u.Id] = u.Hash;
                _sentAny = true;
                ledger.Save();
                Pending = Math.Max(0, Pending - batch.Rows.Count);
                LastUpload = DateTime.Now;
                Raise();
            }
            Server = await client.MeAsync();
        }, "upload");
    }

    private Task DeleteEverythingAsync() => Run(async () =>
    {
        if ((_key ??= ShareIdentity.Load(_settings)) is not { } key) { await Ui(ForgetLocally); return; }
        var gone = await Client(key).DeleteEverythingAsync();
        await Ui(ForgetLocally);
        Notice = $"Deleted {gone.Deleted:N0} shared rows from the server{(gone.Pcs > 1 ? $", for all {gone.Pcs} linked PCs" : "")}. Sharing is off.";
    }, "deleting");

    private Task MakeLinkCodeAsync() => Run(async () =>
    {
        if ((_key ??= ShareIdentity.Load(_settings)) is not { } key) return;
        var salt = await Ui(() => Convert.ToBase64String(_app.ShareSalt()));
        LinkCode = await Client(key).LinkAsync(salt);
    }, "link code");

    /// <summary>This PC already shares and joins the player on the other PC: new salt, and every unit goes again.</summary>
    private Task ClaimAsync(string code) => Run(async () =>
    {
        if ((_key ??= ShareIdentity.Load(_settings)) is not { } key) return;
        var claim = await Client(key).ClaimAsync(code);
        await Ui(() =>
        {
            if (claim.Salt is { Length: > 0 } salt) _settings.ShareSalt = salt;
            _settings.SharePlayer = claim.Player;
            _settings.Save();
        });
        ShareLedger.Forget();
        Notice = "Linked: this PC now shares as the same player as your other one. Its crawls are being sent again with the shared codes.";
        _waitUntil = default;
    }, "linking").ContinueWith(_ => SyncAsync()).Unwrap();

    private Task MakeEnrollCodeAsync() => Run(async () =>
    {
        if ((_key ??= ShareIdentity.Load(_settings)) is not { } key) return;
        EnrollCode = await Client(key).EnrollAsync();
    }, "enrollment code");

    /// <summary>After "Delete everything": no key, no IDs, a fresh salt next time. UI thread.</summary>
    private void ForgetLocally()
    {
        _settings.ShareEnabled = false;
        _settings.SharePaused = false;
        _settings.ShareFrom = null;
        _settings.ShareKey = null;
        _settings.ShareInstall = null;
        _settings.SharePlayer = null;
        _settings.ShareSalt = null;
        _settings.Save();
        _key?.Dispose();
        _key = null;
        Server = null;
        LinkCode = null;
        EnrollCode = null;
        Pending = 0;
        LastUpload = null;
        ShareLedger.Forget();
        _sentAny = false;
    }

    /// <summary>The server no longer knows this install (deleted from the linked PC, or by the operator). UI thread.</summary>
    private void TurnedOffElsewhere()
    {
        ForgetLocally();
        Error = "Sharing was switched off: the server no longer has this PC (its shared data was deleted, perhaps from your other PC). Turn it on again to start over.";
    }

    public void Dispose()
    {
        _timer.Dispose();
        _http.Dispose();
        _key?.Dispose();
    }
}
