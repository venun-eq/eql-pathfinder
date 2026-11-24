using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Pages;

// The Ask page's answer, as web/js/ask.js reads it (camelCase JSON). Property order is the JSON order.

public sealed record AskAnswer(string Kind, string Q, string Where, string With, int Level, int LevelCap, int Visits, int DddLeftOut,
    int OpenLeftOut, double FarmRate, int Rank, string? Trio, int XpExtra, double HalfLifeDays, AskSources Sources, AskRoute Route,
    AskFarm Farm, AskDamage Damage, AskDifficulty Difficulty, AskPool? Community, bool CommunityOpenWorld, AskPlaces? Places = null,
    string? CommunityGap = null, double? DriftPerMonth = null, int BonusLeftOut = 0, int NoRateLeftOut = 0);

/// <summary>Every place you leveled, level by level (the "Every place" question; null for the others).</summary>
/// <param name="Levels">Levels 1–49, each with every place open there that was played within a level of it.</param>
/// <param name="Map">One bar per stretch of leveling, from the level it started at to the level it reached.</param>
public sealed record AskPlaces(string Scope, IReadOnlyList<AskPlaceLevel> Levels, IReadOnlyList<AskMapBar> Map, IReadOnlyDictionary<string, double?> Falls);

/// <param name="Window">The levels its rows draw on ("39-41").</param>
public sealed record AskPlaceLevel(int Level, string Window, IReadOnlyList<AskPlaceRow> Rows);

/// <param name="Crawls">The most visits behind any one level of the window.</param>
/// <param name="Enough">Rests on at least <see cref="Ask.MinCrawls"/> visits and <see cref="Ask.MinHours"/> hours: rankable.</param>
/// <param name="Exact">Played at this level itself, not only at the ones beside it.</param>
/// <param name="Fading">It was the best place 3 levels back and another now clearly beats it here (<see cref="PlaceCheck"/>, docs/pace.md).</param>
public sealed record AskPlaceRow(string Place, int Crawls, double Hours, double LevelsPerHour, double T5PerHour, double HighTierPerHour,
    double FragmentsPerHour, double DeathsPerHour, double KillsPerMinute, bool Enough, string Last, bool Exact, bool Fading = false, double? Cost = null);

/// <summary>One visit or hunt as it happened (Every place lists them under a cell, and draws why a place's levels/h falls).</summary>
/// <param name="XpPerKill">Kill XP per kill, % of a level, at the run's own XP penalty (a crawl's completion XP not counted).</param>
/// <param name="Rank">The trio's reincarnation rank then: the XP penalty the run played at.</param>
public sealed record AskMapBar(string Place, int Level, double Gained, double LevelsPerHour, double T5PerHour, string? Trio, DateTime Start, string Type,
    double Minutes = 0, double KillsPerMinute = 0, double XpPerKill = 0, int Rank = 0);

/// <summary>What the answer rests on: visits and hunts, and how many were grouped (all of them, and those near the levels asked about).</summary>
public sealed record AskSources(int Instances, int Open, int Grouped, int InRangeGrouped, int InRange);

/// <param name="Solo">The same question from solo play only (null when there is none); <paramref name="Group"/> likewise.</param>
/// <param name="Community">The route the players who share would take on their numbers alone (null without them).</param>
/// <param name="VersusSame">Whether the gap overall is within noise (about the same time); each row says its own Slower and Tone.</param>
/// <param name="Versus">You against the players who share, place by place at these levels, slowest against them first.</param>
public sealed record AskRoute(int From, int To, string Goal, double Minutes, double T5, IReadOnlyList<AskLeg> Legs, IReadOnlyList<int> Gaps,
    AskRouteSummary Fastest, AskRouteSummary WithMotes, AskSide? Solo, AskSide? Group, IReadOnlyList<AskAlternative> Alternatives,
    int PlacesSeen, double SwitchMinutes, AskPoolRoute? Community, IReadOnlyList<AskVersus> Versus, double VersusGap = 0, bool VersusSame = false)
{
    /// <summary>"D4", "mixed", or null with no route: what Home compares relevels with (<see cref="DifficultyMix"/>).</summary>
    public string? Mostly => DifficultyMix.OfRoute(Legs);
}

