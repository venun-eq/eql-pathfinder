using System.Globalization;
using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Tracking;
using EqlPathfinder.Engine.Upgrades;

namespace EqlPathfinder.App;

public partial class App
{
    /// <summary>The last Campaign edit <see cref="SetCampaign"/> finished with, by the page's number (<see cref="PageSettings.CampaignAnswered"/>).</summary>
    internal int CampaignAnswered { get; private set; }

    /// <summary>Saves your reincarnation totals for a character and, if it's being tracked or read, recounts.</summary>
    internal void SaveCounts(string character, Dictionary<string, EnteredCount> counts, TrackerService? reading = null)
    {
        var existing = _settings.EnteredCounts.Keys.FirstOrDefault(k => k.Equals(character, StringComparison.OrdinalIgnoreCase));
        if (existing != null) _settings.EnteredCounts.Remove(existing);
        if (counts.Count > 0) _settings.EnteredCounts[character] = new(counts, StringComparer.OrdinalIgnoreCase);
        _settings.Save();
        var svc = reading ?? _service;
        if (svc == null || !svc.Logs.Key.Equals(character, StringComparison.OrdinalIgnoreCase)) return;
        _options.EnteredCounts = new(counts, StringComparer.OrdinalIgnoreCase);
        svc.ApplyEnteredCounts().ContinueWith(_ => Dispatcher.BeginInvoke(() => _main?.Refresh()), TaskScheduler.Default);
    }

    // ------------------------------------------------------------------ tray actions

    /// <summary>A fragment value picked, or null for "Not sure: recommended".</summary>
    internal void SetFragmentValue(double? v)
    {
        _settings.FragmentValueRecommended = v == null;
        if (v != null) _settings.FragmentValue = v.Value;
        _options.FragmentValue = _settings.FragmentValueNow;
        _settings.Save();
        _main?.Refresh();
    }

    internal void SetFarmRate(double rate)
    {
        if (rate <= 0 || rate > 1000) return;
        // Typing a rate means you want that one rather than the one from your logs.
        _settings.FarmRate = rate;
        _settings.FarmRateMode = "manual";
        _options.FarmRateManual = rate;
        _options.FarmRateMode = "manual";
        _settings.Save();
        _main?.Refresh();   // the pages say where the rate comes from: now the one you set
    }

    internal void ToggleSound()
    {
        _settings.SoundOnHighTier = !_settings.SoundOnHighTier;
        _settings.Save();
    }

    // ------------------------------------------------------------------ main window pages

    /// <summary>
    /// Plan → Campaign changed: saves it for this character, building its loadouts again first when asked (a goal preset,
    /// "Rebuild the rest"), then refreshes the page. The refresh says the page's edit <paramref name="n"/> is answered,
    /// so the page stops waiting even when the plan saved is the one it had (a rebuild that changes nothing).
    /// </summary>
    internal async void SetCampaign(CampaignPlan plan, bool rebuild, int n = 0)
    {
        var svc = _service;
        if (svc == null) return;
        try
        {
            plan = plan.Normalized();
            var roles = PlannerSwitch.RolesFor(_settings, svc.Logs.Key);
            PlannerSwitch.WithLists(_settings, svc.Logs.Key, plan);   // the rebuild plans from the new lists
            if (rebuild) plan = await svc.Query(s => s.RebuildCampaign(plan, roles));
            else if (plan.Loadouts.Count > 0) plan.Since ??= DateTime.Now;
            PlannerSwitch.Store(_settings, svc.Logs.Key, plan);
            _settings.Save();
            UsageService.Metrics.Feature(rebuild ? "campaign-build" : "campaign-edit");
        }
        catch (Exception ex) { AppSettings.LogError("campaign", ex); }
        CampaignAnswered = Math.Max(CampaignAnswered, n);
        _main?.Refresh();
    }

