using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.App;

public partial class App
{
    private void StartSelfTestIfRequested()
    {
        if (AppSettings.IsolatedDataDir == null) return;
        switch (Environment.GetEnvironmentVariable("EQLPATHFINDER_SELFTEST"))
        {
            case "1": _ = SelfTestReportAsync(); break;
            case "share": _ = SelfTestShareAsync(); break;
            case "community": _ = SelfTestCommunityAsync(); break;
            case "overlays": _ = SelfTestOverlaysAsync(); break;
            case "help": _ = SelfTestHelpAsync(); break;
            case "loadout": _ = SelfTestLoadoutAsync(); break;
            case "usage": _ = SelfTestUsageAsync(); break;
            case "mock": _ = SelfTestMockAsync(); break;
        }
    }

    /// <summary>
    /// Developer mock-ups in an isolated copy: opens the main window, runs the script in the file EQLPATHFINDER_MOCK names
    /// (it can draw anything on the real page), saves a PNG to EQLPATHFINDER_MOCK_OUT and exits.
    /// </summary>
    private async Task SelfTestMockAsync()
    {
        try
        {
            for (int i = 0; i < 720 && LastSnapshot == null; i++) await Task.Delay(250);
            OpenMain();
            for (int i = 0; i < 240 && _main?.Ready != true; i++) await Task.Delay(250);
            await Task.Delay(2500);
            await _main!.RunScriptAsync(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("EQLPATHFINDER_MOCK")!));
            // EQLPATHFINDER_MOCK_WAIT: milliseconds to let the page settle (an Ask answer takes a few seconds).
            await Task.Delay(int.TryParse(Environment.GetEnvironmentVariable("EQLPATHFINDER_MOCK_WAIT"), out var wait) ? wait : 800);
            await _main.CaptureAsync(Environment.GetEnvironmentVariable("EQLPATHFINDER_MOCK_OUT")!);
        }
        catch (Exception ex) { AppSettings.LogError("mock", ex); }
        Quit();
    }

    /// <summary>
    /// Developer self-test for loadout following in an isolated copy with a stand-in game folder (inside
    /// this copy's data folder, never the real one; the settings point at its Logs): touches slot 6's files, then slot 1's, and times
    /// how long the snapshot, the title chip, the overlay and the meter take to follow, failing past 15 s. Writes selftest-loadout.txt and exits.
    /// </summary>
    private async Task SelfTestLoadoutAsync()
    {
        var log = new List<string>();
        try
        {
            for (int i = 0; i < 240 && (LastSnapshot == null || _service == null); i++) await Task.Delay(250);
            var logs = _service?.Logs ?? throw new InvalidOperationException("no tracked character");
            var gameDir = Path.GetDirectoryName(Path.GetDirectoryName(logs.LivePath))!;
            if (!Path.GetFullPath(gameDir).StartsWith(Path.GetFullPath(AppSettings.DataDir), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("the stand-in game folder must be inside this isolated copy's data folder: " + gameDir);
            async Task<double> Swap(int slot)
            {
                var sw = Stopwatch.StartNew();
                foreach (var name in new[] { $"{logs.Name}_{logs.Server}_LO{slot}.ini", $"UI_{logs.Name}_{logs.Server}_LO{slot}.ini" })
                    File.WriteAllText(Path.Combine(gameDir, name), "[stand-in]" + Environment.NewLine);
                while (sw.Elapsed.TotalSeconds < 20 && LastSnapshot?.Loadout?.Slot != slot) await Task.Delay(100);
                double secs = sw.Elapsed.TotalSeconds;
                // The game writes lines as it plays; the meter reads the loadout at the log's last line, so one after the swap.
                File.AppendAllText(logs.LivePath, $"[{DateTime.Now:ddd MMM dd HH:mm:ss yyyy}] You have entered The Plane of Knowledge.{Environment.NewLine}");
                for (int i = 0; i < 100 && _meter?.Frame?.NotKnown != true; i++) await Task.Delay(100);
                return secs;
            }
            var before = LastSnapshot?.Loadout;
            log.Add($"start: loadout {(before == null ? "none" : before.Slot + " " + before.State)}");
            foreach (var slot in new[] { 6, 1 })
            {
                double secs = await Swap(slot);
                var snap = LastSnapshot!;
                var (chip, tip) = EqlPathfinder.App.MainWindow.ChipOf(snap);
                var home = JsonSerializer.Serialize(_service!.ReincarnationData(), PageJson.Options);
                var homeLoadout = Regex.Match(home, @"""loadout"":\{[^}]*\}").Value;
                var ov = new OverlayWindow(_settings);
                ov.Update(snap);
                var meterFrame = _meter?.Frame;
                log.Add($"swap to {slot}: snapshot {snap.Loadout?.Slot} {snap.Loadout?.State} after {secs:0.0} s");
                log.Add($"  title chip \"{chip}\" tip \"{tip}\"");
                log.Add($"  home {homeLoadout}");
                log.Add($"  overlay footer \"{ov.FootRight.Text}\"");
                log.Add($"  meter frame: not known {meterFrame?.NotKnown.ToString() ?? "no meter yet"}");
                if (snap.Loadout?.Slot != slot || secs > 15) log.Add($"FAILED: loadout {slot} not followed within 15 s ({secs:0.0} s)");
                await Task.Delay(3000);
            }
        }
        catch (Exception ex) { log.Add("FAILED: " + ex); }
        File.WriteAllLines(Path.Combine(AppSettings.DataDir, "selftest-loadout.txt"), log);
        Shutdown();
    }

    /// <summary>
    /// Developer check of usage counts in an isolated copy: opens pages and an Ask question in the real window, logs a
    /// scrubbed error, captures Settings with the preview (selftest-usage-settings.png), sends when EQLPATHFINDER_USAGE=1,
    /// then switches usage off (the server deletes this copy's rows) and on again; writes selftest-usage.txt and exits.
    /// </summary>
    private async Task SelfTestUsageAsync()
    {
        var log = new List<string>();
        static async Task Until(Func<bool> done, int seconds)
        {
            for (int i = 0; i < seconds * 4 && !done(); i++) await Task.Delay(250);
        }
        try
        {
            await Until(() => LastSnapshot != null, 180);
            OpenMain();
            await Until(() => _main?.Ready == true, 60);
            await Task.Delay(1500);
            foreach (var p in new[] { "ask", "upgrade", "settings" })
            {
                await _main!.RunScriptAsync($"show('{p}')");
                await Task.Delay(p == "ask" ? 4000 : 800);
            }
            AppSettings.LogError("selftest probe C:\\Users\\Bob", new IOException(@"C:\Users\Bob\AppData\Local\EQL Pathfinder\x for Venun"));
            await _main!.RunScriptAsync("post('usagePreview')");
            await Task.Delay(1500);
            await _main.RunScriptAsync("document.querySelector('[data-setting=usage]')?.scrollIntoView({ block: 'center' })");
            await Task.Delay(500);
            await _main.CaptureAsync(Path.Combine(AppSettings.DataDir, "selftest-usage-settings.png"));
            var preview = _usage!.PreviewText();
            log.Add("preview:\n" + preview);
            log.Add("usage.json exists before send: " + File.Exists(Path.Combine(AppSettings.DataDir, "usage.json")));
            if (Environment.GetEnvironmentVariable("EQLPATHFINDER_USAGE") == "1")
            {
                log.Add("send: " + await _usage.SendNowAsync());
                log.Add("pending after send: " + _usage.PendingCount());
            }
            // EQLPATHFINDER_USAGE_KEEP=1 leaves what was sent on the server, to look at it there.
            if (Environment.GetEnvironmentVariable("EQLPATHFINDER_USAGE_KEEP") == "1") { log.Add("kept on the server: id " + _settings.UsageId); throw new OperationCanceledException("kept"); }
            using (var off = JsonDocument.Parse("false")) ApplySetting("usage", off.RootElement);
            await Task.Delay(3000);
            log.Add($"off: enabled={_settings.UsageEnabled} usage.json exists={File.Exists(Path.Combine(AppSettings.DataDir, "usage.json"))} " +
                    $"server: {_usage.LastForget ?? "(no answer yet)"} preview=\"{_usage.PreviewText()}\"");
            using (var on = JsonDocument.Parse("true")) ApplySetting("usage", on.RootElement);
            log.Add($"on again: enabled={_settings.UsageEnabled} id={_settings.UsageId}");
        }
        catch (Exception ex) { log.Add("FAILED: " + ex); }
        File.WriteAllLines(Path.Combine(AppSettings.DataDir, "selftest-usage.txt"), log);
        Quit();
    }

    /// <summary>
    /// Developer check of Help against the live server in an isolated copy: the roadmap, a vote and taking it back, and one
    /// feedback note marked "selftest" (delete it from the feedback table afterwards); writes selftest-help.txt and exits.
    /// </summary>
    private async Task SelfTestHelpAsync()
    {
        var log = new List<string>();
        try
        {
            var client = new FeedbackClient(_settings, "selftest");
            static string J(object o) => JsonSerializer.Serialize(o, PageJson.Options);
            var roadmap = J(await client.RoadmapAsync());
            log.Add($"roadmap: {roadmap.Length} chars, items {roadmap.Split("\"id\":").Length - 1}, voter {_settings.VoterId}");
            var on = J(await client.VoteAsync("hunter", true));
            log.Add("vote on: " + (on.Contains("\"id\":\"hunter\"") && Regex.IsMatch(on, "\"id\":\"hunter\"[^}]*\"mine\":true")));
            var off = J(await client.VoteAsync("hunter", false));
            log.Add("vote off: " + Regex.IsMatch(off, "\"id\":\"hunter\"[^}]*\"mine\":false"));
            log.Add("feedback: " + J(await client.SendAsync("other", "selftest: please ignore", null, false, "selftest")));
        }
        catch (Exception ex) { log.Add("FAILED: " + ex); }
        File.WriteAllLines(Path.Combine(AppSettings.DataDir, "selftest-help.txt"), log);
        Shutdown();
    }

    /// <summary>
    /// Developer check of the overlays in an isolated copy: the settings as migrated, arranging (each overlay shown,
    /// unlocked and named, saved as overlay-arranging-&lt;id&gt;.png), done arranging, lock all; writes selftest-overlays.txt and exits.
    /// </summary>
    private async Task SelfTestOverlaysAsync()
    {
        var log = new List<string>();
        string Prefs(string id)
        {
            var p = _settings.Overlay(id);
            return $"{id}: visible={p.Visible} locked={p.Locked} at {p.Left:0},{p.Top:0} background={p.Opacity:0.##} " +
                   string.Join(" ", p.Options.Select(kv => kv.Key + "=" + kv.Value));
        }
        static void Snap(Window w, string name)
        {
            var bmp = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth), (int)Math.Ceiling(w.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(w);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(Path.Combine(AppSettings.DataDir, $"overlay-arranging-{name}.png"));
            png.Save(fs);
        }
        try
        {
            log.Add($"revision {_settings.Revision}, only with the game {_settings.OverlaysOnlyWithGame}, old fields left: {_settings.OverlayLeft != null || _settings.MeterVisible != null}");
            log.Add(Prefs(OverlayKinds.Crawl));
            log.Add(Prefs(OverlayKinds.Meter));
            SetArranging(true);
            await Task.Delay(2000);
            log.Add($"arranging: crawl shown {_overlay?.IsVisible}, meter shown {_meterWindow?.IsVisible}");
            if (_overlay?.IsVisible == true) Snap(_overlay, "crawl");
            if (_meterWindow?.IsVisible == true) Snap(_meterWindow, "meter");
            SetArranging(false);
            await Task.Delay(500);
            log.Add($"done arranging: crawl shown {_overlay?.IsVisible}, meter shown {_meterWindow?.IsVisible} (game running {GameRunning}, only with the game {_settings.OverlaysOnlyWithGame})");
            bool wasLocked = AllOverlaysLocked;
            SetAllOverlaysLocked(true);
            log.Add($"lock all: {AllOverlaysLocked} ({string.Join(", ", OverlayKinds.All.Select(k => k.Id + " " + _settings.Overlay(k.Id).Locked))})");
            SetAllOverlaysLocked(wasLocked);
            _settings.Save();
            var json = File.ReadAllText(Path.Combine(AppSettings.DataDir, "settings.json"));
            log.Add($"saved settings: old fields {json.Contains("\"OverlayLeft\"") || json.Contains("\"MeterVisible\"")}, Overlays {json.Contains("\"Overlays\"")}");
        }
        catch (Exception ex) { log.Add("FAILED: " + ex); }
        File.WriteAllLines(Path.Combine(AppSettings.DataDir, "selftest-overlays.txt"), log);
        Shutdown();
    }

    /// <summary>
    /// Developer check of the community numbers in an isolated copy: join the beta (the file arrives and is saved), ask a
    /// question that uses it, read the saved copy back, leave the beta (the numbers go); writes selftest-community.txt and exits.
    /// </summary>
    private async Task SelfTestCommunityAsync()
    {
        var log = new List<string>();
        var saved = Path.Combine(AppSettings.DataDir, "community-v1.json");
        try
        {
            if (File.Exists(saved)) File.Delete(saved);
            log.Add($"start: beta={_settings.Beta} sharing={_settings.ShareEnabled} current={_community?.Current != null}");
            SetBeta(true);
            for (int i = 0; i < 160 && (_community?.Current == null || _service?.Community == null); i++) await Task.Delay(250);
            var c = _community?.Current;
            log.Add($"beta on: players={c?.File.Players} crawls={c?.File.Crawls} generated={c?.File.Generated:O} saved={File.Exists(saved)} service={_service?.Community != null}");
            if (_service is { } svc)
            {
                var json = await svc.Query(s => JsonSerializer.Serialize(s.AskPage(new AskQuery("level", 11, 34, "fast", null, null, null)), PageJson.Options));
                using var doc = JsonDocument.Parse(json);
                var route = doc.RootElement.GetProperty("route");
                log.Add($"ask 11-34: community={(doc.RootElement.GetProperty("community").ValueKind != JsonValueKind.Null)} " +
                        $"route={route.GetProperty("community").ToString()[..Math.Min(160, route.GetProperty("community").ToString().Length)]}");
            }
            using (var again = new CommunityFeed(this, _settings, "selftest"))
            {
                again.LoadCached();
                log.Add($"saved copy reads back: {again.Current?.File.Generated == c?.File.Generated && again.Current != null}");
            }
            SetBeta(false);
            for (int i = 0; i < 40 && _service?.Community != null; i++) await Task.Delay(250);
            log.Add($"beta off: current={_community?.Current != null} service={_service?.Community != null}");
        }
        catch (Exception ex) { log.Add("FAILED: " + ex); }
        File.WriteAllLines(Path.Combine(AppSettings.DataDir, "selftest-community.txt"), log);
        Shutdown();
    }

    /// <summary>
    /// Developer check of sharing in an isolated copy: turn on (everything), upload, read what the server holds, upload again
    /// (nothing should go), delete everything, then write selftest-share.json and exit.
    /// </summary>
    private async Task SelfTestShareAsync()
    {
        var log = new List<string>();
        void Note(string what) => log.Add($"{DateTime.Now:HH:mm:ss.fff} {what}: enabled={_settings.ShareEnabled} busy={_sharing?.Busy} pending={_sharing?.Pending} " +
            $"error={_sharing?.Error} notice={_sharing?.Notice} server={JsonSerializer.Serialize(_sharing?.Server?.Counts)} withdrawal={_sharing?.Withdrawal}");
        static async Task Until(Func<bool> done, int seconds)
        {
            for (int i = 0; i < seconds * 4 && !done(); i++) await Task.Delay(250);
        }
        try
        {
            await Until(() => LastSnapshot != null, 120);
            Note("ready");
            using var on = JsonDocument.Parse("{\"action\":\"on\",\"history\":\"all\"}");
            _sharing!.Handle(on.RootElement);
            await Until(() => _settings.ShareEnabled && _sharing.Server != null && !_sharing.Busy || _sharing.Error != null, 300);
            Note("after first sync");
            var first = _sharing.Server?.Counts.Values.Sum() ?? 0;
            // One id the build doesn't hold goes into the copy's ledger, so the second sync has something to withdraw.
            const string stale = "deadbeefdeadbeefdeadbeefdeadbeef";
            var ledger = ShareLedger.Load(_settings.SharePlayer);
            var character = ledger.Characters.Keys.FirstOrDefault();
            if (character != null) { ledger.For(character)[stale] = "stale"; ledger.Save(); }
            log.Add($"stale id {stale} put in the ledger for {character}");
            await _sharing.SyncAsync();
            Note("after second sync");
            log.Add($"ledger still holds the stale id after the second sync: {character != null && ShareLedger.Load(_settings.SharePlayer).For(character).ContainsKey(stale)}");
            using var link = JsonDocument.Parse("{\"action\":\"link\"}");
            _sharing.Handle(link.RootElement);
            await Until(() => _sharing.LinkCode != null || _sharing.Error != null, 30);
            Note($"link code {_sharing.LinkCode?.Code}");
            using var del = JsonDocument.Parse("{\"action\":\"delete\"}");
            _sharing.Handle(del.RootElement);
            await Until(() => !_settings.ShareEnabled && !_sharing.Busy, 60);
            Note("after delete");
            log.Add($"first sync put {first} units on the server; key kept after delete: {_settings.ShareKey != null}");
        }
        catch (Exception ex) { log.Add(ex.ToString()); }
        File.WriteAllLines(Path.Combine(AppSettings.DataDir, "selftest-share.txt"), log);
        Quit();
    }

    /// <summary>Developer check after the tour's self-test: what got saved and counted, then exit.</summary>
    private async Task SelfTestReportAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(15));
        object? roster = null;
        string? error = null;
        try { roster = await _service!.Query(s => s.RosterCounts()); }
        catch (Exception ex) { error = ex.ToString(); }
        var report = new
        {
            character = _settings.Character,
            logsDir = _settings.LogsDir,
            welcomeSeen = _settings.WelcomeSeen,
            overlaySize = _settings.Overlay(OverlayKinds.Crawl).Option("size", "Compact"),
            entered = _settings.EnteredCounts,
            mainOpen = _main?.IsVisible == true,
            logOff = LogOff,
            gameRunning = GameRunning,
            error,
            roster,
        };
        File.WriteAllText(Path.Combine(AppSettings.DataDir, "selftest.json"), JsonSerializer.Serialize(report, PageJson.Options));
        Quit();
    }
}