/// <param name="Mine">Your minutes a level there, on average; <paramref name="Theirs"/> the players'. Kills a minute are 0 when either side lacks them.</param>
public sealed record AskVersus(string Place, int From, int To, int Levels, double Mine, double Theirs, int Crawls, int Players,
    double KillsMine, double KillsTheirs, double Gap = 0, string Why = "", int KillPercent = 0, int PerKillPercent = 0, bool Slower = false, string Tone = "soft");

/// <param name="Minutes">Yours and the players' together where both have the place (Ask.PoolVisits); <paramref name="Mine"/> yours alone.</param>
/// <param name="Typical">The players who share at the same place and levels, alone (null unless they have every level).</param>
/// <param name="MineShare">How much of the time is yours: 1 from your logs alone, 0 from the players' alone.</param>
public sealed record AskLeg(string Place, int From, int To, double Minutes, double PerLevel, int Crawls, double T5PerHour, string? LastRun,
    bool BeforePatch, bool OpenWorld, double? Typical, int TypicalPlayers, double? Mine = null, double MineShare = 1, string Tone = "soft");

public sealed record AskRouteSummary(double Minutes, double T5, IReadOnlyList<string> Places);

public sealed record AskSide(double Minutes, double T5, IReadOnlyList<string> Places, int Covered);

public sealed record AskAlternative(string Place, double Minutes, bool Whole, int From, int To);

public sealed record AskPoolRoute(double Minutes, double T5, int Covered, IReadOnlyList<string> Places, IReadOnlyList<AskPoolLeg> Legs);

public sealed record AskPoolLeg(string Place, int From, int To, double Minutes, double PerLevel, double T5PerHour, int Players);

/// <param name="CommunityAllTime">When no shared farm has enough players since the patch: theirs from all time (the page says so).</param>
/// <param name="Crawls">Your crawls at this level in this scope, ranked in <c>Rows</c> or not (<see cref="Ask.FarmCrawls"/>).</param>
/// <param name="Unrated">Your crawls at this level in this scope with no rate yet: with none ranked and some of these, the page says too little, not none.</param>
/// <param name="MinCrawls">What a place needs to be ranked: <see cref="Ask.MinCrawls"/> crawls and <see cref="Ask.MinHours"/> hours.</param>
public sealed record AskFarm(int At, string Band, string Scope, IReadOnlyList<AskBand> Rows, AskBand? Before,
    IReadOnlyList<AskPoolBand>? Community, IReadOnlyList<AskPoolBand>? CommunityAllTime, int Crawls, int MinCrawls, double MinHours, int Unrated = 0);

public sealed record AskBand(string Place, int Crawls, double Hours, double LevelsPerHour, double T5PerHour, double HighTierPerHour,
    double FragmentsPerHour, double DeathsPerHour, double Dps);

public sealed record AskPoolBand(string Place, int Crawls, double Hours, double LevelsPerHour, double T5PerHour, double HighTierPerHour,
    double FragmentsPerHour, double DeathsPerHour, double Dps, int Players);

public sealed record AskDamage(int At, string Band, IReadOnlyList<AskDamageRow> Rows);

public sealed record AskDamageRow(string Place, int Crawls, double Dps, double? Typical, int TypicalPlayers);

public sealed record AskDifficulty(AskDiffPick? Pick, AskDiffPick? Community, IReadOnlyList<AskDiffOption> Options);

public sealed record AskDiffPick(string Key, string Zone, int From, int To, IReadOnlyList<AskDiffColumn> Columns);

public sealed record AskDiffColumn(int D, int Crawls, double LevelsPerHour, double T5PerHour, double DeathsPerHour, double KillsPerMinute, int Players);

public sealed record AskDiffOption(string Key, string Label);

/// <summary>What stands behind the community numbers.</summary>
/// <param name="LeftYouOut">You share, and your own play is taken out of their numbers.</param>
/// <param name="YouAndOne">Places whose number rested on you and one other player, so there is none for you (this slice).</param>
public sealed record AskPool(int Players, int Crawls, DateTime Generated, int Places, int MinPlayers, int MinCrawls, double MaxShare,
    int Stretches = 0, bool SoloGroup = false, bool LeftYouOut = false, IReadOnlyList<string>? YouAndOne = null);

