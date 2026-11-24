using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EqlPathfinder.App;

/// <summary>A window's restored position in screen pixels, and whether it was maximized.</summary>
public sealed class WindowPlace
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public bool Maximized { get; set; }
    /// <summary>Where the window actually was on screen (screen pixels). Differs from the above when Windows Snap placed it.</summary>
    public int? ScreenLeft { get; set; }
    public int? ScreenTop { get; set; }
    public int? ScreenRight { get; set; }
    public int? ScreenBottom { get; set; }
}

/// <summary>Per-user settings in %LocalAppData%\EQL Pathfinder\settings.json (kept across updates).</summary>
public sealed class AppSettings : EqlPathfinder.Engine.Analysis.IPlannerSettings, EqlPathfinder.Engine.Usage.IUsageSettings
{
    /// <summary>Developer testing: EQLPATHFINDER_DATA points a copy at its own settings and database, beside the installed one.</summary>
    public static string? IsolatedDataDir { get; } = Environment.GetEnvironmentVariable("EQLPATHFINDER_DATA") is { Length: > 0 } d ? d : null;

    public static string DataDir { get; } = IsolatedDataDir ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EQL Pathfinder");

    private static string FilePath => Path.Combine(DataDir, "settings.json");
    public static string DatabasePath => Path.Combine(DataDir, "pathfinder.db");
    public static string ErrorLogPath => Path.Combine(DataDir, "error.log");

    public string? LogsDir { get; set; }
    /// <summary>Name_server of the tracked character.</summary>
    public string? Character { get; set; }
    /// <summary>
    /// The fragment value you picked, by how high you take the gear you feed with motes
    /// (<see cref="EqlPathfinder.Engine.Model.Potential.FragmentValueChoices"/>): +10 is 4.5, +9 3.56, +6 or lower 2.0, its spell
    /// value. Used only when <see cref="FragmentValueRecommended"/> is off; read <see cref="FragmentValueNow"/>.
    /// </summary>
    public double FragmentValue { get; set; } = EqlPathfinder.Engine.Model.Potential.RecommendedFragmentValue;
    /// <summary>"Not sure: recommended", the default: follow the app's recommendation, so it moves if the rules do.</summary>
    public bool FragmentValueRecommended { get; set; } = true;
    /// <summary>What a fragment counts as now: the recommendation, or the value you picked.</summary>
    [JsonIgnore]
    public double FragmentValueNow => FragmentValueRecommended ? EqlPathfinder.Engine.Model.Potential.RecommendedFragmentValue : FragmentValue;
    /// <summary>
    /// Settings migration revision. 1: default fragment value 1.6 → 2.0. 2: per-overlay settings map.
    /// 3: default 2.0 → "recommended" once fragments became condensable.
    /// </summary>
    public int Revision { get; set; }
    public bool SoundOnHighTier { get; set; } = true;

    /// <summary>Every overlay's place and look, by id (<see cref="OverlayKinds"/>). Read and create them with <see cref="Overlay"/>.</summary>
    public Dictionary<string, OverlayPrefs> Overlays { get; set; } = new();
    /// <summary>Show the overlays only while EverQuest Legends (eqgame.exe) is running.</summary>
    public bool OverlaysOnlyWithGame { get; set; } = true;

    /// <summary>One overlay's settings, made with its defaults the first time.</summary>
    public OverlayPrefs Overlay(string id) => Overlays.TryGetValue(id, out var p) ? p : Overlays[id] = OverlayKinds.Defaults(id);

    // Before revision 2 each overlay had its own fields; read once to move them into Overlays, never written again.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? OverlayLeft { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? OverlayTop { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? OverlayOpacity { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? OverlayClickThrough { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? OverlayVisible { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? OverlayOnlyWithGame { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OverlaySize { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OverlayMode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? MeterVisible { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? MeterLeft { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? MeterTop { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? MeterClickThrough { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MeterScope { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MeterBy { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MeterMode { get; set; }
    /// <summary>The farm rate you typed (T5-eq/h), used in manual mode and until the logs have enough farming.</summary>
    public double FarmRate { get; set; } = Engine.Tracking.TrackerOptions.DefaultFarmRate;
    /// <summary>best (your best farming zone since the 11-10 patch), usual (median of that farming) or manual.</summary>
    public string FarmRateMode { get; set; } = "best";
    /// <summary>What you optimize for: balanced, speed, motes or rare.</summary>
    public string Goal { get; set; } = "balanced";
    /// <summary>The difficulties you farm for motes (Settings → How you play). Visits there are judged by T5-eq an hour, the rest as leveling; none is allowed.</summary>
    public int[] FarmDifficulties { get; set; } = [3, 4];

    /// <summary>What Pathfinder shows when it starts: Open (the main window), Minimized (on the taskbar) or Tray (only the tray icon).</summary>
    public string StartWindow { get; set; } = "Open";
    /// <summary>Where the main window was last (Windows' own placement: monitor, size, maximized).</summary>
    public WindowPlace? MainWindowPlace { get; set; }

