using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Live;

public sealed partial class TrackerService
{
    private (string? Key, PaceCheck? Value) _paceCache;
    private (string? Key, NextStep? Value) _nextCache;
    private (string? Key, FarmRateInfo? Info) _farmCache;
    private (List<LevelingSegment>? Segments, List<LevelingSegment>? Instances, Dictionary<string, LevelingSegment>? ById,
        Dictionary<string, List<LevelingSegment>>? ByPlace) _paceData;
    private (List<LevelingSegment>? Segments, Dictionary<string, Verdict>? Map, string? FarmKey) _judged;

    private List<Visit> AllVisits()
    {
        lock (_historyLock)
        {
            var all = new List<Visit>(_history);
            if (_engine.State.Current is { } c) all.Add(c);
            return all;
        }
    }

    /// <summary>Your farming rates from the logs (recomputed when a crawl closes).</summary>
    public FarmRateInfo FarmInfo()
    {
        int n;
        lock (_historyLock) n = _history.Count;
        var farmKey = $"{n}|{SettingsKey()}";
        if (_farmCache.Info == null || _farmCache.Key != farmKey)
        {
            List<Visit> closed;
            lock (_historyLock) closed = _history.ToList();
            _farmCache = (farmKey, RouteAnalysis.FarmRates(closed, _options));
        }
        return _farmCache.Info!;
    }

    /// <summary>The farm rate in use, worked out from its source each time it's asked (the logs' cached rates, then the setting). Nothing stores it.</summary>
    public double FarmRateNow() => RouteAnalysis.EffectiveFarmRate(FarmInfo(), _options);

    public LiveSnapshot BuildSnapshot()
    {
        var now = Now;
        var s = _engine.State;
        var eff = s.LastTime is { } lt && now > lt.AddMinutes(5) ? lt.AddMinutes(5) : now;
        var o = _options;
        double farmRate = FarmRateNow();
        var history = AllVisits().Where(v => v.Closed).ToList();
        var cur = s.Current;
        bool farming = IsFarmingMode(s.Level, CapNow);

        VisitStats? curStats = cur != null ? VisitStats.From(cur, eff, o, isCurrent: true) : null;
        double? cost = curStats is { LevelsPerHour: { } clv, T5PerHour: { } ct5 } && !farming
            ? RouteAnalysis.CostPerLevel(farmRate, ct5, clv) : null;
        PaceCheck? pace = CachedPaceOf(cur, curStats, farming, history.Count, now);

        double? levelPct = s.LevelPositionKnown ? Math.Min(100, s.KillPercentInLevel) : null;
        double? nextLevelMin = EstimateNextLevelMinutes(farming, levelPct, curStats, pace);
        var jv = BuildActiveJourneyView(s, CapNow);
        var next = CachedNextStep(history, cur, farming, farmRate, s, now);

        var chain = new List<Visit>(history.TakeLast(60));
        if (cur != null) chain.Add(cur);
        // At the cap the session is farming: a relevel crawl in the same place just before it (started below the cap) is not part of it.
        var session = SessionStats.CurrentSession(chain, 30, s.Level is { } sl && sl >= CapNow ? v => v.LevelStart is not { } ls || ls >= o.CapOn(v.Start, ls) : null);
        var sessionDrops = session.SelectMany(v => v.HighTier).OrderByDescending(d => d.Time).ToList();
        var recent = history.AsEnumerable().Reverse().Take(6).Select(v => VisitStats.From(v, eff, o)).ToList();

        bool notKnown = LoadoutOn.IsNotKnown(s.Loadout);
        var effectiveNext = notKnown && next != null
            ? next with { LastPace = null, LastPlace = null, LastNotClear = null, LastFarm = null, Place = null, Of = null }
            : next;
        return new LiveSnapshot(
            _logs.Name, s.Level, s.Trio, s.Race, s.Zone, s.LastTime,
            s.LastTime is { } t && (now - t).TotalMinutes > 5, _importing, _importPercent,
            farming ? "farming" : "leveling", curStats, cost, notKnown ? null : pace, levelPct, nextLevelMin,
            effectiveNext, jv, SessionStats.From(session, eff, o), sessionDrops, recent, _highTier.ToList(),
            s.Looted.T5(o.FragmentValue), s.Looted.HighTierT5(o.FragmentValue), o.FragmentValue, s.Loadout);
    }

    private bool IsFarmingMode(int? level, int levelCap) => ModeSetting switch
    {
        "Leveling" => false,
        "Farming" => true,
        _ => level is { } lv && lv >= levelCap,
    };