/// <param name="Segments">Every stretch of your leveling (<see cref="PlayFacts.Segments"/>).</param>
/// <param name="Rank">The rank of the trio you're on (its most-reincarnated class): answers are for the XP it needs.</param>
/// <param name="Community">The players who share (null without their numbers).</param>
// FarmRate stays trailing-optional: the tests build AskInputs positionally; the one app caller (TrackerService) always passes it.
public sealed record AskInputs(IReadOnlyList<LevelingSegment> Segments, int LeftOut, int OpenLeftOut, TrackerOptions Options, int Level,
    string? Trio, int Rank, CommunityData? Community, DateTime Now, int BonusLeftOut = 0, int NoRateLeftOut = 0, double FarmRate = TrackerOptions.DefaultFarmRate);

public static class AskPage
{
    private sealed record BuildContext(
        string Q, string Where, string With, int From, int To, bool Motes, int At, bool Patch,
        List<LevelingSegment> Picked, AskData Data, double Xp, CommunityView? Pool,
        List<LevelingSegment> Counted, List<LevelingSegment> InRange, int FarmUnrated, int LevelUnrated)
    {
        public string Around => At < 50 ? AskData.WindowOf(At) : AskData.BandOf(At);
    }

    /// <summary>
    /// The Ask page's answer to one question ("EQL Pathfinder: Sharing spec", Questions the pool answers), from your own logs.
    /// Unset parts of the question start from where you are: your level, and your Goal for counting motes. Where (instances,
    /// open world) and With (solo, group) narrow the play it answers from; the solo and grouped routes always come back too.
    /// The route's times are yours and the players' who share together, yours counting more the more visits you have
    /// (<see cref="Ask.PoolVisits"/>); places only they have played join it. Their route on their numbers alone, every
    /// other question's answers, and you against them place by place show beside yours.
    /// </summary>
    public static AskAnswer Build(AskQuery query, AskInputs input)
    {
        var ctx = CreateContext(query, input);
        var o = input.Options;
        bool openWithoutPool = input.Community != null && ctx.Pool == null && ctx.Where == "open";
        string? communityGap = input.Community != null && ctx.Pool == null
            ? (ctx.Where == "open" ? "the open world" : "solo and grouped play apart") : null;

        return new AskAnswer("ask", ctx.Q, ctx.Where, ctx.With, input.Level, o.LevelCap, ctx.Data.Visits,
            input.LeftOut, input.OpenLeftOut, Math.Round(input.FarmRate),
            input.Rank, input.Trio, (int)Math.Round((ctx.Xp - 1) * 100), AskData.HalfLifeDays,
            BuildSources(ctx),
            BuildRoute(ctx, input),
            BuildFarm(ctx),
            BuildDamage(ctx),
            BuildDifficulty(query, ctx),
            BuildPoolSummary(ctx, input),
            openWithoutPool,
            ctx.Q == "places" ? Places(ctx.Data, ctx.Picked, ctx.Patch, o.LevelCap, ctx.Xp, input.FarmRate, input.Trio) : null,
            communityGap,
            ctx.Data.Drift?.PerMonthPercent, input.BonusLeftOut, ctx.Q == "farm" ? ctx.FarmUnrated : ctx.LevelUnrated);
    }

    private static BuildContext CreateContext(AskQuery query, AskInputs input)
    {
        var o = input.Options;
        int level = input.Level;
        string q = query.Q is "level" or "farm" or "diff" or "dmg" or "places" ? query.Q : "level";
        string where = query.Where is "instances" or "open" ? query.Where : "all";
        string with = query.With is "solo" or "group" ? query.With : "either";
        int from = Math.Clamp(query.From ?? (level < o.LevelCap ? level : 1), 1, 49);
        int to = Math.Clamp(query.To ?? (level < o.LevelCap ? Math.Min(o.LevelCap, from + 10) : o.LevelCap), from + 1, 50);
        bool motes = (query.Goal ?? (o.Goal == "speed" ? "fast" : "motes")) == "motes";
        int at = Math.Clamp(query.At ?? level, 1, 50);
        bool patch = query.Scope != "all";

        var picked = LevelingSegment.Filter(input.Segments, where, with).ToList();
        var d = AskData.From(picked, RouteAnalysis.FarmPatch, input.Now, onlyWithRate: true);
        double xp = Planner.XpMultiplier(input.Rank);
        var pool = input.Community?.For(where, with, o.FragmentValue);
        var counted = picked.Where(AskData.Counts).ToList();
        var inRange = counted.Where(x => x.LevelStart is { } ls && ls < to && ls >= from - 5).ToList();

        // The farm answer leaves out the visits of its own level (within one below the cap) and scope with no rate.
        // Crawls only, as its rows and the farm rate count them: open-world hunts and other instances are not crawls.
        int farmUnrated = d.UnratedAt.Count(u => u.Type == "crawl"
            && (at >= o.LevelCap ? u.Level >= o.LevelCap : Math.Abs(u.Level - at) <= AskData.WindowLevels)
            && (!patch || u.Start >= RouteAnalysis.FarmPatch));
        int levelUnrated = d.UnratedAt.Count(u => u.Level < to && u.Level >= from - 5);

        return new BuildContext(q, where, with, from, to, motes, at, patch, picked, d, xp, pool, counted, inRange, farmUnrated, levelUnrated);
    }

