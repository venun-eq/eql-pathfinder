using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;
using EqlPathfinder.Engine.Upgrades;

namespace EqlPathfinder.Engine.Live;

public sealed partial class TrackerService
{
    private List<Journey>? _journeys;
    private string? _journeysKey;
    private DateTime _journeysBuilt;
    private (string? Key, List<LevelingSegment>? Segments, int LeftOut, int OpenLeftOut, int BonusLeftOut) _askCache;
    private (string? Key, PlayFacts? Facts) _facts;
    private (PlayFacts? Facts, TrioTimeline? Timeline) _timeline;

    /// <summary>The game's spell → classes table, once the background read has finished (null until then, or without the file).</summary>
    private IReadOnlyDictionary<string, int>? SpellClasses =>
        _spellClasses.IsCompletedSuccessfully && _spellClasses.Result.Count > 0 ? _spellClasses.Result : null;

    /// <summary>
    /// Journeys, recomputed when a crawl closes, the level, loadout or crawl changes (a ding outside an
    /// instance counts), and every 30 s while the current crawl's hours move.
    /// </summary>
    public List<Journey> Journeys()
    {
        int n;
        lock (_historyLock) n = _history.Count;
        var s = _engine.State;
        var key = $"{n}|{s.Level}|{s.Trio}|{s.Loadout}|{s.Stints.Count}|{s.Roster.Classes.Values.Sum(i => i.Rank)}|{s.Race}|{s.Current?.Id}|{SpellClasses != null}|{SettingsKey()}";
        if (_journeys == null || _journeysKey != key || (DateTime.Now - _journeysBuilt).TotalSeconds >= 30)
        {
            List<KillRecord> field;
            lock (_historyLock) field = _fieldKills.ToList();
            _journeys = Analysis.Journeys.Build(Facts(), s, _options, FieldHunts.From(field, _options));
            _journeysKey = key;
            _journeysBuilt = DateTime.Now;
        }
        return _journeys;
    }

    public List<VisitStats> History(int limit)
    {
        List<Visit> visits;
        lock (_historyLock) visits = _history.TakeLast(limit).ToList();
        var now = Now;
        var trios = Trios();
        var judged = Judged();
        var rows = visits.AsEnumerable().Reverse().Select(v =>
        {
            var st = VisitStats.From(v, now, _options);
            if (trios.TryGetValue(v.Id, out var c)) st = st with { Trio = c.Trio, Race = c.Race, TrioSource = c.Source };
            st = Verdicts.Apply(st, judged.GetValueOrDefault(v.Id));
            return st;
        }).ToList();
        Verdicts.AssignStops(rows);
        Stretches.Assign(rows, Journeys(), CapNow);   // each row's stretch and its stop's kill pace, for the pages to draw
        return rows;
    }

    public List<HighTierDrop> HighTierHistory(int limit)
    {
        lock (_storeLock) return _store.HighTierDrops(_logs.Key, limit);
    }

    /// <summary>Every fragment and every drop above T5, newest first.</summary>
    public List<HighTierDrop> NotableDrops(int limit)
    {
        lock (_storeLock) return _store.NotableDrops(_logs.Key, limit);
    }

    /// <summary>Every mote and fragment pickup, newest first, in a compact shape for the Loot page's charts and filters.</summary>
    public object AllLootCompact()
    {
        List<PathfinderStore.LootRow> rows;
        lock (_storeLock) rows = _store.AllLoot(_logs.Key);
        return rows.Select(x => new
        {
            t = x.Time,
            k = (int)x.Kind,
            r = (int)x.Tier,
            n = x.Count,
            c = x.Chest ? 1 : 0,
            s = x.Source,
            p = x.Place,
            i = x.InInstance ? 1 : 0,
        }).ToList();
    }

    /// <summary>Pickups and condense offers after a moment, for the mote bank on the Upgrades page.</summary>
    public List<BankEvent> BankEventsAfter(DateTime after)
    {
        lock (_storeLock) return _store.BankEventsAfter(_logs.Key, after);
    }