    private PaceCheck? CachedPaceOf(Visit? cur, VisitStats? curStats, bool farming, int historyCount, DateTime now)
    {
        if (cur == null || curStats == null || cur.Difficulty == null) return null;
        var key = $"{cur.Id}|{_revision}|{(int)curStats.ActiveMinutes}|{farming}|{historyCount}|{(int)curStats.IdleMinutes}|{SettingsKey()}|{now:yyyyMMddHH}";
        if (_paceCache.Key != key) _paceCache = (key, PaceOf(cur, curStats));
        return _paceCache.Value;
    }

    private static double? EstimateNextLevelMinutes(bool farming, double? levelPct, VisitStats? curStats, PaceCheck? pace)
    {
        if (farming || levelPct is not { } pct) return null;
        double? lvh = curStats is { LevelsPerHour: > 0.1 } c2 ? c2.LevelsPerHour : pace?.Run?.ExpectedLevelsPerHour;
        return lvh is > 0.1 ? (100 - pct) / 100 / lvh.Value * 60 : null;
    }

    private JourneyView? BuildActiveJourneyView(EngineState s, int levelCap)
    {
        var active = Journeys().FirstOrDefault(j => !j.Done && s.Trio != null && j.TrioKey == ClassNames.TrioKey(s.Trio));
        return active == null ? null : new JourneyView(active.Trio, active.Race, s.Level ?? active.LevelNow, levelCap, active.ActiveHours, active.EtaHours, active.EtaNote);
    }

    private NextStep? CachedNextStep(List<Visit> history, Visit? cur, bool farming, double farmRate, EngineState s, DateTime now)
    {
        if (cur != null && !cur.Completed) return null;
        // Include _revision so Reward Chest loot and trailing kills after "completed" update the done card immediately.
        var key = $"{history.Count}|{s.Level}|{cur?.Id}|{_revision}|{farming}|{farmRate}|{SettingsKey()}|{now:yyyyMMddHH}";
        if (_nextCache.Key != key) _nextCache = (key, BuildNext(history, cur, farming, s));
        return _nextCache.Value;
    }

    /// <summary>Between crawls: how the last one ranked, and the best next choice from your history (<see cref="NextRanking"/>).</summary>
    private NextStep? BuildNext(List<Visit> history, Visit? finished, bool farming, EngineState s)
    {
        var o = _options;
        VisitStats? lastStats = null;
        Verdict? judged = null;
        int? place = null, of = null;
        var last = finished ?? history.LastOrDefault(RouteAnalysis.Usable);
        if (last != null && last.Difficulty != null && (Now - last.EndTime).TotalHours < 6)
        {
            lastStats = VisitStats.From(last, last.EndTime, o);
            judged = Judge(last, lastStats);
            // The ranking is among the visits its usual is made of, so it shows only when there is a usual.
            if (judged?.Farm is { Rank: { } rank0 } fp)
            {
                place = rank0;
                of = fp.Runs + 1;
            }
        }
        string lastLabel = last != null ? $"{Short(last.BaseZone)}{(last.Difficulty is { } ld ? $" D{ld}" : "")}" : "";
        IReadOnlyList<NextOption> choices = [];
        NextStep? BuildStep() => lastStats == null && choices.Count == 0
            ? null
            : new NextStep { Last = lastStats, Place = place, Of = of, Options = choices, LastPace = judged?.Pace, LastPlace = judged?.Place, LastNotClear = judged?.NotClear, LastKind = judged?.Kind, LastFarm = judged?.Farm, LastBoss = lastStats?.Boss };

        // Where next comes from Ask's tables (instances: the overlay is between crawls), so the overlay, Home and the Ask page
        // give one answer: older visits fade, pre-nerf DDD is left out, levels are for the XP your trio needs.
        var d = Analysis.AskData.From(LevelingSegment.Filter(AskSegments().Segments, "instances", null), RouteAnalysis.FarmPatch, Now, onlyWithRate: true);
        Xp xp = Xp.Of(L => s.Roster.TrioRankAt(s.Trio?.Split('/'), L, _options.BaseCap));

        if (farming)
        {
            choices = NextRanking.Farms(d, CapNow, xp.At(CapNow), lastLabel);
            return BuildStep();
        }
        if (s.Level is not { } level || level >= CapNow) return BuildStep();
        // What the player is doing ranks the next place: the last visit played for motes ranks by cost against farming, else by speed.
        bool motes = judged != null ? VisitKinds.ByMotes(judged.Kind) : last != null && last.Difficulty is { } lastDiff && o.FarmDifficulties.Contains(lastDiff);
        choices = NextRanking.Leveling(d, level, CapNow, motes, FarmRateNow(), xp, lastLabel);
        return BuildStep();
    }

