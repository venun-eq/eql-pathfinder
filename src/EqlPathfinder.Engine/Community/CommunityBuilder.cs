using System.Globalization;
using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Community;

/// <summary>When a number is published: enough players and crawls behind it, and no one player more than a share of it.</summary>
/// <param name="MinPlayers">Players behind a cell before it's published (the spec's 3; 2 while the pool is small).</param>
/// <param name="MinCrawls">Crawls behind a cell before it's published.</param>
/// <param name="MaxShare">The most weight one player can have in a cell (never below an equal share).</param>
/// <param name="MinPlayerDays">
/// Days a player must have been registered (the server's own clock, which shared facts can't fake) before their play counts:
/// made-up players have to be kept for that long. 0 = off, while sharing is invite-only; switched on (config
/// min_player_days) when registration opens (docs/community-job.md, "Fake players").
/// </param>
public sealed record CommunityRules(int MinPlayers = 2, int MinCrawls = 5, double MaxShare = 0.2, int MinPlayerDays = 0);

/// <summary>
/// Per-level (1–49) minutes per level (rank 0), T5-eq/h, and optional extra rates with sample counts.
/// <para>
/// Split loot fields (<c>Mote…</c> and <c>Fragment…</c>) allow each client to apply its own fragment value;
/// combined <c>T5PerHour</c> and <c>HighTierPerHour</c> stay populated at <see cref="CommunityBuilder.FragmentValue"/> for older clients.
/// The same goes for <see cref="CommunityBand"/> and <see cref="CommunityColumn"/>.
/// </para>
/// </summary>
public sealed record CommunityPlace(string Place, double?[] Minutes, double[] T5PerHour, int[] Crawls, int[] Players, DateTime?[] Last,
    double[]? Hours = null, double[]? HighTierPerHour = null, double[]? FragmentsPerHour = null, double[]? DeathsPerHour = null,
    double[]? KillsPerMinute = null, double[]? Dps = null,
    double[]? MoteT5PerHour = null, double[]? FragmentT5PerHour = null, double[]? MoteHighTierPerHour = null, double[]? FragmentHighTierPerHour = null);

public sealed record CommunityBand(string Band, string Place, bool SincePatch, int Crawls, int Players, double Hours, double LevelsPerHour,
    double T5PerHour, double HighTierPerHour, double FragmentsPerHour, double DeathsPerHour, double KillsPerMinute, double Dps, DateTime Last,
    double? MoteT5PerHour = null, double? FragmentT5PerHour = null, double? MoteHighTierPerHour = null, double? FragmentHighTierPerHour = null);

public sealed record CommunityColumn(int Difficulty, int Crawls, int Players, double LevelsPerHour, double T5PerHour, double DeathsPerHour,
    double KillsPerMinute, DateTime Last, double? MoteT5PerHour = null, double? FragmentT5PerHour = null);

public sealed record CommunityCompare(string Zone, int From, int To, IReadOnlyList<CommunityColumn> Columns);

/// <summary>One set of the Ask tables: places by level, places by level band, difficulties side by side.</summary>
public sealed record CommunityTables(IReadOnlyList<CommunityPlace> Places, IReadOnlyList<CommunityBand> Bands, IReadOnlyList<CommunityCompare> Difficulties);

/// <summary>
/// community/v1.json: the Ask tables built from everyone who shares, each player's numbers worked out on their own
/// play first (the same math as the app's Ask page), then combined cell by cell under <see cref="Rules"/>. Places,
/// Bands and Difficulties hold all play; open-world places end in " · open world" (schema 2).
/// </summary>
/// <param name="Solo">The same tables from solo play only (schema 2: mostly solo kills); <paramref name="Group"/> from grouped play.</param>
/// <param name="Stretches">Open-world hunting stretches behind the numbers (schema 2).</param>
public sealed record CommunityFile(int Schema, DateTime Generated, int Players, int Crawls, int LeftOut, CommunityRules Rules,
    IReadOnlyList<CommunityPlace> Places, IReadOnlyList<CommunityBand> Bands, IReadOnlyList<CommunityCompare> Difficulties,
    CommunityTables? Solo = null, CommunityTables? Group = null, int Stretches = 0);