    /// <summary>Motes and fragments looted per tier since the logs began, for the Loot page.</summary>
    public object LootLedger()
    {
        List<PathfinderStore.LootTotal> totals;
        DateTime? since;
        lock (_storeLock) (totals, since) = _store.LootTotals(_logs.Key);
        var tiers = Enum.GetValues<MoteTier>().Where(t => (int)t >= 1)
            .Select(t => new
            {
                tier = (int)t,
                name = t.ToString(),
                t5Value = MoteTiers.T5Value(t),
                motes = totals.Where(x => x.Tier == t && x.Kind == LootKind.Mote).Sum(x => x.Count),
                fragments = totals.Where(x => x.Tier == t && x.Kind == LootKind.Fragment).Sum(x => x.Count),
                chestMotes = totals.Where(x => x.Tier == t && x.Kind == LootKind.Mote).Sum(x => x.FromChests),
                chestFragments = totals.Where(x => x.Tier == t && x.Kind == LootKind.Fragment).Sum(x => x.FromChests),
            })
            .Where(x => x.motes + x.fragments > 0).ToList();
        return new { since, tiers };
    }

    /// <summary>Re-applies the entered counts in Options.EnteredCounts (after you change them). Runs on the tracker thread.</summary>
    public Task ApplyEnteredCounts() => Query(s =>
    {
        s._engine.State.Roster.Apply(s._options.EnteredCounts);
        s._journeys = null;
        s._nextCache = default;
        s._revision++;
        s._dirty = true;
        s._publishNow = true;
        return true;
    });

    /// <summary>Each class and race: what the log saw, what you entered, and the count Pathfinder uses.</summary>
    public object RosterCounts()
    {
        var r = _engine.State.Roster;
        var entered = _options.EnteredCounts;
        object Row(bool race, string key, string name)
        {
            var i = race ? r.Races.GetValueOrDefault(key) : r.Classes.GetValueOrDefault(key);
            var e = entered.GetValueOrDefault(Roster.Key(race, key));
            return new
            {
                key,
                name,
                logged = i?.Seen.Count ?? 0,
                rank = i?.Rank ?? e?.Count ?? 0,
                entered = e?.Count,
                enteredAt = e?.AsOf,
                lastSeen = i?.Seen.Count > 0 ? i.Seen[^1] : (DateTime?)null,
            };
        }
        return new
        {
            classes = ClassNames.All.Select(c => Row(false, c.Abbrev, c.Name)).ToList(),
            races = ClassNames.Races.Select(x => Row(true, x, x)).ToList(),
            logStart = _engine.State.FirstTime,
            logEnd = _engine.State.LastTime,
        };
    }

    /// <summary>Everything the Reincarnations and Route tabs need, and Plan → Campaign for <paramref name="campaign"/> (yours, from settings).</summary>
    /// <param name="history">History as it is sent (<see cref="History"/>), so the stretches and kill paces here are over the rows the page has.</param>
    public object ReincarnationData(CampaignPlan? campaign = null, IReadOnlyList<VisitStats>? history = null, PlannerInput? planner = null)
    {
        var s = _engine.State;
        var o = _options;
        var farm = FarmInfo();
        double farmRate = RouteAnalysis.EffectiveFarmRate(farm, o);
        // Precompute kill pace across relevel crawls and History stretches so pages render without math.
        var rows = (history ?? History(5000)).ToList();
        var built = Journeys();
        var stretches = Stretches.Describe(rows, built, CapNow);   // the ids History already put on the rows
        var byStart = built.OrderBy(j => j.Start).ToList();
        var journeys = built.Select(j => j with { KillPace = stretches.FirstOrDefault(st => st.Relevel == byStart.FindIndex(x => ReferenceEquals(x, j)))?.KillPace }).ToList();
        var classes = BuildClassRosterRows(s.Roster, o.EnteredCounts);
        var races = BuildRaceRosterRows(s.Roster, o.EnteredCounts, journeys);
        var era = RouteAnalysis.EraStart(s);

        // Home's answer for where you are, from Ask (one model for every "where" question): the route to the cap while
        // leveling, the best farm at the cap.
        int lvl = s.Level ?? 1;
        var (kindOf, kindSource) = DifficultyMix.HomeKind(journeys, s.Trio);
        var homeKind = kindOf == null ? null : new { source = kindSource, trio = kindOf.Trio, trioKey = kindOf.TrioKey, mostly = kindOf.Mostly, farmed = kindOf.Farmed };
        var home = AskPage(lvl < CapNow
            ? new AskQuery("level", lvl, CapNow, kindOf?.Farmed == true ? "motes" : "fast", null, null, null)
            : new AskQuery("farm", null, null, null, CapNow, "patch", null));