    private static AskSources BuildSources(BuildContext ctx) => new(
        ctx.Counted.Count(x => x.Type != "open world"),
        ctx.Counted.Count(x => x.Type == "open world"),
        ctx.Counted.Count(x => x.Grouped),
        ctx.InRange.Count(x => x.Grouped),
        ctx.InRange.Count);

    private static AskRoute BuildRoute(BuildContext ctx, AskInputs input)
    {
        var d = ctx.Data;
        var pool = ctx.Pool;
        double xp = ctx.Xp;
        int from = ctx.From, to = ctx.To;
        var fast = Ask.Chain(d, from, to, false, input.FarmRate, xp, pool?.Ask);
        var withMotes = Ask.Chain(d, from, to, true, input.FarmRate, xp, pool?.Ask);
        var route = ctx.Motes ? withMotes : fast;
        var cRoute = pool == null ? null : Ask.Chain(pool.Ask, from, to, ctx.Motes, input.FarmRate, xp);

        AskData SliceData(string w) => AskData.From(LevelingSegment.Filter(input.Segments, ctx.Where, w), RouteAnalysis.FarmPatch, input.Now, onlyWithRate: true);
        AskData? PoolFor(string w) => input.Community?.For(ctx.Where, w, input.Options.FragmentValue)?.Ask;
        AskSide? Side(Ask.Route r) => r.Legs.Count == 0 ? null : new AskSide(r.Minutes, r.T5, r.Legs.Select(l => l.Place).ToList(), to - from - r.Gaps.Count);
        static AskRouteSummary Summary(Ask.Route r) => new(r.Minutes, r.T5, r.Legs.Select(l => l.Place).ToList());

        var poolRoute = cRoute == null || cRoute.Legs.Count == 0 ? null : new AskPoolRoute(cRoute.Minutes, cRoute.T5, to - from - cRoute.Gaps.Count,
            cRoute.Legs.Select(l => l.Place).ToList(),
            cRoute.Legs.Select(l => new AskPoolLeg(l.Place, l.From, l.To, l.Minutes, Math.Round(l.Minutes / l.Levels, 1), l.T5PerHour,
                PlayersInStretch(pool, l.Place, l.From, l.To))).ToList());

        var against = pool == null ? [] : Ask.Against(d, pool.Ask, from, to, xp);
        var versus = against.Select(v => new AskVersus(v.Place, v.From, v.To, v.Levels,
            v.Mine, v.Theirs, v.Visits, PlayersInStretch(pool, v.Place, v.From, v.To), v.KillsMine, v.KillsTheirs, v.Gap, v.Why,
            (int)Math.Round(v.KillGap * 100, MidpointRounding.AwayFromZero),
            (int)Math.Round(v.PerKillGap * 100, MidpointRounding.AwayFromZero),
            v.Gap >= Ask.VersusNoise, Ask.VersusTone(v.Gap))).ToList();
        double overall = Ask.VersusOverall(against);

        return new AskRoute(from, to, ctx.Motes ? "motes" : "fast", route.Minutes, route.T5,
            BuildLegs(route, pool), route.Gaps, Summary(fast), Summary(withMotes),
            Side(Ask.Chain(SliceData("solo"), from, to, ctx.Motes, input.FarmRate, xp, PoolFor("solo"))),
            Side(Ask.Chain(SliceData("group"), from, to, ctx.Motes, input.FarmRate, xp, PoolFor("group"))),
            Ask.Alternatives(d, from, to, route, xpMultiplier: xp).Select(a => new AskAlternative(a.Place, a.Minutes, a.Whole, a.From, a.To)).ToList(),
            d.Places.Count(p => Enumerable.Range(from, to - from).Any(L => p.At(L) != null)),
            Ask.SwitchMinutes, poolRoute, versus, overall, Math.Abs(overall) < Ask.VersusNoise);
    }