    /// <summary>Your next-trio priorities from the Plan page (null = defaults).</summary>
    public EqlPathfinder.Engine.Analysis.PlanPrefs? Plan { get; set; }
    /// <summary>How the Route page ranks zones: value (cost per level), speed (levels/h) or motes (T5-eq/h).</summary>
    public string RouteMetric { get; set; } = "value";

    /// <summary>Overrides the release feed this build came with (a folder or https address). Empty = the built-in one.</summary>
    public string UpdateFeedUrl { get; set; } = "";

    /// <summary>
    /// Reincarnation counts you entered, per character (Name_server), keyed "class:WAR" / "race:Ogre". Kept here rather than in
    /// the history database, so re-reading the logs never loses them.
    /// </summary>
    public Dictionary<string, Dictionary<string, EqlPathfinder.Engine.Tracking.EnteredCount>> EnteredCounts { get; set; } = new();

    /// <summary>The welcome tour version this copy has shown (0 = none yet).</summary>
    public int WelcomeSeen { get; set; }

    /// <summary>
    /// Random salt (base64) for the codes that stand in for character names and rows when sharing. Made the first time
    /// the Sharing preview is built; linking a second PC (phase 1) will copy it there so both give the same codes.
    /// </summary>
    public string? ShareSalt { get; set; }

    /// <summary>In the beta: beta releases arrive as well as regular ones, so new features come a few days early.</summary>
    public bool Beta { get; set; }

    /// <summary>Community sharing is on (off until the player turns it on in Settings).</summary>
    public bool ShareEnabled { get; set; }
    /// <summary>Sharing was chosen (in the welcome tour) before it could start: it starts with the app, sharing all play.</summary>
    public bool SharePending { get; set; }
    /// <summary>The invite code typed in the welcome tour with <see cref="SharePending"/> (sharing is invite-only), used once.</summary>
    public string? SharePendingCode { get; set; }
    /// <summary>Sharing and the beta were offered (the tour, or the Home card): the Home card doesn't ask again.</summary>
    public bool ShareIntroSeen { get; set; }
    /// <summary>An anonymous random id (32 hex) for roadmap votes and feedback: one vote per copy, nothing about the player.</summary>
    public string? VoterId { get; set; }
    /// <summary>One-time notices on Home (web/js/notices.js) this copy has answered: they never show again.</summary>
    public List<string> NoticesSeen { get; set; } = new();
    /// <summary>Anonymous usage counts may be counted and sent (docs/usage.md). Off until the player says yes in the tour on a new copy
    /// (<see cref="NewCopy"/>); true by default only for a file written before 11-17, which has no switch and was on.</summary>
    public bool UsageEnabled { get; set; } = true;
    /// <summary>The player has answered the tour's Usage step (docs/specs/usage-choice.md): false on a new copy until they do, null (missing)
    /// in a file from before the question, which counts as answered. Never migrated: an older build drops the field.</summary>
    public bool? UsageChosen { get; set; }
    /// <summary>This copy's random usage id (32 hex): not the sharing key's id and not <see cref="VoterId"/>.</summary>
    public string? UsageId { get; set; }
    /// <summary>The day this copy started counting (yyyy-mm-dd).</summary>
    public string? UsageSince { get; set; }
    /// <summary>Ids whose rows the server must still delete (docs/specs/usage-choice.md): a No moves the id here, the server's answer clears it.</summary>
    public string[] UsageForget { get; set; } = [];
    /// <summary>Uploads are paused; what was shared stays on the server.</summary>
    public bool SharePaused { get; set; }
    /// <summary>Share only play from this moment on (null = everything in the logs).</summary>
    public DateTime? ShareFrom { get; set; }
    /// <summary>This install's private key (PKCS#8), encrypted for this Windows user (DPAPI), base64.</summary>
    public string? ShareKey { get; set; }
    public string? ShareInstall { get; set; }
    public string? SharePlayer { get; set; }
    /// <summary>The data folder the sharing key was made in. An isolated copy only shares with a key it made itself.</summary>
    public string? ShareHome { get; set; }

    /// <summary>
    /// An isolated copy (EQLPATHFINDER_DATA) whose settings.json was copied from a real install starts with
    /// sharing off and no identity. DPAPI keys are scoped to the Windows user rather than the folder, so without
    /// this guard an isolated copy with an empty ledger would re-upload as the real install and burn its daily limit.
    /// The real install's data is never touched.
    /// </summary>
    internal void ForgetCopiedSharing()
    {
        if (IsolatedDataDir == null || (ShareKey == null && ShareInstall == null)) return;
        if (EqlPathfinder.Engine.Sharing.ShareKeyHome.MadeIn(ShareHome, DataDir)) return;
        (ShareKey, ShareInstall, SharePlayer, ShareSalt, ShareHome) = (null, null, null, null, null);
        (ShareEnabled, SharePaused, SharePending, SharePendingCode, ShareFrom) = (false, false, false, null, null);
    }
    /// <summary>Overrides the community API address (developer testing). Empty = the built-in one.</summary>
    public string ShareApi { get; set; } = "";