        // Precompute race curves for the open relevel, Home's comparison kind, and the campaign's next run.
        planner ??= new(new CampaignRoles(ClassRoles.DefaultCarries, true), false);
        var campaignView = CampaignData(campaign ?? new CampaignPlan(), journeys, planner.Roles) with
        {
            NewPlanner = NewPlannerView.Of(planner, SurvivalCheck.Of(journeys, o.FarmDifficulties), o.FarmDifficulties.Order().ToList()),
        };
        var homeNow = HomeState.Of(s.Level, s.Trio, s.Race, CapNow, s.Roster, journeys, s.Loadout);
        int curRank = (s.Trio?.Split('/') ?? []).Select(c => s.Roster.Classes.GetValueOrDefault(c)?.Rank ?? 0).DefaultIfEmpty(0).Max();
        var journeyRaces = JourneyRace.For(journeys, CapNow, homeNow.Now?.JourneyIndex, kindOf?.Mostly, curRank, campaignView.Runs.FirstOrDefault(x => x.State == "next")?.Xp,
            home.Route.Legs.Count > 0 ? home.Route.Minutes : null, home.Route.Legs.Count > 0 ? home.Route.T5 : null, home.Route.Legs.Select(l => l.Minutes).ToList());
        return new
        {
            levelCap = CapNow,
            newLevels = NewLevels(),
            xpByRank = Planner.XpByRank,
            farmRate,
            farm,
            farmRateMode = o.FarmRateMode,
            farmRateManual = o.FarmRateManual,
            farmSource = RouteAnalysis.FarmSource(farm, o),   // where the rate comes from: every page that uses it says so
            completionist = Passives.GetValueOrDefault("Completionist")?.Description,
            stretches,
            usualRuns = RunPace.MinRuns,
            classes,
            races,
            journeys,
            home,
            homeKind,
            homeNow,
            currentTrio = s.Trio,
            currentLevel = s.Level,
            era,
            logStart = s.FirstTime,
            paces = RelevelPaceData(journeys),
            campaign = campaignView,
            journeyRaces,
        };
    }

    /// <summary>
    /// The levels the expansion in force raised the cap by, while pages have no numbers for them: its name, day, the old cap and the new, and the
    /// trio's reincarnation count at the first new level (its XP to level there). Null before the expansion's day and for the first one.
    /// </summary>
    private object? NewLevels()
    {
        var list = _options.Expansions;
        int at = -1;
        for (int i = 0; i < list.Count; i++) if (list[i].From <= Now) at = i;
        if (at < 1 || list[at].LevelCap <= list[at - 1].LevelCap) return null;
        var from = list[at - 1].LevelCap;
        var rank = _engine.State.Roster.TrioRankAt(_engine.State.Trio?.Split('/'), from + 1, _options.BaseCap);
        return new { expansion = list[at].Name, day = list[at].From.ToString("yyyy-MM-dd"), from, to = list[at].LevelCap, rank };
    }

    private List<object> BuildClassRosterRows(Roster roster, IReadOnlyDictionary<string, EnteredCount> enteredCounts) =>
        ClassNames.All.Select(c =>
        {
            var i = roster.Classes.GetValueOrDefault(c.Abbrev);
            var p = Passives.GetValueOrDefault(c.Abbrev);
            var e = enteredCounts.GetValueOrDefault(Roster.Key(false, c.Abbrev));
            return (object)new
            {
                key = c.Abbrev,
                name = c.Name,
                rank = i?.Rank ?? 0,
                logged = i?.Seen.Count ?? 0,
                entered = e?.Count,
                enteredAt = e?.AsOf,
                level = i?.Level,
                reincarnated = i?.Reincarnated,
                reachedCap = i?.ReachedCap,
                lastActive = i?.LastActive,
                traits = c.Traits.ToString(),
                note = c.Note,
                carry = ClassNames.Carries.GetValueOrDefault(c.Abbrev),
                healer = ClassNames.IsHealer(c.Abbrev),
                passive = p?.Description,
                ability = p?.Ability,
            };
        }).ToList();

