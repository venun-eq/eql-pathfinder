using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Upgrades;
using Microsoft.Web.WebView2.Core;

namespace EqlPathfinder.App;

/// <summary>
/// The main window: Pathfinder's own title bar (WPF) over web/index.html in WebView2, which draws Home, Plan,
/// Roster, Route, Crawls, Loot, Help and Settings. The page asks for changes with small JSON messages.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x8F, 0xD1, 0x8F));
    private static readonly Brush Grey = new SolidColorBrush(Color.FromRgb(0x8D, 0x95, 0x9E));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xE8, 0xB4, 0x6A));

    private readonly App _app;
    private readonly DispatcherTimer _timer, _placeTimer;
    private bool _ready;

    /// <summary>Remembers where the window is (debounced while you drag; always on close and before an update restart).</summary>
    public void SavePlacement()
    {
        if (Chrome.GetPlacement(this, _app.Settings.MainWindowPlace) is not { } place) return;
        _app.Settings.MainWindowPlace = place;
        _app.Settings.Save();
    }

    public MainWindow(App app)
    {
        InitializeComponent();
        _app = app;
        Chrome.Apply(this);
        Chrome.FillWordmark(Wordmark);
        MinBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        MaxBtn.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        CloseBtn.Click += (_, _) => Close();
        UpdateBtn.Click += (_, _) => _app.RestartToUpdate();
        StateChanged += (_, _) => ApplyState();

        // Come back where you left it: same monitor, size and maximized state, across restarts and updates.
        if (_app.Settings?.MainWindowPlace != null) WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) =>
        {
            if (_app.Settings?.MainWindowPlace is { } place) Chrome.SetPlacement(this, place, WindowState == WindowState.Minimized);
        };
        Loaded += (_, _) => ApplyState();
        _placeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _placeTimer.Tick += (_, _) => { _placeTimer.Stop(); SavePlacement(); };
        void Moved(object? s, EventArgs e) { if (IsLoaded) { _placeTimer.Stop(); _placeTimer.Start(); } }
        LocationChanged += Moved;
        SizeChanged += (s, e) => Moved(s, e);
        StateChanged += Moved;
        Closing += (_, _) => { _placeTimer.Stop(); SavePlacement(); };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => { if (IsVisible && WindowState != WindowState.Minimized) Refresh(); };
        Loaded += async (_, _) => await InitAsync();
        Closed += (_, _) => _timer.Stop();
        UpdateChrome();
    }

    private void ApplyState()
    {
        bool max = WindowState == WindowState.Maximized;
        Root.Margin = max ? Chrome.MaximizedInset() : new Thickness(0);
        Web.Margin = max ? new Thickness(0) : new Thickness(3, 0, 3, 3);
        MaxIcon.Data = Geometry.Parse(max ? "M2.5,0.5 H9.5 V7.5 M0.5,2.5 H7.5 V9.5 H0.5 Z" : "M0.5,0.5 H9.5 V9.5 H0.5 Z");
        MaxBtn.ToolTip = max ? "Restore" : "Maximize";
    }

    /// <summary>
    /// The title bar's chip, "50 · PAL/MNK/SHM · Froglok", and its tooltip. An unmet or modified loadout
    /// says so and never shows the previous loadout.
    /// </summary>
    internal static (string Text, string? Tip) ChipOf(EqlPathfinder.Engine.Live.LiveSnapshot? snap)
    {
        if (snap?.Loadout is { State: "new" or "classes" } lo)
        {
            var parts = new[] { snap.Level?.ToString(), lo.State == "new" ? (snap.Level == null ? $"Loadout {lo.Slot} not known yet" : $"loadout {lo.Slot} not known yet") : "classes not known yet",
                lo.State == "classes" ? snap.Race : null }.Where(p => !string.IsNullOrEmpty(p));
            return (string.Join(" · ", parts), $"Type /who in game once and Pathfinder knows loadout {lo.Slot} from then on.");
        }
        return (string.Join(" · ", new[] { snap?.Level?.ToString(), snap?.Trio, snap?.Race }.Where(p => !string.IsNullOrEmpty(p))), null);
    }

    /// <summary>Who is tracked, whether the game is running, and an update waiting to install.</summary>
    public void UpdateChrome()
    {
        var logs = _app.Service?.Logs;
        WhoText.Text = logs?.Name ?? "";
        ServerText.Text = logs is { Server.Length: > 0 } l ? " · " + char.ToUpperInvariant(l.Server[0]) + l.Server[1..] : "";
        var snap = _app.LastSnapshot;
        var (chip, tip) = ChipOf(snap);
        WhoChip.Visibility = chip.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        WhoChipText.Text = chip;
        WhoChip.ToolTip = tip;
        bool game = _app.GameRunning;
        if (_app.LogOff)
        {
            StatusDot.Fill = StatusText.Foreground = Amber;
            StatusText.Text = "EverQuest is running · log is off: type /log on";
            StatusText.ToolTip = $"No new line in {logs?.Name ?? "your"}'s log since you logged in. In game, type /log on (it stays on).";
        }
        else
        {
            StatusDot.Fill = game ? Green : Grey;
            StatusText.Foreground = game ? Green : Grey;
            StatusText.Text = game ? "Tracking · EverQuest is running" : "Waiting for EverQuest Legends";
            StatusText.ToolTip = null;
        }
        var pending = _app.PendingUpdate;
        UpdateBtn.Visibility = pending != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateText.Text = $"Update {pending} ready · Restart";
    }

    private async Task InitAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppSettings.DataDir, "WebView2"));
            await Web.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            Web.Visibility = Visibility.Collapsed;
            Fallback.Visibility = Visibility.Visible;
            Fallback.Text = "This window needs the Microsoft Edge WebView2 Runtime, which comes with Windows 11. " +
                            "Install it from https://developer.microsoft.com/microsoft-edge/webview2/ and reopen this window. " +
                            "The overlay works without it.";
            return;
        }
        var core = Web.CoreWebView2;
        WebViewSecurity.Configure(core, "index.html", OnMessage);
        // Report a problem's shortcut. WebView2 hands keys pressed with Ctrl to WPF before the browser acts on them, and
        // Ctrl+Shift+R is the browser's hard reload, so it's taken here and the page opens its panel (report.js).
        Web.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.R || System.Windows.Input.Keyboard.Modifiers !=
                (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift)) return;
            e.Handled = true;
            _ = Web.CoreWebView2?.ExecuteScriptAsync("openReport()");
        };
        core.NavigationCompleted += (_, _) =>
        {
            _ready = true;
            Refresh();
            if (_goWhenReady is var (page, focus)) { _goWhenReady = null; Go(page, focus); }
        };
        core.Navigate(WebViewPolicy.AppUrl("index.html"));
        _timer.Start();
    }

    /// <summary>Messages from the page: "refresh", or {type, ...} for settings and actions.</summary>
    private void OnMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var m = doc.RootElement;
            if (m.ValueKind == JsonValueKind.String)
            {
                if (m.GetString() == "refresh") Refresh();
                return;
            }
            string type = m.GetProperty("type").GetString() ?? "";
            switch (type)
            {
                case "farmRate": _app.SetFarmRate(m.GetProperty("value").GetDouble()); break;
                case "campaign":
                    _app.SetCampaign(m.GetProperty("value").Deserialize<CampaignPlan>(PageJson.Options) ?? new CampaignPlan(),
                        m.TryGetProperty("rebuild", out var rb) && rb.ValueKind == JsonValueKind.True,
                        n: m.TryGetProperty("n", out var cn) && cn.ValueKind == JsonValueKind.Number ? cn.GetInt32() : 0);
                    return;
                case "campaignOptions":
                    SendCampaignOptions(m.GetProperty("value").Deserialize<CampaignPlan>(PageJson.Options) ?? new CampaignPlan(), m.GetProperty("index").GetInt32());
                    return;
                case "comboPrice":
                    SendComboPrice(m.GetProperty("value").Deserialize<CampaignPlan>(PageJson.Options) ?? new CampaignPlan(),
                        m.GetProperty("combo").Deserialize<CampaignCombo>(PageJson.Options) ?? new CampaignCombo(),
                        m.TryGetProperty("index", out var ci) && ci.ValueKind == JsonValueKind.Number ? ci.GetInt32() : null, m.GetProperty("n").GetInt32());
                    return;
                case "routeMetric": _app.SetGoal(m.GetProperty("value").GetString() ?? "balanced"); break;
                case "setting": _app.ApplySetting(m.GetProperty("key").GetString() ?? "", m.GetProperty("value")); break;
                case "loot": SendLoot(); return;
                case "upgrade": SendUpgrade(m.TryGetProperty("value", out var uv) ? uv.Clone() : null); return;
                case "upgradeBank":
                    _app.SetBank(m.GetProperty("value"));
                    SendUpgrade(m.TryGetProperty("request", out var rq) ? rq.Clone() : null);
                    return;
                case "sharePreview": UsageService.Metrics.Feature("share-preview"); SendSharePreview(); return;
                // Usage counts: a page opened (the page's own names only), and Settings → See what's sent.
                case "seen" when m.TryGetProperty("page", out var sp) && sp.GetString() is "home" or "ask" or "campaign" or "classroles" or "roster" or "crawls"
                    or "loot" or "relevels" or "upgrade" or "help" or "settings":
                    UsageService.Metrics.Page(sp.GetString()!);
                    return;
                case "notice" when m.TryGetProperty("id", out var ni) && ni.GetString() is { Length: > 0 and <= 40 } nid && nid.All(c => c is (>= 'a' and <= 'z') or '-')
                    && m.TryGetProperty("action", out var na) && na.GetString() is "ok" or "see" or "off":
                    _app.NoticeSeen(nid, na.GetString()!);
                    return;
                case "usagePreview":
                    UsageService.Metrics.Feature("usage-preview");
                    Web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new { kind = "usagePreview", text = _app.Usage?.PreviewText() ?? "" }, PageJson.Options));
                    return;
                case "share": _app.Sharing?.Handle(m); SendShareStatus(); return;
                case "ask": SendAsk(m); return;
                // Help: what's coming (with votes), a vote, feedback and your reports, all on the community server.
                case "roadmap": PostAsync(Feedback.RoadmapAsync()); return;
                case "myReports": PostAsync(Feedback.MineAsync()); return;
                case "vote" when m.TryGetProperty("item", out var vi) && vi.GetString() is { } item:
                    UsageService.Metrics.Feature("vote");
                    PostAsync(Feedback.VoteAsync(item, !m.TryGetProperty("on", out var von) || von.ValueKind != JsonValueKind.False));
                    return;
                // Report a problem: the page hides names, clock times and folders, then asks for the picture.
                case "reportShot": PostAsync(ReportShotAsync()); return;
                case "feedback":
                    {
                        string? S(string k) => m.TryGetProperty(k, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
                        bool report = S("from") == "report";
                        var shot = report ? ShotBytes(S("shot")) : null;
                        UsageService.Metrics.Feature(report ? (shot != null ? "report-shot" : "report") : "feedback");
                        PostAsync(Feedback.SendAsync(S("kind") ?? "other", S("text") ?? "", S("contact"),
                            m.TryGetProperty("errors", out var fe) && fe.ValueKind == JsonValueKind.True,
                            S("page") is { } fp && PageNames.Contains(fp) ? fp : "help",
                            report ? "report" : "help", report ? _reportDetails : null, shot));
                        return;
                    }
                case "counts": _app.SetCountsFromPage(m.GetProperty("value")); return;
                case "countsReset": _app.SetCountsFromPage(null); return;
                case "action":
                    // Deferred: an action can close this window, which must not happen inside WebView2's own callback.
                    var action = m.GetProperty("value").GetString() ?? "";
                    Dispatcher.BeginInvoke(() => _app.RunAction(action));
                    return;
                default: return;
            }
            Refresh();
        }
        catch (Exception ex) { AppSettings.LogError("main window message", ex); }
    }

    /// <summary>Every loot row, sent only while the Loot page asks for it (it's the biggest list Pathfinder keeps).</summary>
    private async void SendLoot()
    {
        var svc = _app.Service;
        if (!_ready || svc == null || Web.CoreWebView2 == null) return;
        try
        {
            var rows = await svc.Query(s => s.AllLootCompact());
            var json = await Task.Run(() => JsonSerializer.Serialize(new { kind = "loot", rows }, PageJson.Options));
            Web.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex) { AppSettings.LogError("loot rows", ex); }
    }

    /// <summary>Campaign → Swap… and the picker: what each change to one loadout would do, from the plan as the page has it.</summary>
    private async void SendCampaignOptions(CampaignPlan plan, int index)
    {
        var svc = _app.Service;
        if (!_ready || svc == null || Web.CoreWebView2 == null) return;
        try
        {
            var roles = PlannerSwitch.RolesFor(_app.Settings, svc.Logs.Key);
            var options = await svc.Query(s => s.CampaignOptions(plan, index, roles));
            var json = await Task.Run(() => JsonSerializer.Serialize(new { kind = "campaignOptions", index, options }, PageJson.Options));
            Web.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex) { AppSettings.LogError("campaign options", ex); }
    }

    /// <summary>Your combos → the form's "What it does": the price of the combo as the form has it, answered with the <paramref name="n"/> it was asked with (the page draws only its latest).</summary>
    private async void SendComboPrice(CampaignPlan plan, CampaignCombo combo, int? index, int n)
    {
        var svc = _app.Service;
        if (!_ready || svc == null || Web.CoreWebView2 == null) return;
        try
        {
            var roles = PlannerSwitch.RolesFor(_app.Settings, svc.Logs.Key);
            var price = await svc.Query(s => s.ComboPrice(plan, combo, index, roles));
            var json = await Task.Run(() => JsonSerializer.Serialize(new { kind = "comboPrice", n, price }, PageJson.Options));
            Web.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex) { AppSettings.LogError("combo price", ex); }
    }

    /// <summary>The Upgrades page's answer, which the engine builds (<see cref="UpgradePage.Build"/>) from this character's logs, their bank and what the log saw since it was entered.</summary>
    private async void SendUpgrade(JsonElement? request)
    {
        var svc = _app.Service;
        if (!_ready || svc == null || Web.CoreWebView2 == null) return;
        try
        {
            var logs = svc.Logs;
            var bank = _app.Settings.BankFor(logs.Key);
            var events = bank.EnteredAt is { } at ? await svc.Query(s => s.BankEventsAfter(at)) : [];
            var json = await Task.Run(() => JsonSerializer.Serialize(UpgradePage.Build(logs, bank, events, request), PageJson.Options));
            Web.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex) { AppSettings.LogError("upgrades page", ex); }
    }

    private static readonly JsonSerializerOptions ShareJson = new(PageJson.Options) { WriteIndented = true };

    /// <summary>The Ask page's answer to the question it posted: {q, from, to, goal, at, scope, diff}, any of them left out.</summary>
    private async void SendAsk(JsonElement m)
    {
        var svc = _app.Service;
        if (!_ready || svc == null || Web.CoreWebView2 == null) return;
        try
        {
            // Deserialize synchronously before the first await so every field the page sends reaches the engine directly.
            EqlPathfinder.Engine.Analysis.AskQuery? query;
            try { query = m.Deserialize<EqlPathfinder.Engine.Analysis.AskQuery>(PageJson.Query); }
            catch (JsonException) { query = null; }   // a question saved by an older page: start from where you are
            query ??= new(null, null, null, null, null, null, null);
            // The page asks again on every refresh: only a question that changed counts as asked.
            bool asked = query != _lastAsk;
            _lastAsk = query;
            var answer = await PostAsk(svc, query);
            // Usage counts: which question, and whether the shared numbers could answer it (the guild launch's measure).
            if (asked) UsageService.Metrics.Ask(answer.Q, EqlPathfinder.Engine.Usage.UsageMetrics.PoolAnswered(answer));
        }
        catch (Exception ex) { AppSettings.LogError("ask", ex); }
    }

    private EqlPathfinder.Engine.Analysis.AskQuery? _lastAsk;

    private FeedbackClient? _feedback;
    private FeedbackClient Feedback => _feedback ??= new FeedbackClient(_app.Settings, _app.AppVersion ?? "development");

    /// <summary>The page's own names (index.html): what a report may say it was sent from.</summary>
    internal static readonly string[] PageNames = ["home", "ask", "campaign", "classroles", "roster", "crawls", "loot", "relevels", "upgrade", "help", "settings"];

    /// <summary>The window's details as the last Report a problem opened: sent with that report (the page shows them first).</summary>
    private string? _reportDetails;

    /// <summary>
    /// Report a problem: a PNG of the page alone (never the desktop, the game or this window's title bar, which names the
    /// character), taken after the page has hidden names, clock times and folders, with the window's details.
    /// </summary>
    private async Task<object> ReportShotAsync()
    {
        var details = _reportDetails = ReportDetails();
        try
        {
            using var png = new MemoryStream();
            await Web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
            return new { kind = "reportShot", image = "data:image/png;base64," + Convert.ToBase64String(png.ToArray()), details };
        }
        catch (Exception ex)
        {
            AppSettings.LogError("report screenshot", ex);
            return new { kind = "reportShot", image = (string?)null, details };
        }
    }

    /// <summary>"1280×800 window · 125% scaling · Windows 10.0.26300 · WebView2 142.0.3595.53": what a layout bug depends on.</summary>
    private string ReportDetails()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var os = Environment.OSVersion.Version;
        string web;
        try { web = Web.CoreWebView2?.Environment.BrowserVersionString ?? "?"; }
        catch (Exception) { web = "?"; }
        return $"{Math.Round(Web.ActualWidth)}×{Math.Round(Web.ActualHeight)} window{(WindowState == WindowState.Maximized ? " (maximized)" : "")}" +
               $" · {Math.Round(dpi.DpiScaleX * 100)}% scaling · Windows {os.Major}.{os.Minor}.{os.Build} · WebView2 {web}";
    }

    /// <summary>The screenshot the page made (base64, after any marking up), or null when it isn't a PNG, JPEG or WebP in size.</summary>
    internal static byte[]? ShotBytes(string? base64)
    {
        if (string.IsNullOrEmpty(base64) || base64.Length > FeedbackClient.MaxShot / 3 * 4 + 8) return null;
        try { var b = Convert.FromBase64String(base64); return FeedbackClient.IsImage(b) ? b : null; }
        catch (FormatException) { return null; }
    }

    /// <summary>Posts a reply to the page once it's ready (a network call), logging rather than throwing.</summary>
    private async void PostAsync(Task<object> reply)
    {
        try
        {
            var json = JsonSerializer.Serialize(await reply, PageJson.Options);
            Web.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex) { AppSettings.LogError("help", ex); }
    }

    private (string Page, string? Focus)? _goWhenReady;

    /// <summary>The page has loaded (developer checks wait for it).</summary>
    internal bool Ready => _ready && Web.CoreWebView2 != null;

    /// <summary>Runs script on the page and returns its JSON result (developer checks).</summary>
    internal Task<string> RunScriptAsync(string js) => Web.CoreWebView2.ExecuteScriptAsync(js);

    /// <summary>A PNG of the page as it is (developer checks).</summary>
    internal async Task CaptureAsync(string path)
    {
        await using var f = File.Create(path);
        await Web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, f);
    }

    /// <summary>Opens a page (and focuses an element on it): the tray's "Send feedback…".</summary>
    internal async void Go(string page, string? focus = null)
    {
        if (!_ready || Web.CoreWebView2 == null) { _goWhenReady = (page, focus); return; }
        try
        {
            var script = $"show({JsonSerializer.Serialize(page)});" +
                         (focus != null ? $" document.getElementById({JsonSerializer.Serialize(focus)})?.focus();" : "");
            await Web.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex) { AppSettings.LogError("go to page", ex); }
    }

    private async Task<EqlPathfinder.Engine.Pages.AskAnswer> PostAsk(EqlPathfinder.Engine.Live.TrackerService svc, EqlPathfinder.Engine.Analysis.AskQuery query)
    {
        var answer = await svc.Query(s => s.AskPage(query));
        var json = await Task.Run(() => JsonSerializer.Serialize(answer, PageJson.Options));
        Web.CoreWebView2?.PostWebMessageAsJson(json);
        return answer;
    }

    /// <summary>Answers the last question again (new community numbers arrived). UI thread.</summary>
    internal async void RefreshAsk()
    {
        if (!_ready || _lastAsk is not { } query || _app.Service is not { } svc || Web.CoreWebView2 == null) return;
        try { await PostAsk(svc, query); }
        catch (Exception ex) { AppSettings.LogError("ask", ex); }
    }

    /// <summary>Sharing's status for the Settings card (on, counts, pending, codes, errors). UI thread.</summary>
    internal void SendShareStatus()
    {
        if (!_ready || Web.CoreWebView2 == null || _app.Sharing is not { } sharing) return;
        try { Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(sharing.Status(), PageJson.Options)); }
        catch (Exception ex) { AppSettings.LogError("share status", ex); }
    }

    /// <summary>The Sharing preview: exactly what the next full upload holds, built on request. Building it sends nothing.</summary>
    private async void SendSharePreview()
    {
        var svc = _app.Service;
        if (!_ready || svc == null || Web.CoreWebView2 == null) return;
        try
        {
            var salt = _app.ShareSalt();
            var version = _app.AppVersion ?? "development";
            var since = _app.Settings.ShareFrom;
            // Trios are checked against the spells cast: build once the spell file is in, or the trios would change later.
            await Task.Run(() => svc.WaitForGameData(TimeSpan.FromSeconds(60)));
            var payload = await svc.Query(s => s.SharePayload(version, salt, since));
            var json = await Task.Run(() => SharePreviewMessage(payload));
            Web.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex) { AppSettings.LogError("share preview", ex); }
    }

    /// <summary>The preview message for the page: counts, the size as sent (compact, then gzip-compressed), and the payload laid out to read.</summary>
    internal static string SharePreviewMessage(EqlPathfinder.Engine.Sharing.SharePayload p)
    {
        var compact = JsonSerializer.SerializeToUtf8Bytes(p, PageJson.Options);
        using var zipped = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(zipped, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(compact);
        return JsonSerializer.Serialize(new
        {
            kind = "sharePreview",
            since = p.Since,
            schema = p.Schema,
            crawls = p.Crawls.Count,
            relevels = p.Relevels.Count,
            stretches = p.Stretches.Count,
            reincarnations = p.Reincarnations.Count,
            kills = p.Crawls.Sum(c => c.Kills.Count) + p.Stretches.Sum(s => s.Kills.Count),
            bytes = compact.Length,
            compressed = zipped.Length,
            character = p.Character,
            text = JsonSerializer.Serialize(p, ShareJson),
        }, PageJson.Options);
    }

    public async void Refresh()
    {
        UpdateChrome();
        var svc = _app.Service;
        if (!_ready || svc == null || Web.CoreWebView2 == null) return;
        try
        {
            var snap = _app.LastSnapshot;
            // The settings are read here, on the UI thread, before the query.
            var settings = App.SettingsForPage(_app.Settings, overlaysLocked: _app.AllOverlaysLocked, arranging: _app.Arranging,
                version: _app.AppVersion, pendingUpdate: _app.PendingUpdate, updatesEnabled: _app.UpdatesEnabled,
                gameRunning: _app.GameRunning, onBetaBuild: _app.OnBetaBuild, campaignAnswered: _app.CampaignAnswered);
            var campaign = PlannerSwitch.PlanFor(_app.Settings, svc.Logs.Key);
            // Built on the tracker thread: the engine state isn't safe to read from here. The engine builds it
            // (PageData), so a test can build the very message a page is sent.
            var planner = PlannerSwitch.PageInput(_app.Settings, svc.Logs.Key);
            var payload = await svc.Query(s => PageData.Build(s, snap, campaign, settings, planner));
            var json = await Task.Run(() => JsonSerializer.Serialize(payload, PageJson.Options));
            Web.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex) { AppSettings.LogError("main window refresh", ex); }
    }
}