    private static string Short(string zone) => zone.StartsWith("The ") ? zone[4..] : zone;

    /// <summary>Developer preview: the snapshot as if your last leveling (or farming) crawl were still running (the last one started by <paramref name="before"/>, if given).</summary>
    public LiveSnapshot PreviewAsCurrent(bool farming, DateTime? before = null)
    {
        var o = _options;
        var history = AllVisits().Where(v => v.Closed).ToList();
        var v = history.LastOrDefault(x => RouteAnalysis.Usable(x, o.CapOn(x)) && (farming ? x.LevelStart >= o.CapOn(x) : x.LevelStart < o.CapOn(x)) && (before == null || x.Start <= before));
        var snap = BuildSnapshot();
        double farmRate = FarmRateNow();
        if (v == null) return snap;
        var stats = VisitStats.From(v, v.EndTime, o) with { State = "in progress" };
        var pace = PaceOf(v, stats);
        double? cost = !farming && stats is { LevelsPerHour: { } slv, T5PerHour: { } st5 } ? RouteAnalysis.CostPerLevel(farmRate, st5, slv) : null;
        return snap with
        {
            Mode = farming ? "farming" : "leveling",
            Current = stats,
            Pace = pace,
            CostPerLevel = cost,
            Next = null,
            NextLevelMinutes = farming ? null : 7,
            Level = v.LevelEnd,
            Trio = v.Trio ?? snap.Trio,
        };
    }

    // ------------------------------------------------------------------ pace, like for like

    /// <summary>Ask's instance visits and the leveling segments by visit and by place, rebuilt with <see cref="AskSegments"/>.</summary>
    private (List<LevelingSegment> Instances, Dictionary<string, LevelingSegment> ById, Dictionary<string, List<LevelingSegment>> ByPlace) PaceData()
    {
        var segs = AskSegments().Segments;
        if (!ReferenceEquals(_paceData.Segments, segs))
            _paceData = (segs, LevelingSegment.Filter(segs, "instances", null).ToList(),
                segs.Where(x => x.VisitId != null).GroupBy(x => x.VisitId!).ToDictionary(g => g.Key, g => g.First()),
                segs.GroupBy(x => x.Place).ToDictionary(g => g.Key, g => g.ToList()));
        return (_paceData.Instances!, _paceData.ById!, _paceData.ByPlace!);
    }

    private Verdict? Judge(Visit v, VisitStats st)
    {
        if (v.Difficulty == null || v.LevelStart == null) return null;
        if (st.Raid) return new(VisitKinds.Raid, null, null, null);
        var (instances, byId, byPlace) = PaceData();
        if (!byId.TryGetValue(v.Id, out var run))
        {
            // The live crawl (or one the DDD rule leaves out of Ask): judged from its stats now.
            var facts = Facts();
            var trio = facts.Trio(v.Id).Trio;
            run = LevelingSegment.From(st, facts.RankAt(trio, v.Start), facts.PartyShare(v.Id)) with { Trio = trio, RankAtLevel = facts.RankByLevel(trio, v.Start, _options), Cap = _options.CapOn(v) };
        }
        return Verdicts.Judge(run, instances, byPlace.GetValueOrDefault(run.Place) ?? [], _options.FarmDifficulties, _options,
            grouped => CommunityForYou()?.For("instances", grouped ? "group" : "solo"));
    }

    /// <summary>Every closed crawl judged, for History; rebuilt when Ask's segments or the farmed difficulties change.</summary>
    private Dictionary<string, Verdict> Judged()
    {
        var segs = AskSegments().Segments;
        string farmKey = SettingsKey();
        if (ReferenceEquals(_judged.Segments, segs) && _judged.Map != null && _judged.FarmKey == farmKey) return _judged.Map;
        var (instances, _, byPlace) = PaceData();
        var map = Verdicts.JudgeAll(segs, instances, byPlace, _options.FarmDifficulties, _options);
        _judged = (segs, map, farmKey);
        return map;
    }

    /// <summary>Judges an in-progress visit with the same <see cref="Judge"/> rules as History, or null if lacking difficulty/level.</summary>
    private PaceCheck? PaceOf(Visit v, VisitStats st) =>
        Judge(v, st) is { } j ? new PaceCheck(j.Kind, j.Pace, j.Place, j.Farm, st.Boss) : null;
}