    private List<object> BuildRaceRosterRows(Roster roster, IReadOnlyDictionary<string, EnteredCount> enteredCounts, IReadOnlyList<Journey> journeys)
    {
        // A race's level as journeys saw it (its /who may have shown an illusion instead).
        int RaceLevel(string race, Incarnation? i) =>
            Math.Max(i?.Level ?? 0, journeys.Where(j => j.Race == race).Select(j => j.LevelNow).DefaultIfEmpty(0).Max());

        return ClassNames.Races.Select(r =>
        {
            var i = roster.Races.GetValueOrDefault(r);
            var p = Passives.GetValueOrDefault(r);
            var e = enteredCounts.GetValueOrDefault(Roster.Key(true, r));
            int lv = RaceLevel(r, i);
            return (object)new
            {
                key = r,
                name = r,
                rank = i?.Rank ?? 0,
                logged = i?.Seen.Count ?? 0,
                entered = e?.Count,
                enteredAt = e?.AsOf,
                level = lv > 0 ? lv : (int?)null,
                reincarnated = i?.Reincarnated,
                passive = p?.Description,
            };
        }).ToList();
    }

    /// <summary>
    /// What sharing sends for this character: all play in the logs, or only from <paramref name="since"/> on. The Settings
    /// preview and the uploads both use it. Runs on the tracker thread.
    /// </summary>
    public SharePayload SharePayload(string appVersion, byte[] salt, DateTime? since = null)
    {
        var journeys = Journeys();
        var from = since ?? DateTime.MinValue;
        // Rows still in the batch reach the store at the next checkpoint (a few seconds).
        List<KillRecord> kills;
        List<LevelRecord> events;
        lock (_storeLock)
        {
            kills = _store.KillsSince(_logs.Key, from);
            events = _store.LevelEventsSince(_logs.Key, from);
        }
        return ShareBuilder.Build(appVersion, _logs.Server, _logs.Key, salt, AllVisits().Where(v => v.Closed).ToList(), journeys,
            kills, events, _engine.State, _options, since, Facts());
    }

    /// <summary>
    /// <see cref="SharePayload"/> and whether it can be trusted to say what the app no longer builds, read together on the
    /// tracker thread: false while a history read is going, the checkpoint is from an older engine, or no history was read
    /// (docs/specs/share-without-derived.md).
    /// </summary>
    public ShareRead ShareRead(string appVersion, byte[] salt, DateTime? since = null) =>
        new(SharePayload(appVersion, salt, since), HasHistory && !Importing && !NeedsRebuild && !_importFailed);

    /// <summary>The Ask page's tables from every source, solo and grouped together.</summary>
    public AskData AskData() => Analysis.AskData.From(AskSegments().Segments, RouteAnalysis.FarmPatch, Now, onlyWithRate: true);

    /// <summary>
    /// Every stretch of leveling the Ask page answers from: closed instance visits (crawls and others) and open-world hunts,
    /// each with its trio, its reincarnation rank then (times compare at rank 0) and how much of it was grouped. Left out under
    /// the DDD rule: visits from before the nerf where DDD did most of the damage, and open-world hunts from before it
    /// played on a bard trio or an unknown one (damage isn't kept outside instances, so a bard's DDD can't be ruled out there).
    /// Rebuilt when a visit closes, a hunt grows, a reincarnation lands, or the hour turns (the fade moves on).
    /// </summary>
    private (List<LevelingSegment> Segments, int LeftOut, int OpenLeftOut, int BonusLeftOut) AskSegments()
    {
        int n, fieldCount;
        lock (_historyLock) { n = _history.Count; fieldCount = _fieldKills.Count; }
        var s = _engine.State;
        string key = $"{n}|{fieldCount}|{s.Roster.Classes.Values.Sum(i => i.Rank)}|{SpellClasses != null}|{SettingsKey()}|{Now:yyyyMMddHH}";
        if (_askCache.Segments != null && _askCache.Key == key) return (_askCache.Segments, _askCache.LeftOut, _askCache.OpenLeftOut, _askCache.BonusLeftOut);

        List<KillRecord> field;
        lock (_historyLock) field = _fieldKills.ToList();
        List<PathfinderStore.LootRow> loot;
        List<LevelRecord> deaths;
        lock (_storeLock)
        {
            loot = _store.AllLoot(_logs.Key).Where(l => !l.InInstance).ToList();
            deaths = _store.LevelEventsSince(_logs.Key, DateTime.MinValue).Where(e => e.Kind == "death").ToList();
        }
        var (segments, leftOut, openLeftOut, bonusLeftOut) = Facts().Segments(field, loot, deaths, _options);
        _askCache = (key, segments, leftOut, openLeftOut, bonusLeftOut);
        return (segments, leftOut, openLeftOut, bonusLeftOut);
    }