    /// <summary>Motes, fragments and VTPs you entered on the Upgrades page, per character (Name_server).</summary>
    public Dictionary<string, EqlPathfinder.Engine.Upgrades.MoteBank> Banks { get; set; } = new();

    /// <summary>The old planner's plan for each character (Name_server). Never shown now: counted by the card "Campaign has a new planner", and removed by its Got it.</summary>
    public Dictionary<string, EqlPathfinder.Engine.Analysis.CampaignPlan> Campaigns { get; set; } = new();

    /// <summary>
    /// Each character's plan (Name_server). Named for the new planner it was made in; a plan made in the beta is the player's plan, and a null
    /// plan is "no plan yet". Campaigns above holds the old planner's plans, which are never read: the card's Got it removes them.
    /// </summary>
    public Dictionary<string, EqlPathfinder.Engine.Analysis.NewCampaignPlan> NewCampaigns { get; set; } = new();

    public EqlPathfinder.Engine.Upgrades.MoteBank BankFor(string? character) =>
        Banks.FirstOrDefault(kv => kv.Key.Equals(character, StringComparison.OrdinalIgnoreCase)).Value ?? new();

    /// <summary>Entered counts for one character, as a case-insensitive copy.</summary>
    public Dictionary<string, EqlPathfinder.Engine.Tracking.EnteredCount> CountsFor(string? character)
    {
        var all = EnteredCounts.FirstOrDefault(kv => kv.Key.Equals(character, StringComparison.OrdinalIgnoreCase)).Value;
        return all != null ? new(all, StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? NewCopy()).Migrated();
                s.ForgetCopiedSharing();
                return s;
            }
        }
        catch (Exception ex) { LogError("settings.json unreadable", ex); }
        return NewCopy();
    }

    private const int CurrentRevision = 3;

    /// <summary>A new copy: not asked yet, so nothing is counted or sent until the tour's Usage step is answered. Used for no file, an unreadable file and a file that reads null.</summary>
    internal static AppSettings NewCopy() => new() { Revision = CurrentRevision, UsageEnabled = false, UsageChosen = false };

    private AppSettings Migrated()
    {
        // Rev 1: migrate legacy 1.6 fragment default to 2.0 (spell value).
        if (Revision < 1 && Math.Abs(FragmentValue - 1.6) < 0.001) FragmentValue = 2.0;
        // Rev 3: migrate the old 2.0 default to "recommended"; explicit custom values stay untouched.
        if (Revision < 3) FragmentValueRecommended = Math.Abs(FragmentValue - 2.0) < 0.001;
        // Rev 2: migrate legacy per-overlay fields into the Overlays map.
        if (Revision < 2)
        {
            var crawl = Overlay(OverlayKinds.Crawl);
            crawl.Visible = OverlayVisible ?? crawl.Visible;
            crawl.Left = OverlayLeft ?? crawl.Left;
            crawl.Top = OverlayTop ?? crawl.Top;
            crawl.Locked = OverlayClickThrough ?? crawl.Locked;
            crawl.Opacity = OverlayOpacity ?? crawl.Opacity;
            if (OverlaySize != null) crawl.Options["size"] = OverlaySize;
            if (OverlayMode != null) crawl.Options["mode"] = OverlayMode;
            var meter = Overlay(OverlayKinds.Meter);
            meter.Visible = MeterVisible ?? meter.Visible;
            meter.Left = MeterLeft ?? meter.Left;
            meter.Top = MeterTop ?? meter.Top;
            meter.Locked = MeterClickThrough ?? meter.Locked;
            meter.Opacity = OverlayOpacity ?? meter.Opacity;   // the meter used the overlay's background
            if (MeterScope != null) meter.Options["scope"] = MeterScope;
            if (MeterBy != null) meter.Options["by"] = MeterBy;
            if (MeterMode != null) meter.Options["mode"] = MeterMode;
            OverlaysOnlyWithGame = OverlayOnlyWithGame ?? OverlaysOnlyWithGame;
            (OverlayLeft, OverlayTop, OverlayOpacity, OverlayClickThrough, OverlayVisible, OverlayOnlyWithGame, OverlaySize, OverlayMode) =
                (null, null, null, null, null, null, null, null);
            (MeterVisible, MeterLeft, MeterTop, MeterClickThrough, MeterScope, MeterBy, MeterMode) = (null, null, null, null, null, null, null);
        }
        Revision = CurrentRevision;
        return this;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex) { LogError("saving settings", ex); }
    }

    /// <summary>Writes the error to the local error log, and counts it (scrubbed: what, type, where; docs/usage.md).</summary>
    public static void LogError(string what, Exception ex)
    {
        try { EqlPathfinder.Engine.Usage.UsageMetrics.Default.Error(what, ex); }
        catch { /* counting must never be the second error */ }
        try
        {
            Directory.CreateDirectory(DataDir);
            File.AppendAllText(ErrorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {what}: {ex}{Environment.NewLine}");
        }
        catch { /* nothing left to do */ }
    }
}