    /// <summary>The fewest players behind any level of a stretch, each level read with the ones beside it (as the route reads it).</summary>
    private static int PlayersInStretch(CommunityView? pool, string place, int from, int to) =>
        pool == null ? 0 : Enumerable.Range(from, to - from).Select(L => pool.PlayersNear(place, L)).DefaultIfEmpty(0).Min();

    private static List<AskLeg> BuildLegs(Ask.Route r, CommunityView? pool) =>
        r.Legs.Select(l => new AskLeg(l.Place, l.From, l.To, l.Minutes, Math.Round(l.Minutes / l.Levels, 1),
            l.MaxCrawls, l.T5PerHour, l.LastRun?.ToString("yyyy-MM-dd"), l.LastRun < RouteAnalysis.FarmPatch, l.Place.EndsWith(" · open world"),
            l.Theirs, l.Theirs == null ? 0 : PlayersInStretch(pool, l.Place, l.From, l.To), l.Mine, l.MineShare,
            l.Mine is double m && l.Theirs is double t && t > 0 ? Ask.VersusTone(m / t - 1) : "soft")).ToList();

    private static AskFarm BuildFarm(BuildContext ctx)
    {
        var d = ctx.Data;
        var pool = ctx.Pool;
        var farms = Ask.Farms(d, ctx.At, ctx.Patch);
        var before = ctx.Patch && farms.Count == 0 ? Ask.Farms(d, ctx.At, false).FirstOrDefault() : null;
        var poolFarms = pool == null ? null : Ask.Farms(pool.Ask, ctx.At, ctx.Patch);

        AskBand ToBand(BandStat b) => new(b.Place, b.Crawls, b.Hours, Math.Round(b.LevelsPerHour / ctx.Xp, 2), b.T5PerHour,
            b.HighTierPerHour, b.FragmentsPerHour, b.DeathsPerHour, b.Dps);
        AskPoolBand ToPoolBand(BandStat b) => new(b.Place, b.Crawls, b.Hours, Math.Round(b.LevelsPerHour / ctx.Xp, 2), b.T5PerHour,
            b.HighTierPerHour, b.FragmentsPerHour, b.DeathsPerHour, b.Dps, pool!.PlayersFor(b, ctx.At));

        return new AskFarm(ctx.At, ctx.Around, ctx.Patch ? "patch" : "all",
            farms.Take(8).Select(ToBand).ToList(),
            before == null ? null : ToBand(before),
            poolFarms?.Take(8).Select(ToPoolBand).ToList(),
            pool != null && ctx.Patch && poolFarms!.Count == 0 ? Ask.Farms(pool.Ask, ctx.At, false).Take(8).Select(ToPoolBand).ToList() : null,
            Ask.FarmCrawls(d, ctx.At, ctx.Patch), Ask.MinCrawls, Ask.MinHours, ctx.FarmUnrated);
    }

    private static AskDamage BuildDamage(BuildContext ctx)
    {
        var pool = ctx.Pool;
        // Shared DPS around this level; falls back to the 10-level band when a community file lacks per-level extras.
        bool poolPerLevel = pool != null && pool.Ask.Places.Any(p => p.Extras != null);
        (double Dps, int Players)? Typical(string place)
        {
            if (pool == null) return null;
            var t = ctx.At < 50 && poolPerLevel
                ? pool.Ask.Near(ctx.At, false).FirstOrDefault(x => x.Place == place && x.Dps > 0)
                : pool.Ask.Bands.FirstOrDefault(x => x.Place == place && x.Band == AskData.BandOf(ctx.At) && !x.SincePatch && x.Dps > 0);
            return t == null ? null : (t.Dps, ctx.At < 50 && poolPerLevel ? pool.PlayersNear(place, ctx.At) : pool.PlayersFor(t));
        }

        var rows = Ask.Damage(ctx.Data, ctx.At).Take(8).Select(b =>
        {
            var typical = Typical(b.Place);
            return new AskDamageRow(b.Place, b.Crawls, b.Dps, typical?.Dps, typical?.Players ?? 0);
        }).ToList();
        return new AskDamage(ctx.At, ctx.Around, rows);
    }