    /// <summary>
    /// Class roles: the carries and the toggle are saved at once, then the plan is built again around them. The
    /// rebuilt plan is saved only if the roles and the stored plan are unchanged when it lands; otherwise the
    /// later edit's rebuild lands instead.
    /// </summary>
    internal async void SetRoles(CampaignRoles roles)
    {
        var svc = _service;
        if (svc == null) return;
        try
        {
            var key = svc.Logs.Key;
            PlannerSwitch.StoreRoles(_settings, key, roles);
            _settings.Save();
            _main?.Refresh();
            if (await PlannerSwitch.RebuildAround(_settings, key, (plan, saved) => svc.Query(s => s.RebuildCampaign(plan, saved)))) _settings.Save();
        }
        catch (Exception ex) { AppSettings.LogError("class roles", ex); }
        _main?.Refresh();
    }

    internal void SetGoal(string goal)
    {
        if (goal == "value") goal = "balanced";
        if (goal is not ("balanced" or "speed" or "motes" or "rare")) return;
        _settings.Goal = goal;
        _options.Goal = goal;
        _settings.Save();
        if (_overlay != null && LastSnapshot != null) _overlay.Update(LastSnapshot);
    }

    /// <summary>A switch or choice on the Settings page.</summary>
    internal void ApplySetting(string key, JsonElement v)
    {
        switch (key)
        {
            case "fragmentValue": SetFragmentValue(v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null); break;   // "recommended"
            case "beta": SetBeta(v.GetBoolean()); break;
            case "plannerCard" when v.GetString() == "seen":   // Got it on "Campaign has a new planner": every old plan goes, whatever the character
                PlannerSwitch.RemoveOldPlans(_settings); _settings.Save(); _main?.Refresh(); break;
            case "raceListSeen" when _service != null:   // the page left Campaign after the one-time race list line
                PlannerSwitch.RaceListEmptiedSeen(_settings, _service.Logs.Key); _settings.Save(); break;
            case "carries" when v.ValueKind == JsonValueKind.Object:
                SetRoles(new CampaignRoles(
                    v.TryGetProperty("carries", out var cs) && cs.ValueKind == JsonValueKind.Array ? cs.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [],
                    !v.TryGetProperty("oneStrongSlow", out var os) || os.ValueKind != JsonValueKind.False));
                break;
            case "sound": if (v.GetBoolean() != _settings.SoundOnHighTier) ToggleSound(); break;
            // Overlays: "overlay.<id>.visible|locked|opacity|<option>", and the ones for all of them.
            case "overlaysOnlyWithGame": if (v.GetBoolean() != _settings.OverlaysOnlyWithGame) ToggleOnlyWithGame(); break;
            case "overlaysLocked": SetAllOverlaysLocked(v.GetBoolean()); break;
            case "arrangeOverlays": SetArranging(v.GetBoolean()); break;
            case "shareIntroSeen": _settings.ShareIntroSeen = true; _settings.Save(); break;
            case not null when key.StartsWith("overlay.", StringComparison.Ordinal): ApplyOverlaySetting(key, v); break;
            // The welcome tour's own names for the crawl overlay's switches.
            case "overlayVisible": SetOverlayVisible(OverlayKinds.Crawl, v.GetBoolean()); break;
            case "overlaySize" when v.GetString() is "Compact" or "Standard" or "Detailed": SetOverlayOption(OverlayKinds.Crawl, "size", v.GetString()!); break;
            case "overlayMode" when v.GetString() is "Auto" or "Leveling" or "Farming": SetOverlayOption(OverlayKinds.Crawl, "mode", v.GetString()!); break;
            case "goal": SetGoal(v.GetString() ?? ""); _main?.Refresh(); break;
            case "farmDifficulties" when v.ValueKind == JsonValueKind.Array:
                _settings.FarmDifficulties = v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).Where(d => d is >= 0 and <= 4).Distinct().Order().ToArray();
                _options.FarmDifficulties = _settings.FarmDifficulties;
                _settings.Save();
                if (_overlay != null && LastSnapshot != null) _overlay.Update(LastSnapshot);
                _main?.Refresh();
                break;
            case "farmRateMode" when v.GetString() is "best" or "usual" or "manual":
                _settings.FarmRateMode = v.GetString()!;
                _options.FarmRateMode = _settings.FarmRateMode;
                _settings.Save();
                _main?.Refresh();
                break;
            case "startWindow" when v.GetString() is "Open" or "Minimized" or "Tray":
                _settings.StartWindow = v.GetString()!;
                _settings.Save();
                break;
            case "usage" when v.ValueKind is JsonValueKind.True or JsonValueKind.False: _usage?.SetEnabled(v.GetBoolean()); break;
        }
        _tray?.Refresh();
    }

    /// <summary>
    /// Sharing all play, chosen in the welcome tour or on Home's card. On a first run the sharing service doesn't exist yet,
    /// so the choice (and the invite code, while sharing is invite-only) waits in settings and starts with the app. A wrong
    /// or missing code leaves sharing off with the reason on Home's card and in Settings → Sharing.
    /// </summary>
    internal void ChooseSharing(string? code = null)
    {
        _settings.ShareIntroSeen = true;
        if (_settings.ShareEnabled) { _settings.SharePending = false; _settings.SharePendingCode = null; _settings.Save(); return; }
        UsageService.Metrics.Feature("share-on");
        _settings.SharePending = _sharing == null;
        _settings.SharePendingCode = _sharing == null ? code : null;
        _settings.Save();
        if (_sharing != null) _ = _sharing.TurnOn(everything: true, code);
    }

    /// <summary>"overlay.&lt;id&gt;.&lt;what&gt;" from the Settings page: visible, locked, opacity, or one of the overlay's own options.</summary>
    private void ApplyOverlaySetting(string key, JsonElement v)
    {
        var parts = key.Split('.');
        if (parts.Length != 3 || OverlayKinds.All.FirstOrDefault(k => k.Id == parts[1]) is not { } kind) return;
        switch (parts[2])
        {
            case "visible" when v.ValueKind is JsonValueKind.True or JsonValueKind.False: SetOverlayVisible(kind.Id, v.GetBoolean()); break;
            case "locked" when v.ValueKind is JsonValueKind.True or JsonValueKind.False: SetOverlayLocked(kind.Id, v.GetBoolean()); break;
            case "opacity" when v.ValueKind == JsonValueKind.Number: SetOverlayOpacity(kind.Id, v.GetDouble()); break;
            case "opacity" when v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var o):
                SetOverlayOpacity(kind.Id, o); break;
            // Only options the overlay has, with the values it knows.
            case var option when kind.Options.ContainsKey(option) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } value
                && (kind.Id, option, value) is (OverlayKinds.Crawl, "size", "Compact" or "Standard" or "Detailed")
                    or (OverlayKinds.Crawl, "mode", "Auto" or "Leveling" or "Farming") or (OverlayKinds.Meter, not "fightEnd", _) or (OverlayKinds.Meter, "fightEnd", "3" or "5" or "8" or "10" or "15" or "30"):
                SetOverlayOption(kind.Id, option, value);
                break;
        }
    }

    internal void RunAction(string action)
    {
        if (action is "checkUpdates" or "changeCharacter" or "openData") UsageService.Metrics.Feature(action);
        switch (action)
        {
            case "checkUpdates": CheckForUpdates(); break;
            case "restartUpdate": RestartToUpdate(); break;
            case "changeCharacter": ChangeCharacter(); break;
            case "openData": OpenDataFolder(); break;
            case "tour": ShowTour(); break;
        }
    }

    /// <summary>
    /// Joining the beta shows beta features (Sharing today) and adds the beta update channel; this build looks for one
    /// right away. Leaving keeps this build until a regular release passes it, and never stops sharing that's already on:
    /// its card stays so it can be paused or deleted.
    /// </summary>
    private void SetBeta(bool on)
    {
        if (_settings.Beta == on) return;
        _settings.Beta = on;
        _settings.Save();
        if (_updater != null)
        {
            _updater.Beta = on;
            if (on) _ = _updater.CheckAsync();
        }
        _ = _community?.RefreshAsync();   // on with the beta; off without it, unless sharing is on
        _main?.Refresh();
    }

    /// <summary>
    /// From the Upgrades page: the motes, fragments and VTPs you have now, as the game shows them ({motes: [11], fragments: [11],
    /// vtps}). Stamped with the time, so loot and condensing after it are added on top.
    /// </summary>
    internal void SetBank(JsonElement v)
    {
        if (_service?.Logs.Key is not { } key) return;
        static int[] Tiers(JsonElement v, string name)
        {
            var a = new int[11];
            if (v.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var x in arr.EnumerateArray())
                {
                    if (i >= 1 && i <= 10 && x.TryGetInt32(out var n)) a[i] = Math.Max(0, n);
                    i++;
                }
            }
            return a;
        }
        foreach (var k in _settings.Banks.Keys.Where(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList()) _settings.Banks.Remove(k);
        _settings.Banks[key] = new MoteBank
        {
            Motes = Tiers(v, "motes"),
            Fragments = Tiers(v, "fragments"),
            Vtps = v.TryGetProperty("vtps", out var t) && t.TryGetInt32(out var n) ? Math.Max(0, n) : 0,
            EnteredAt = DateTime.Now,
        };
        _settings.Save();
    }

    /// <summary>From the Roster page: {"class:WAR": 2, ...} sets those totals; reset goes back to the log alone.</summary>
    internal void SetCountsFromPage(JsonElement? changes)
    {
        if (_service?.Logs.Key is not { } key) return;
        var counts = _settings.CountsFor(key);
        if (changes is not { } ch) counts.Clear();
        else
            foreach (var p in ch.EnumerateObject())
            {
                // Typing back exactly what the log shows means "the log is right": no entry needed.
                if (p.Value.ValueKind == JsonValueKind.Null) counts.Remove(p.Name);
                else counts[p.Name] = new EnteredCount(Math.Clamp(p.Value.GetInt32(), 0, 99), DateTime.Now);
            }
        SaveCounts(key, counts);
    }

    /// <summary>
    /// The settings as a page reads them (data.settings): single mapping from <see cref="AppSettings"/> for both the window
    /// and the --snapshot preview. Non-saved settings are required parameters so callers cannot omit them. Read on
    /// the UI thread.
    /// </summary>
    internal static PageSettings SettingsForPage(AppSettings s, bool overlaysLocked, bool arranging, string? version, string? pendingUpdate,
        bool updatesEnabled, bool gameRunning, bool onBetaBuild, int campaignAnswered) => new()
        {
            FragmentValue = s.FragmentValueNow,
            FragmentRecommended = s.FragmentValueRecommended,
            Sound = s.SoundOnHighTier,
            Overlays = OverlayKinds.All.Select(k =>
            {
                var p = s.Overlay(k.Id);
                return new PageOverlay { Id = k.Id, Name = k.Name, About = k.About, Visible = p.Visible, Locked = p.Locked, Opacity = p.Opacity, Options = p.Options };
            }).ToList(),
            OverlaysOnlyWithGame = s.OverlaysOnlyWithGame,
            OverlaysLocked = overlaysLocked,
            Arranging = arranging,
            ShareIntroSeen = s.ShareIntroSeen,
            FarmRate = s.FarmRate,
            FarmRateMode = s.FarmRateMode,
            Goal = s.Goal,
            FarmDifficulties = s.FarmDifficulties,
            StartWindow = s.StartWindow,
            Version = version,
            PendingUpdate = pendingUpdate,
            UpdatesEnabled = updatesEnabled,
            LogsDir = s.LogsDir,
            Character = s.Character,
            GameRunning = gameRunning,
            Beta = s.Beta,
            OnBetaBuild = onBetaBuild,
            ShareEnabled = s.ShareEnabled,
            Usage = s.UsageEnabled,
            NoticesSeen = s.NoticesSeen,
            CampaignAnswered = campaignAnswered,
        };

    /// <summary>A one-time notice on Home was answered (any of its buttons): it never shows again.</summary>
    internal void NoticeSeen(string id, string action)
    {
        UsageService.Metrics.Feature("notice-" + id + "-" + action);
        if (_settings.NoticesSeen.Contains(id)) return;
        _settings.NoticesSeen.Add(id);
        _settings.Save();
    }

    /// <summary>The player's sharing salt, made and saved the first time it's needed. UI thread.</summary>
    internal byte[] ShareSalt()
    {
        if (_settings.ShareSalt is { Length: > 0 } s)
            try { return Convert.FromBase64String(s); } catch (FormatException) { }
        var salt = ShareBuilder.NewSalt();
        _settings.ShareSalt = Convert.ToBase64String(salt);
        _settings.Save();
        return salt;
    }
}