/// <summary>
/// What a build saw, for the job's log line (not in the file): why play was left out, and how many place-and-level cells
/// have play against how many the file shows (a cell needs <see cref="CommunityRules.MinPlayers"/> players).
/// </summary>
public sealed class CommunityStats
{
    public Dictionary<string, int> LeftOut { get; } = new();
    public int CellsWithPlay { get; set; }
    public int CellsOnePlayer { get; set; }
    public int CellsPublished { get; set; }

    public void Count(string why, int n = 1)
    {
        if (n > 0) LeftOut[why] = LeftOut.GetValueOrDefault(why) + n;
    }

    public override string ToString() =>
        $"left out: {(LeftOut.Count == 0 ? "nothing" : string.Join(", ", LeftOut.OrderByDescending(x => x.Value).Select(x => $"{x.Value} {x.Key}")))}; " +
        $"{CellsPublished} of {CellsWithPlay} place-and-level cells shown ({CellsOnePlayer} have only one player)";
}

/// <summary>A shared crawl and whose it is.</summary>
public sealed record PlayerCrawl(string Player, SharedCrawl Crawl);

/// <summary>A shared open-world hunting stretch and whose it is.</summary>
public sealed record PlayerStretch(string Player, SharedStretch Stretch);

public static class CommunityBuilder
{
    /// <summary>2 = open-world stretches join the tables, and Solo and Group tables come beside them.</summary>
    public const int Schema = 2;

    /// <summary>
    /// Default fragment value (spell value) used in combined T5PerHour/HighTierPerHour fields for backwards compatibility;
    /// newer clients read split mote/fragment fields and apply their own setting.
    /// </summary>
    public const double FragmentValue = 2.0;

    /// <summary>The idle threshold the job works active time out with: the app's (<see cref="Tracking.TrackerOptions"/>).</summary>
    public static readonly double IdleThresholdSeconds = new Tracking.TrackerOptions().IdleThresholdSeconds;

    /// <summary>The level cap the job reads kills against: the app's. A kill at it adds no XP to a visit (the tracker's EmitKill).</summary>
    private static readonly int LevelCap = new Tracking.TrackerOptions().LevelCap;

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>A unit's facts (the JSON the app gzipped) as a crawl.</summary>
    public static SharedCrawl? ReadCrawl(string json) => JsonSerializer.Deserialize<SharedCrawl>(json, Json);

    /// <summary>
    /// One shared crawl as the Ask math takes it, or null when it's left out: a run from before the 11-10 DDD nerf that
    /// was mostly DDD (shared crawls carry only the day, so all of 11-10 counts as before), or numbers no crawl can have.
    /// </summary>
    public static LevelingSegment? Visit(SharedCrawl c) => Visit(c, out _);

    private static bool ValidZone(string? zone) => !string.IsNullOrWhiteSpace(zone) && zone.Length <= 80 && !zone.Any(char.IsControl);