    private static AskDifficulty BuildDifficulty(AskQuery query, BuildContext ctx)
    {
        var d = ctx.Data;
        var pool = ctx.Pool;
        // One zone per answer: both local and community results match the requested zone (or return none).
        // A pick neither has any more (Where or Who with changed) starts from your level again; only with no zone of your
        // own does the shared zone nearest your level answer.
        string? asked = query.Diff is { } key && (d.Difficulties.Any(x => x.Key == key) || pool?.Ask.Difficulties.Any(x => x.Key == key) == true) ? key : null;
        var diff = asked != null ? d.Difficulties.FirstOrDefault(x => x.Key == asked) : Ask.NearestDifficulty(d, ctx.At);
        asked ??= diff?.Key;
        var cDiff = pool == null ? null
            : asked != null ? pool.Ask.Difficulties.FirstOrDefault(x => x.Key == asked) : Ask.NearestDifficulty(pool.Ask, ctx.At);

        AskDiffPick ToPick(DifficultyCompare p, bool shared) => new(p.Key, p.Zone, p.From, p.To, p.Columns.Select(x => new AskDiffColumn(
            x.Difficulty, x.Crawls, Math.Round(x.LevelsPerHour / ctx.Xp, 2), x.T5PerHour, x.DeathsPerHour, x.KillsPerMinute,
            shared ? pool!.PlayersFor(p, x.Difficulty) : 0)).ToList());

        var options = d.Difficulties.Concat(pool?.Ask.Difficulties ?? []).DistinctBy(x => x.Key)
            .Select(x => new AskDiffOption(x.Key, $"{x.Zone} {x.From}–{x.To}")).ToList();
        return new AskDifficulty(diff == null ? null : ToPick(diff, false), cDiff == null ? null : ToPick(cDiff, true), options);
    }

    private static AskPool? BuildPoolSummary(BuildContext ctx, AskInputs input)
    {
        if (ctx.Pool == null) return null;
        var file = input.Community!.File;
        return new AskPool(file.Players, file.Crawls, file.Generated, file.Places.Count,
            file.Rules.MinPlayers, file.Rules.MinCrawls, file.Rules.MaxShare, file.Stretches, input.Community.HasSoloGroup,
            ctx.Pool.LeftYouOut, ctx.Pool.YouAndOne.ToList());
    }

    /// <summary>
    /// Every place at every level, from the same per-level table as the route (older visits fade, levels per hour for the
    /// XP your trio needs, each level read with the ones beside it), and the map of where you leveled. The page ranks them
    /// by the measure you pick, and its best-place-per-level strip comes from the same rows.
    /// </summary>
    private static AskPlaces Places(AskData d, IReadOnlyList<LevelingSegment> picked, bool patch, int cap, double xp, double farmRate, string? trio)
    {
        var levels = Enumerable.Range(1, Math.Min(49, cap - 1)).Select(L => new AskPlaceLevel(L, AskData.WindowOf(L),
            d.Near(L, patch).Select(b => new AskPlaceRow(b.Place, b.Crawls, b.Hours, Math.Round(b.LevelsPerHour / xp, 2), b.T5PerHour,
                b.HighTierPerHour, b.FragmentsPerHour, b.DeathsPerHour, b.KillsPerMinute, Ask.Enough(d, b), b.Last.ToString("yyyy-MM-dd"), b.Exact,
                PlaceCheck.At(d, b.Place, L, xp, ZoneGates.Open)?.Kind == "fading", RouteAnalysis.CostPerLevel(farmRate, b.T5PerHour, b.LevelsPerHour / xp)))
                .ToList())).ToList();
        var bars = picked.Where(s => s.LevelStart is { } ls && ls < cap && s.Levels > 0 && (!patch || s.Start >= RouteAnalysis.FarmPatch)).OrderBy(s => s.Start).ToList();
        var map = bars
            .Select(s => new AskMapBar(s.Place, s.LevelStart!.Value, Math.Round(s.Levels, 2), s.ActiveMinutes > 0 ? Math.Round(s.Levels * 60 / s.ActiveMinutes, 2) : 0,
                s.ActiveMinutes > 0 ? Math.Round(s.T5 * 60 / s.ActiveMinutes, 1) : 0, s.Trio, s.Start, s.Type, Math.Round(s.PlayedMinutes, 1),
                s.PlayedMinutes > 0 ? Math.Round(s.Kills / s.PlayedMinutes, 2) : 0, Rates.XpPerKill(s.KillLevels, s.XpKills) is { } xk ? Math.Round(xk, 3) : 0, s.Rank)).ToList();
        return new AskPlaces(patch ? "patch" : "all", levels, map, Ask.Falls(bars, trio));
    }
}