    /// <summary>The Ask page's answer to one question (<see cref="Pages.AskPage.Build"/>), from your play and the players who share.</summary>
    public AskAnswer AskPage(AskQuery query)
    {
        var (segments, leftOut, openLeftOut, bonusLeftOut) = AskSegments();
        var st = _engine.State;
        // The trio you're on: its most-reincarnated class sets the XP every answer is for.
        int rank = (st.Trio?.Split('/') ?? []).Select(c => st.Roster.Classes.GetValueOrDefault(c)?.Rank ?? 0).DefaultIfEmpty(0).Max();
        return Pages.AskPage.Build(query, new AskInputs(segments, leftOut, openLeftOut, _options, st.Level ?? 1, st.Trio, rank,
            CommunityForYou(), Now, bonusLeftOut, FarmRate: FarmRateNow(),
            RankAtLevel: L => st.Roster.TrioRankAt(st.Trio?.Split('/'), L, _options.BaseCap)));
    }

    /// <summary>
    /// What every visit was (the live one too): its trio (/who, or carried and checked against the spells cast), rank,
    /// the DDD rule, how grouped it was. Every page reads these. Rebuilt when a visit closes, a reincarnation lands, the
    /// spell file arrives, or the live visit's casts grow by ten.
    /// </summary>
    public PlayFacts Facts()
    {
        int n;
        lock (_historyLock) n = _history.Count;
        var s = _engine.State;
        var key = $"{n}|{SpellClasses != null}|{s.Trio}|{s.Loadout}|{s.Stints.Count}|{s.Roster.Classes.Values.Sum(i => i.Rank)}|{s.Current?.Id}|{(s.Current?.Casts.Values.Sum() ?? 0) / 10}";
        if (_facts.Facts == null || _facts.Key != key)
        {
            Dictionary<string, (int Kills, int Party)> party;
            lock (_historyLock) party = new(_party);
            _facts = (key, PlayFacts.From(AllVisits(), s, SpellClasses, party));
        }
        return _facts.Facts;
    }

    /// <summary>
    /// Which trio was playing when (<see cref="Facts"/>), for the fight meter's "usual". The same object until the facts
    /// change, so a caller can tell when there's a new one.
    /// </summary>
    public TrioTimeline TrioTimeline()
    {
        var facts = Facts();
        if (_timeline.Facts != facts || _timeline.Timeline == null)
            _timeline = (facts, new TrioTimeline(facts.Visits.Select(v => (v.Start, Trio: facts.Trio(v.Id).Trio))
                .Where(x => x.Trio != null).Select(x => (x.Start, (string?)x.Trio))
                .Concat(_engine.State.Stints.Select(x => (x.From, x.Trio)))));
        return _timeline.Timeline;
    }

    /// <summary>The trio behind every visit and how it's known (<see cref="Facts"/>).</summary>
    public IReadOnlyDictionary<string, TrioCall> Trios() => Facts().Calls;

    /// <summary>Each closed visit's most-cast spells ("Ward Undead ×12"): why it got the trio it did.</summary>
    public Dictionary<string, string[]> CastSummary() => AllVisits().Where(v => v.Closed).ToDictionary(v => v.Id,
        v => v.Casts.OrderByDescending(c => c.Value).Take(5).Select(c => $"{c.Key} ×{c.Value}").ToArray());

    /// <summary>Waits for the spell file (read in the background) so the Plan can say what each class did. False on timeout.</summary>
    public bool WaitForGameData(TimeSpan timeout)
    {
        try { return _spellClasses.Wait(timeout); }
        catch (AggregateException) { return false; }
    }

    /// <summary>History → Relevels: each relevel's pace against your others in the same places, and where the hours went.</summary>
    public RelevelPaceResult RelevelPaceData(List<Journey>? journeys = null) =>
        RelevelPaces.Build(Facts(), journeys ?? Journeys(), _options);
}