    /// <param name="why">Why the crawl was left out (null when it's kept), for the job's log line.</param>
    public static LevelingSegment? Visit(SharedCrawl c, out string? why)
    {
        why = "unreadable facts";
        if (c == null) return null;
        why = "no day";
        if (!DateTime.TryParseExact(c.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return null;
        why = "no zone";
        if (!ValidZone(c.Zone)) return null;
        why = "impossible time or levels";
        if (c.Difficulty is < 0 or > 20 || c.Rank is < 0 or > 999 || c.LevelStart is < 1 or > 200 || c.LevelEnd is < 1 or > 200) return null;
        why = "mostly DDD before the nerf";
        if (day <= RouteAnalysis.FarmPatch.Date && DddShare(c) >= 0.5) return null;
        why = "during a bonus-XP event";   // faster than the place gives (the app leaves it out of its own Ask too)
        if (c.Bonus == true) return null;
        var (levels, killLevels, active) = Recompute(c);
        why = "impossible time or levels";
        if (!double.IsFinite(active) || !double.IsFinite(levels) || !double.IsFinite(killLevels) || active < 0 || active > 600 || levels < 0 || levels > 49) return null;
        if (active >= 3 && levels / (active / 60) > 60) return null;
        // Reject impossible level spans (e.g., from mid-crawl loadout swaps): a crawl cannot lose levels
        // or span 2+ levels on less than 1 level of XP (reward-only 49->50 dings and cap farming stay valid).
        if (c.LevelStart is { } ls && c.LevelEnd is { } le && (le < ls || le - ls >= 2 && levels < 1)) return null;

        var loot = Parts(c.Loot);
        double t5 = loot.MoteT5 + FragmentValue * loot.FragmentT5;
        why = "impossible loot";
        if (!double.IsFinite(t5) || t5 < 0 || t5 > 20_000 || (active >= 3 && t5 / (active / 60) > 2000)) return null;
        int frags = (int)Math.Min(int.MaxValue, c.Loot?.Fragments?.Where(x => x.Key is >= 1 and <= 10).Sum(x => (long)Math.Max(0, x.Value)) ?? 0L);
        why = null;

        var kills = c.Kills ?? [];
        var deaths = c.Deaths ?? [];
        long damageTotal = c.Damage is { Total: > 0 } dmg ? dmg.Total : 0;
        // DPS over active time, matching VisitStats (0 when below the rate threshold).
        double dps = Rates.PerSecond(damageTotal, active, c.Completed, kills.Count) ?? 0;
        // Midday, so a crawl on the patch day counts as after it in the "since the patch" bands.
        return new LevelingSegment(LevelingSegment.PlaceName(c.Zone, c.Difficulty), LevelingSegment.ZoneName(c.Zone), c.Difficulty, day.AddHours(12), c.LevelStart,
            active, levels, t5, loot.MoteHighTierT5 + FragmentValue * loot.FragmentHighTierT5, frags, kills.Count, deaths.Count, dps, c.Rank,
            c.Difficulty != null ? "crawl" : "instance", PartyShare(kills), c.Trio)
        { KillLevels = killLevels, Loot = loot, XpKills = KnownXpKills(kills), Completed = c.Completed };
    }

    /// <summary>
    /// A crawl's levels and active minutes worked out from its facts, as the app works them out from its own records:
    /// levels are the kills' XP plus the rewards' share; active time is the time inside less idle (<see cref="ActiveTime"/>).
    /// Idle needs the pauses and end (schema 4); a crawl from an older app without them keeps the active minutes it was
    /// sent with (<see cref="SharedCrawl.Derived"/>), until that app re-sends it.
    /// </summary>
    public static (double Levels, double KillLevels, double ActiveMinutes) Recompute(SharedCrawl c)
    {
        double killXp = 0;
        var killTimes = new List<double>();
        foreach (var k in c.Kills ?? [])
        {
            if (Num(k, 0) is { } t) killTimes.Add(t);
            if (Num(k, 4) is { } lvl && lvl >= LevelCap) continue;   // at the cap the app counts no XP for a kill
            killXp += Num(k, 1) ?? 0;
        }
        double rewardXp = (c.Rewards ?? []).Sum(r => Num(r, 1) ?? 0);
        double active = c.Derived?.ActiveMinutes ?? double.NaN;
        if (c.End is { } end)
        {
            if (!double.IsFinite(c.ActiveSeconds) || c.ActiveSeconds < 0 || end < 0)
                active = double.NaN;
            else
            {
                killTimes.Sort();
                var pauses = (c.Pauses ?? []).Where(p => p is { Length: >= 2 }).Select(p => ((double)p[0], (double)p[1])).ToList();
                double idle = Math.Min(ActiveTime.IdleSeconds(killTimes, end, pauses, IdleThresholdSeconds), c.ActiveSeconds);
                active = Math.Max(0, c.ActiveSeconds - idle) / 60;
            }
        }
        return ((killXp + rewardXp) / 100, killXp / 100, active);
    }

    /// <summary>
    /// An open-world stretch's minutes and levels from its kill rows, as the app's <see cref="FieldHunts"/> works them out:
    /// first kill to last, plus a minute for the first pull, and the kills' XP. No kills: what it was sent with.
    /// </summary>
    public static (double Minutes, double Levels) Recompute(SharedStretch s)
    {
        var kills = s.Kills ?? [];
        var times = kills.Select(k => Num(k, 0)).OfType<double>().ToList();
        if (times.Count == 0) return s.Derived != null ? (s.Derived.Minutes, s.Derived.Levels) : (double.NaN, double.NaN);
        return ((times.Max() - times.Min()) / 60 + 1, kills.Sum(k => Num(k, 1) ?? 0) / 100);
    }

    /// <summary>A crawl's loot in parts: motes at their T5-eq and fragments at 1× a same-tier mote, all and above T5.</summary>
    public static LootParts Parts(SharedLoot? l)
    {
        double m = 0, mh = 0, f = 0, fh = 0;
        if (l?.Motes != null) foreach (var (tier, n) in l.Motes) Add(tier, n, ref m, ref mh);
        if (l?.Fragments != null) foreach (var (tier, n) in l.Fragments) Add(tier, n, ref f, ref fh);
        return new LootParts(m, mh, f, fh);

        static void Add(int tier, int n, ref double all, ref double high)
        {
            if (tier is < 1 or > 10 || n <= 0) return;
            double v = n * MoteTiers.T5Value((MoteTier)tier);
            all += v;
            if (MoteTiers.IsHighTier((MoteTier)tier)) high += v;
        }
    }

    /// <summary>The kills whose XP the log sized: rows with a number in xp. A null is an XP line with no percent, left out of XP per kill (kills-at-cap).</summary>
    private static int KnownXpKills(IReadOnlyList<CompactRow>? kills) =>
        kills == null ? 0 : kills.Count(k => Num(k, 1) != null && !(Num(k, 4) is { } lvl && lvl >= LevelCap));

    /// <summary>Position <paramref name="i"/> of a compact row, when it's a number.</summary>
    private static double? Num(CompactRow row, int i)
    {
        if (string.IsNullOrEmpty(row.Json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(row.Json);
            var r = doc.RootElement;
            return r.ValueKind == JsonValueKind.Array && r.GetArrayLength() > i && r[i].ValueKind == JsonValueKind.Number ? r[i].GetDouble() : null;
        }
        catch (JsonException) { return null; }
    }

    public static double DddShare(SharedCrawl c) =>
        c.Damage is not { Total: > 0, ByAbility: { } byAbility } ? 0
        : byAbility.Where(kv => RouteAnalysis.IsDdd(kv.Key)).Sum(kv => kv.Value > 0 ? (double)kv.Value : 0) / c.Damage.Total;

    /// <summary>The share of kills that gave party experience (kill rows are [t, xp, party, secs, level]).</summary>
    public static double PartyShare(IReadOnlyList<CompactRow>? kills)
    {
        if (kills == null || kills.Count == 0) return 0;
        int party = 0;
        foreach (var k in kills)
        {
            if (string.IsNullOrEmpty(k.Json)) continue;
            try
            {
                using var doc = JsonDocument.Parse(k.Json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 2)
                {
                    var p = doc.RootElement[2];
                    if (p.ValueKind == JsonValueKind.True || (p.ValueKind == JsonValueKind.Number && p.GetDouble() != 0)) party++;
                }
            }
            catch (JsonException) { }
        }
        return (double)party / kills.Count;
    }

    /// <summary>A unit's facts as an open-world stretch.</summary>
    public static SharedStretch? ReadStretch(string json) => JsonSerializer.Deserialize<SharedStretch>(json, Json);

    /// <summary>
    /// Converts a shared open-world stretch into a segment for Ask, or null if excluded.
    /// Uses the stretch's own trio/rank when supplied, falling back to the same player's nearest same-day crawl
    /// for older clients. Carries no loot (0 motes, never ranked as a farm). The DDD filter for the
    /// open world: before the nerf, a stretch on a bard trio or an unknown one is left out (damage isn't kept outside instances).
    /// </summary>
    public static LevelingSegment? Hunt(SharedStretch s, IReadOnlyList<SharedCrawl> playerCrawls) => Hunt(s, playerCrawls, out _);

    /// <param name="why">Why the hunt was left out (null when it's kept), for the job's log line.</param>
    public static LevelingSegment? Hunt(SharedStretch s, IReadOnlyList<SharedCrawl> playerCrawls, out string? why)
    {
        why = "unreadable facts";
        if (s == null) return null;
        why = "no day";
        if (!DateTime.TryParseExact(s.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return null;
        why = "hunt with no zone";
        if (!ValidZone(s.Zone)) return null;
        var zone = s.Zone!;
        why = "during a bonus-XP event";
        if (s.Bonus == true) return null;
        var d = Recompute(s);
        why = "impossible time or levels";
        if (!double.IsFinite(d.Minutes) || !double.IsFinite(d.Levels) || d.Minutes <= 0 || d.Minutes > 600 || d.Levels < 0 || d.Levels > 49) return null;
        if (d.Minutes >= 3 && d.Levels / (d.Minutes / 60) > 60) return null;
        // Prefer the stretch's own trio/rank; fall back to the same day's nearest shared crawl for older clients.
        var near = s.Trio == null ? playerCrawls.Where(c => c.Day == s.Day).MinBy(c => Math.Abs(c.Seq - s.Seq)) : null;
        var trio = s.Trio ?? near?.Trio;
        int rank = s.Trio != null ? s.Rank ?? 0 : near?.Rank ?? 0;
        if (rank is < 0 or > 999) return null;
        why = trio == null ? "hunt before the nerf, trio unknown" : "hunt before the nerf on a bard trio";
        if (day <= RouteAnalysis.FarmPatch.Date && (trio == null || trio.Split('/').Contains("BRD"))) return null;
        var kills = s.Kills ?? [];
        var firstLevel = FirstLevel(kills);
        if (firstLevel is < 1 or > 200) { why = "impossible time or levels"; return null; }
        why = null;
        return new LevelingSegment(LevelingSegment.OpenPlace(zone), LevelingSegment.ZoneName(zone), null, day.AddHours(12), firstLevel,
            d.Minutes, d.Levels, 0, 0, 0, kills.Count, 0, 0, rank, "open world", PartyShare(kills), trio)
        { KillLevels = d.Levels, XpKills = KnownXpKills(kills) };
    }

    /// <summary>The level at a stretch's first kill (kill rows are [t, xp, party, secs, level]).</summary>
    private static int? FirstLevel(IReadOnlyList<CompactRow>? kills)
    {
        if (kills == null) return null;
        foreach (var k in kills)
        {
            if (string.IsNullOrEmpty(k.Json)) continue;
            try
            {
                using var doc = JsonDocument.Parse(k.Json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 4
                    && doc.RootElement[4].ValueKind == JsonValueKind.Number) return doc.RootElement[4].GetInt32();
            }
            catch (JsonException) { }
        }
        return null;
    }

    public static CommunityFile Build(IEnumerable<PlayerCrawl> crawls, CommunityRules rules, DateTime now) => Build(crawls, [], rules, now);

    /// <summary>
    /// Every player's play (crawls, instances, open-world stretches) worked out on its own, then combined: all of it, and
    /// solo and grouped play apart (a visit or stretch is grouped when most of its kills gave party experience).
    /// </summary>
    /// <param name="stats">Filled in for the job's log line: why play was left out, and how much the file shows.</param>
    public static CommunityFile Build(IEnumerable<PlayerCrawl> crawls, IEnumerable<PlayerStretch> stretches, CommunityRules rules, DateTime now,
        CommunityStats? stats = null)
    {
        var all = new List<PlayerAsk>();
        var solo = new List<PlayerAsk>();
        var group = new List<PlayerAsk>();
        int used = 0, hunts = 0, leftOut = 0;
        var stretchesBy = stretches.GroupBy(s => s.Player).ToDictionary(g => g.Key, g => g.Select(x => x.Stretch).ToList());
        var crawlsBy = crawls.GroupBy(c => c.Player).ToDictionary(g => g.Key, g => g.Select(x => x.Crawl).ToList());
        LevelingSegment? Note(LevelingSegment? seg, string? why)
        {
            if (seg == null && why != null) stats?.Count(why);
            return seg;
        }
        foreach (var player in crawlsBy.Keys.Union(stretchesBy.Keys).OrderBy(p => p, StringComparer.Ordinal))
        {
            var mine = crawlsBy.GetValueOrDefault(player) ?? [];
            var visits = mine.Select(c => Note(Visit(c, out var why), why)).ToList();
            var huntSegs = (stretchesBy.GetValueOrDefault(player) ?? []).Select(s => Note(Hunt(s, mine, out var why), why)).ToList();
            var kept = visits.OfType<LevelingSegment>().ToList();
            var keptHunts = huntSegs.OfType<LevelingSegment>().ToList();
            leftOut += visits.Count - kept.Count + huntSegs.Count - keptHunts.Count;
            used += kept.Count;
            hunts += keptHunts.Count;
            var segs = kept.Concat(keptHunts).ToList();
            if (stats != null)
            {
                // Kept, but the Ask math itself skips them (as it does in the app).
                stats.Count("under 3 active minutes", segs.Count(s => s.ActiveMinutes < 3));
                stats.Count("below a zone's gate", segs.Count(s => s.ActiveMinutes >= 3 && s.LevelStart is { } ls && !ZoneGates.Open(s.Place, ls)));
            }
            var mineAll = PlayerAsk.From(segs, now);
            stats?.Count("no rate", mineAll.Ask.NoRate);
            all.Add(mineAll);
            solo.Add(PlayerAsk.From(LevelingSegment.Filter(segs, null, "solo"), now));
            group.Add(PlayerAsk.From(LevelingSegment.Filter(segs, null, "group"), now));
        }
        CommunityTables Tables(List<PlayerAsk> perPlayer, CommunityStats? s = null) =>
            new(Places(perPlayer, rules, s), Bands(perPlayer, rules), Difficulties(perPlayer, rules));
        var t = Tables(all, stats);
        return new CommunityFile(Schema, now, all.Count, used, leftOut, rules, t.Places, t.Bands, t.Difficulties, Tables(solo), Tables(group), hunts);
    }

    private static bool Enough(int players, int crawls, CommunityRules r) => players >= r.MinPlayers && crawls >= r.MinCrawls;

    /// <summary>
    /// One player's Ask math three times over the same play: as the app has it (loot at <see cref="FragmentValue"/>), on
    /// motes alone, and on fragments alone at 1×. The math is a weighted sum of each crawl's loot, so any fragment value v
    /// gives motes + v × fragments: that's what lets each app apply its own.
    /// </summary>
    private sealed record PlayerAsk(AskData Ask, AskData Motes, AskData Fragments)
    {
        public static PlayerAsk From(IEnumerable<LevelingSegment> play, DateTime now)
        {
            var segs = play.ToList();
            AskData Run(IEnumerable<LevelingSegment> s) => AskData.From(s.ToList(), RouteAnalysis.FarmPatch, now, minDifficulties: 1, onlyWithRate: true);
            return new(Run(segs),
                Run(segs.Select(s => s with { T5 = s.Loot?.MoteT5 ?? s.T5, HighTierT5 = s.Loot?.MoteHighTierT5 ?? s.HighTierT5 })),
                Run(segs.Select(s => s with { T5 = s.Loot?.FragmentT5 ?? 0, HighTierT5 = s.Loot?.FragmentHighTierT5 ?? 0 })));
        }

        public PlaceLevels? MotePlace(string place) => Motes.Places.FirstOrDefault(p => p.Place == place);
        public PlaceLevels? FragmentPlace(string place) => Fragments.Places.FirstOrDefault(p => p.Place == place);
    }

    private static List<CommunityPlace> Places(List<PlayerAsk> perPlayer, CommunityRules r, CommunityStats? stats = null)
    {
        var list = new List<CommunityPlace>();
        foreach (var g in perPlayer.SelectMany(d => d.Ask.Places.Select(p => (P: p, M: d.MotePlace(p.Place), F: d.FragmentPlace(p.Place))))
                     .GroupBy(x => x.P.Place).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var parts = g.ToList();
            var ps = parts.Select(x => x.P).ToList();
            double[] moteT5 = new double[49], fragT5 = new double[49], moteHigh = new double[49], fragHigh = new double[49];
            var minutes = new double?[49];
            var t5 = new double[49];
            var crawls = new int[49];
            var players = new int[49];
            var last = new DateTime?[49];
            double[] hours = new double[49], high = new double[49], frag = new double[49], deaths = new double[49], kills = new double[49], dps = new double[49];
            for (int i = 0; i < 49; i++)
            {
                var haveParts = parts.Where(x => x.P.Minutes[i] != null).ToList();
                var have = haveParts.Select(x => x.P).ToList();
                players[i] = have.Count;
                crawls[i] = have.Sum(p => p.Crawls[i]);
                last[i] = have.Select(p => p.Last[i]).Max();
                if (stats != null && have.Count > 0)
                {
                    stats.CellsWithPlay++;
                    if (have.Count == 1) stats.CellsOnePlayer++;
                }
                if (!Enough(players[i], crawls[i], r)) continue;
                if (stats != null) stats.CellsPublished++;
                var w = CappedWeights(have.Select(p => (double)p.Crawls[i]).ToArray(), r.MaxShare);
                minutes[i] = Math.Round(Mean(have.Select(p => p.Minutes[i]!.Value), w), 1);
                t5[i] = Math.Round(Mean(have.Select(p => p.T5PerHour[i]), w), 1);
                moteT5[i] = Math.Round(Mean(haveParts.Select(x => x.M?.T5PerHour[i] ?? 0), w), 2);
                fragT5[i] = Math.Round(Mean(haveParts.Select(x => x.F?.T5PerHour[i] ?? 0), w), 2);
                moteHigh[i] = Math.Round(Mean(haveParts.Select(x => x.M?.Extras?.HighTierPerHour[i] ?? 0), w), 2);
                fragHigh[i] = Math.Round(Mean(haveParts.Select(x => x.F?.Extras?.HighTierPerHour[i] ?? 0), w), 2);
                if (have.Any(p => p.Extras == null)) continue;
                hours[i] = Math.Round(have.Sum(p => p.Extras!.Hours[i]), 2);
                high[i] = Math.Round(Mean(have.Select(p => p.Extras!.HighTierPerHour[i]), w), 2);
                frag[i] = Math.Round(Mean(have.Select(p => p.Extras!.FragmentsPerHour[i]), w), 2);
                deaths[i] = Math.Round(Mean(have.Select(p => p.Extras!.DeathsPerHour[i]), w), 2);
                kills[i] = Math.Round(Mean(have.Select(p => p.Extras!.KillsPerMinute[i]), w), 2);
                // Damage only from players whose damage is known there (none is kept outside instances).
                var dw = have.Select((p, k) => p.Extras!.Dps[i] > 0 ? w[k] : 0).ToArray();
                dps[i] = Math.Round(Mean(have.Select(p => p.Extras!.Dps[i]), dw));
            }
            if (minutes.Any(m => m != null))
                list.Add(new CommunityPlace(g.Key, minutes, t5, crawls, players, last, hours, high, frag, deaths, kills, dps, moteT5, fragT5, moteHigh, fragHigh));
        }
        return list;
    }

    private static List<CommunityBand> Bands(List<PlayerAsk> perPlayer, CommunityRules r)
    {
        static (string, string, bool) Key(BandStat b) => (b.Band, b.Place, b.SincePatch);
        return perPlayer.SelectMany(d => d.Ask.Bands.Select(b => (B: b,
                M: d.Motes.Bands.FirstOrDefault(x => Key(x) == Key(b)), F: d.Fragments.Bands.FirstOrDefault(x => Key(x) == Key(b)))))
            .GroupBy(x => Key(x.B))
            .Where(g => Enough(g.Count(), g.Sum(x => x.B.Crawls), r))
            .Select(g =>
            {
                var parts = g.ToList();
                var bs = parts.Select(x => x.B).ToList();
                var w = CappedWeights(bs.Select(b => (double)b.Crawls).ToArray(), r.MaxShare);
                double M(Func<BandStat, double> f) => Math.Round(Mean(bs.Select(f), w), 2);
                double P(Func<(BandStat B, BandStat? M, BandStat? F), double> f) => Math.Round(Mean(parts.Select(f), w), 2);
                var dps = bs.Select((b, i) => (b.Dps, W: w[i])).Where(x => x.Dps > 0).ToList();
                return new CommunityBand(g.Key.Item1, g.Key.Item2, g.Key.Item3, bs.Sum(b => b.Crawls), bs.Count, Math.Round(bs.Sum(b => b.Hours), 2),
                    M(b => b.LevelsPerHour), M(b => b.T5PerHour), M(b => b.HighTierPerHour), M(b => b.FragmentsPerHour), M(b => b.DeathsPerHour),
                    M(b => b.KillsPerMinute), dps.Count > 0 ? Math.Round(Mean(dps.Select(x => x.Dps), dps.Select(x => x.W).ToArray())) : 0,
                    bs.Max(b => b.Last),
                    P(x => x.M?.T5PerHour ?? 0), P(x => x.F?.T5PerHour ?? 0), P(x => x.M?.HighTierPerHour ?? 0), P(x => x.F?.HighTierPerHour ?? 0));
            })
            .OrderBy(b => Array.IndexOf(AskData.BandNames, b.Band)).ThenBy(b => b.Place, StringComparer.Ordinal).ThenBy(b => b.SincePatch).ToList();
    }

    private static List<CommunityCompare> Difficulties(List<PlayerAsk> perPlayer, CommunityRules r)
    {
        static DifficultyColumn? Col(AskData d, DifficultyCompare c, int difficulty) =>
            d.Difficulties.FirstOrDefault(x => x.Zone == c.Zone && x.From == c.From)?.Columns.FirstOrDefault(x => x.Difficulty == difficulty);
        return perPlayer.SelectMany(d => d.Ask.Difficulties.Select(c => (C: c, D: d))).GroupBy(x => (x.C.Zone, x.C.From))
            .Select(g => new CommunityCompare(g.Key.Zone, g.Key.From, g.First().C.To,
                g.SelectMany(x => x.C.Columns.Select(col => (Col: col, M: Col(x.D.Motes, x.C, col.Difficulty), F: Col(x.D.Fragments, x.C, col.Difficulty))))
                    .GroupBy(x => x.Col.Difficulty)
                    .Where(x => Enough(x.Count(), x.Sum(c => c.Col.Crawls), r))
                    .Select(x =>
                    {
                        var parts = x.ToList();
                        var cs = parts.Select(p => p.Col).ToList();
                        var w = CappedWeights(cs.Select(c => (double)c.Crawls).ToArray(), r.MaxShare);
                        double M(Func<DifficultyColumn, double> f) => Math.Round(Mean(cs.Select(f), w), 2);
                        return new CommunityColumn(x.Key, cs.Sum(c => c.Crawls), cs.Count, M(c => c.LevelsPerHour), M(c => c.T5PerHour),
                            M(c => c.DeathsPerHour), M(c => c.KillsPerMinute), cs.Max(c => c.Last),
                            Math.Round(Mean(parts.Select(p => p.M?.T5PerHour ?? 0), w), 2), Math.Round(Mean(parts.Select(p => p.F?.T5PerHour ?? 0), w), 2));
                    })
                    .OrderBy(c => c.Difficulty).ToList()))
            .Where(c => c.Columns.Count >= 2)
            .OrderBy(c => c.From).ThenBy(c => c.Zone, StringComparer.Ordinal).ToList();
    }

    private static double Mean(IEnumerable<double> values, double[] w)
    {
        double s = 0, sw = 0;
        int i = 0;
        foreach (var v in values) { s += v * w[i]; sw += w[i]; i++; }
        return sw > 0 ? s / sw : 0;
    }

    /// <summary>
    /// Weights in proportion to each player's crawls, but no player above <paramref name="maxShare"/> of the total (or an
    /// equal share, when there are too few players for that). Capped players are lowered until the cap holds.
    /// </summary>
    public static double[] CappedWeights(double[] crawls, double maxShare)
    {
        int n = crawls.Length;
        if (n == 0) return [];
        double s = Math.Max(maxShare, 1.0 / n);
        var capped = new bool[n];
        while (true)
        {
            int k = capped.Count(c => c);
            double free = Enumerable.Range(0, n).Where(i => !capped[i]).Sum(i => crawls[i]);
            double denom = 1 - k * s;
            if (k == n || denom <= 1e-9 || free <= 0) return Enumerable.Repeat(1.0, n).ToArray();
            double total = free / denom;
            var over = Enumerable.Range(0, n).Where(i => !capped[i] && crawls[i] > s * total + 1e-9).ToList();
            if (over.Count == 0)
                return Enumerable.Range(0, n).Select(i => capped[i] ? s * total : crawls[i]).ToArray();
            foreach (var i in over) capped[i] = true;
        }
    }
}
