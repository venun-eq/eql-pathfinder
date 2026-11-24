using System.Text.Json;
using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Community;

/// <summary>
/// One slice of the community numbers as the Ask math takes it: the same <see cref="AskData"/> one player's logs give, so
/// every question runs on it unchanged, plus how many players stand behind each number.
/// </summary>
public sealed class CommunityView
{
    public AskData Ask { get; }
    private readonly Dictionary<string, int[]> _placePlayers;
    private readonly Dictionary<(string Band, string Place, bool SincePatch), int> _bandPlayers;
    private readonly Dictionary<(string Zone, int From, int Difficulty), int> _diffPlayers;

    /// <param name="fragmentValue">
    /// Local fragment value setting. When the community file has split mote/fragment fields, the view combines them
    /// using this value; legacy files keep their pre-combined <see cref="CommunityBuilder.FragmentValue"/> totals.
    /// </param>
    internal CommunityView(CommunityTables t, int visits, double fragmentValue = CommunityBuilder.FragmentValue)
    {
        double V(double? motes, double? frags, double combined) => motes is { } m && frags is { } f ? Math.Round(m + fragmentValue * f, 2) : combined;
        double[] Arr(double[]? motes, double[]? frags, double[] combined) =>
            motes?.Length == combined.Length && frags?.Length == combined.Length ? combined.Select((c, i) => V(motes[i], frags[i], c)).ToArray() : combined;
        Ask = new AskData
        {
            Places = t.Places.Select(p => new PlaceLevels(p.Place, p.Minutes, Arr(p.MoteT5PerHour, p.FragmentT5PerHour, p.T5PerHour), p.Crawls, p.Last)
            {
                Extras = p is { Hours: { } h, HighTierPerHour: { } hi, FragmentsPerHour: { } fr, DeathsPerHour: { } de, KillsPerMinute: { } ki, Dps: { } dps }
                    ? new LevelExtras(h, Arr(p.MoteHighTierPerHour, p.FragmentHighTierPerHour, hi), fr, de, ki, dps) : null,
            }).ToList(),
            Published = true,
            Bands = t.Bands.Select(b => new BandStat(b.Band, b.Place, b.SincePatch, b.Crawls, b.Hours, b.LevelsPerHour,
                V(b.MoteT5PerHour, b.FragmentT5PerHour, b.T5PerHour), V(b.MoteHighTierPerHour, b.FragmentHighTierPerHour, b.HighTierPerHour),
                b.FragmentsPerHour, b.DeathsPerHour, b.KillsPerMinute, b.Dps, b.Last)).ToList(),
            Difficulties = t.Difficulties.Select(c => new DifficultyCompare(c.Zone, c.From, c.To,
                c.Columns.Select(x => new DifficultyColumn(x.Difficulty, x.Crawls, x.LevelsPerHour, V(x.MoteT5PerHour, x.FragmentT5PerHour, x.T5PerHour),
                    x.DeathsPerHour, x.KillsPerMinute, x.Last)).ToList())).ToList(),
            Visits = visits,
        };
        _placePlayers = t.Places.GroupBy(p => p.Place).ToDictionary(g => g.Key, g => g.First().Players);
        _bandPlayers = t.Bands.GroupBy(b => (b.Band, b.Place, b.SincePatch)).ToDictionary(g => g.Key, g => g.First().Players);
        _diffPlayers = t.Difficulties.SelectMany(c => c.Columns.Select(x => (Key: (c.Zone, c.From, x.Difficulty), x.Players)))
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Players);
    }

    /// <summary>The fewest players behind any level from <paramref name="from"/> up to (not including) <paramref name="to"/> at a place; 0 when one is missing.</summary>
    public int PlayersAt(string place, int from, int to)
    {
        if (!_placePlayers.TryGetValue(place, out var p) || to <= from) return 0;
        int min = int.MaxValue;
        for (int L = from; L < to; L++) min = Math.Min(min, L is >= 1 and <= 49 ? p[L - 1] : 0);
        return min == int.MaxValue ? 0 : min;
    }

    public int PlayersFor(BandStat b) => _bandPlayers.GetValueOrDefault((b.Band, b.Place, b.SincePatch));

    /// <summary>The most players behind any level around <paramref name="level"/> at a place (its <see cref="AskData.Near"/> row).</summary>
    public int PlayersNear(string place, int level)
    {
        if (!_placePlayers.TryGetValue(place, out var p)) return 0;
        int most = 0;
        for (int L = Math.Max(1, level - AskData.WindowLevels); L <= Math.Min(49, level + AskData.WindowLevels); L++) most = Math.Max(most, p[L - 1]);
        return most;
    }

    /// <summary>Players behind a row from <see cref="Analysis.Ask.RowsAt"/>: around a level below the cap, the band's at it.</summary>
    public int PlayersFor(BandStat b, int level) => level < 50 ? PlayersNear(b.Place, level) : PlayersFor(b);

    public int PlayersFor(DifficultyCompare c, int difficulty) => _diffPlayers.GetValueOrDefault((c.Zone, c.From, difficulty));

    /// <summary>Your own shared play is taken out of these numbers (<see cref="Without"/>).</summary>
    public bool LeftYouOut { get; private init; }

    /// <summary>Places with a number that rested on you and one other player: taken away for you, since what's left is one player's.</summary>
    public IReadOnlyCollection<string> YouAndOne { get; private init; } = [];

    private CommunityView(AskData ask, Dictionary<string, int[]> places, Dictionary<(string, string, bool), int> bands,
        Dictionary<(string, int, int), int> diffs)
    {
        Ask = ask;
        _placePlayers = places;
        _bandPlayers = bands;
        _diffPlayers = diffs;
    }

    /// <summary>
    /// Community tables with the local player's own play subtracted out so blending with local history does not
    /// double-count them. Up to 1 ÷ MaxShare players count equally in <see cref="CommunityBuilder.CappedWeights"/>, so your
    /// weight is 1/n; past that it's your crawls' share, capped. A number is put back to the others' alone wherever your
    /// <paramref name="mine"/> has the level (the job's own test for counting a player), and goes when that leaves fewer
    /// players or visits than the file publishes with: one other player's play is never shown on its own.
    /// </summary>
    /// <param name="mine">Your tables as the community job would build them (the same slice, as of the file's time).</param>
    internal CommunityView Without(AskData mine, CommunityRules r)
    {
        var dropped = new SortedSet<string>(StringComparer.Ordinal);
        double Weight(int n, int myCrawls, int crawls) => n * r.MaxShare <= 1 ? 1.0 / n : Math.Min(r.MaxShare, crawls > 0 ? (double)myCrawls / crawls : 0);
        static double Out(double pool, double me, double w) => w >= 1 ? 0 : (pool - w * me) / (1 - w);
        static double Rate(double pool, double me, double w) => Math.Max(0, Math.Round(Out(pool, me, w), 2));
        bool Enough(int n, int crawls) => n >= r.MinPlayers && crawls >= r.MinCrawls;

        var myPlaces = mine.Places.GroupBy(p => p.Place).ToDictionary(g => g.Key, g => g.First());
        var placePlayers = new Dictionary<string, int[]>();
        var places = new List<PlaceLevels>();
        foreach (var p in Ask.Places)
        {
            var players = (int[])(_placePlayers.GetValueOrDefault(p.Place) ?? new int[49]).Clone();
            placePlayers[p.Place] = players;
            if (!myPlaces.TryGetValue(p.Place, out var me)) { places.Add(p); continue; }
            var minutes = (double?[])p.Minutes.Clone();
            var t5 = (double[])p.T5PerHour.Clone();
            var crawls = (int[])p.Crawls.Clone();
            var x = p.Extras;
            var mx = me.Extras;
            double[]? hours = x?.Hours.ToArray(), high = x?.HighTierPerHour.ToArray(), frag = x?.FragmentsPerHour.ToArray(),
                deaths = x?.DeathsPerHour.ToArray(), kills = x?.KillsPerMinute.ToArray(), dps = x?.Dps.ToArray();
            for (int i = 0; i < 49; i++)
            {
                if (me.Minutes[i] is not { } m || players[i] < 1) continue;
                int n = players[i], c = Math.Max(0, p.Crawls[i] - me.Crawls[i]);
                players[i] = n - 1;
                crawls[i] = c;
                if (p.Minutes[i] is not { } pool) continue;
                double w = Weight(n, me.Crawls[i], p.Crawls[i]), others = Out(pool, m, w);
                if (!Enough(n - 1, c) || others <= 0) { minutes[i] = null; dropped.Add(p.Place); continue; }
                minutes[i] = Math.Round(others, 1);
                t5[i] = Rate(p.T5PerHour[i], me.T5PerHour[i], w);
                if (x == null || mx == null) continue;
                hours![i] = Math.Max(0, Math.Round(x.Hours[i] - mx.Hours[i], 3));
                high![i] = Rate(x.HighTierPerHour[i], mx.HighTierPerHour[i], w);
                frag![i] = Rate(x.FragmentsPerHour[i], mx.FragmentsPerHour[i], w);
                deaths![i] = Rate(x.DeathsPerHour[i], mx.DeathsPerHour[i], w);
                kills![i] = Rate(x.KillsPerMinute[i], mx.KillsPerMinute[i], w);
                if (x.Dps[i] > 0 && mx.Dps[i] > 0) dps![i] = Math.Max(0, Math.Round(Out(x.Dps[i], mx.Dps[i], w)));
            }
            if (minutes.Any(v => v != null))
                places.Add(new PlaceLevels(p.Place, minutes, t5, crawls, p.Last)
                {
                    Extras = x == null ? null : new LevelExtras(hours!, high!, frag!, deaths!, kills!, dps!),
                });
        }

        var myBands = mine.Bands.GroupBy(b => (b.Band, b.Place, b.SincePatch)).ToDictionary(g => g.Key, g => g.First());
        var bandPlayers = new Dictionary<(string, string, bool), int>();
        var bands = new List<BandStat>();
        foreach (var b in Ask.Bands)
        {
            var key = (b.Band, b.Place, b.SincePatch);
            int n = _bandPlayers.GetValueOrDefault(key);
            if (!myBands.TryGetValue(key, out var me) || n < 1) { bands.Add(b); bandPlayers[key] = n; continue; }
            int c = Math.Max(0, b.Crawls - me.Crawls);
            double w = Weight(n, me.Crawls, b.Crawls);
            if (!Enough(n - 1, c) || Out(b.LevelsPerHour, me.LevelsPerHour, w) <= 0) { dropped.Add(b.Place); continue; }
            bandPlayers[key] = n - 1;
            bands.Add(b with
            {
                Crawls = c,
                Hours = Math.Max(0, Math.Round(b.Hours - me.Hours, 2)),
                LevelsPerHour = Rate(b.LevelsPerHour, me.LevelsPerHour, w),
                T5PerHour = Rate(b.T5PerHour, me.T5PerHour, w),
                HighTierPerHour = Rate(b.HighTierPerHour, me.HighTierPerHour, w),
                FragmentsPerHour = Rate(b.FragmentsPerHour, me.FragmentsPerHour, w),
                DeathsPerHour = Rate(b.DeathsPerHour, me.DeathsPerHour, w),
                KillsPerMinute = Rate(b.KillsPerMinute, me.KillsPerMinute, w),
                Dps = b.Dps > 0 && me.Dps > 0 ? Math.Max(0, Math.Round(Out(b.Dps, me.Dps, w))) : b.Dps,
            });
        }

        var myDiffs = mine.Difficulties.GroupBy(c => (c.Zone, c.From)).ToDictionary(g => g.Key, g => g.First());
        var diffPlayers = new Dictionary<(string, int, int), int>();
        var diffs = new List<DifficultyCompare>();
        foreach (var d in Ask.Difficulties)
        {
            var me = myDiffs.GetValueOrDefault((d.Zone, d.From));
            var cols = new List<DifficultyColumn>();
            foreach (var x in d.Columns)
            {
                var key = (d.Zone, d.From, x.Difficulty);
                int n = _diffPlayers.GetValueOrDefault(key);
                var mc = me?.Columns.FirstOrDefault(y => y.Difficulty == x.Difficulty);
                if (mc == null || n < 1) { cols.Add(x); diffPlayers[key] = n; continue; }
                int c = Math.Max(0, x.Crawls - mc.Crawls);
                double w = Weight(n, mc.Crawls, x.Crawls);
                if (!Enough(n - 1, c) || Out(x.LevelsPerHour, mc.LevelsPerHour, w) <= 0) { dropped.Add(LevelingSegment.PlaceName(d.Zone, x.Difficulty)); continue; }
                diffPlayers[key] = n - 1;
                cols.Add(x with
                {
                    Crawls = c,
                    LevelsPerHour = Rate(x.LevelsPerHour, mc.LevelsPerHour, w),
                    T5PerHour = Rate(x.T5PerHour, mc.T5PerHour, w),
                    DeathsPerHour = Rate(x.DeathsPerHour, mc.DeathsPerHour, w),
                    KillsPerMinute = Rate(x.KillsPerMinute, mc.KillsPerMinute, w),
                });
            }
            if (cols.Count >= 2) diffs.Add(d with { Columns = cols });
        }

        return new CommunityView(new AskData { Places = places, Bands = bands, Difficulties = diffs, Published = true, Visits = Ask.Visits },
            placePlayers, bandPlayers, diffPlayers)
        { LeftYouOut = true, YouAndOne = dropped };
    }

    /// <summary>
    /// Minutes the players who share take at a place from <paramref name="from"/> to <paramref name="to"/> (rank 0, times
    /// <paramref name="xpMultiplier"/>), or null unless every one of those levels is published.
    /// </summary>
    public double? MinutesAt(string place, int from, int to, double xpMultiplier = 1)
    {
        var p = Ask.Places.FirstOrDefault(x => x.Place == place);
        if (p == null || to <= from) return null;
        double sum = 0;
        for (int L = from; L < to; L++)
        {
            if (p.At(L) is not { } m) return null;
            sum += m;
        }
        return sum * xpMultiplier;
    }
}

/// <summary>
/// community/v1.json as the Ask page reads it (schema 1 or 2). Schema 1 is instances only, solo and grouped together;
/// schema 2 adds open-world places (named "… · open world") and Solo and Group tables. <see cref="For"/> gives the slice
/// for the page's Where and Who with, or null when the file doesn't have it.
/// </summary>
public sealed class CommunityData
{
    private const string OpenWorld = " · open world";
    public CommunityFile File { get; }
    private readonly Dictionary<(string Where, string With, double FragmentValue), CommunityView?> _views = new();
    /// <summary>Your play that's in the file, when you share (<see cref="LeavingOut"/>); null otherwise.</summary>
    private readonly IReadOnlyList<LevelingSegment>? _yours;

    private CommunityData(CommunityFile f, IReadOnlyList<LevelingSegment>? yours = null)
    {
        File = f;
        _yours = yours;
    }

    /// <summary>
    /// The same file as a player who shares should read it: every slice without their own play (<see cref="CommunityView.Without"/>).
    /// <paramref name="yours"/> are the player's Ask visits; those from <paramref name="sharedFrom"/> (what sharing sends) up
    /// to the file's time are the ones in it.
    /// </summary>
    public CommunityData LeavingOut(IEnumerable<LevelingSegment> yours, DateTime sharedFrom) =>
        new(File, yours.Where(s => s.Start >= sharedFrom && s.Start <= File.Generated).ToList());

    /// <summary>Your own play is taken out of every slice (<see cref="LeavingOut"/>).</summary>
    public bool LeavesYouOut => _yours != null;

    /// <summary>The file this app version understands (schema 1 or 2, 49 levels a place), or null.</summary>
    public static CommunityData? Parse(string json)
    {
        try
        {
            var f = JsonSerializer.Deserialize<CommunityFile>(json, CommunityBuilder.Json);
            if (f is not { Schema: 1 or 2 } || f.Places == null || f.Bands == null || f.Difficulties == null) return null;
            if (new[] { f.Places, f.Solo?.Places, f.Group?.Places }.Where(p => p != null).SelectMany(p => p!)
                .Any(p => p.Place == null || p.Minutes?.Length != 49 || p.T5PerHour?.Length != 49 || p.Crawls?.Length != 49
                    || p.Players?.Length != 49 || p.Last?.Length != 49)) return null;
            return new CommunityData(f);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Does the file cover the open world (schema 2), and solo and grouped play apart?</summary>
    public bool HasOpenWorld => File.Schema >= 2;
    public bool HasSoloGroup => File.Solo != null && File.Group != null;

    /// <summary>
    /// Community slice for Where ("all", "instances", "open") and With ("either", "solo", "group"); null if unsupported
    /// by a schema 1 file. Combines split loot using <paramref name="fragmentValue"/>. Thread-safe (shared by UI and tracker).
    /// </summary>
    public CommunityView? For(string? where = "all", string? with = "either", double fragmentValue = CommunityBuilder.FragmentValue)
    {
        lock (_views) return Build(where, with, fragmentValue);
    }

    private CommunityView? Build(string? where, string? with, double fragmentValue)
    {
        where = where is "instances" or "open" ? where : "all";
        with = with is "solo" or "group" ? with : "either";
        if (_views.TryGetValue((where, with, fragmentValue), out var cached)) return cached;
        CommunityView? view = null;
        var t = with switch { "solo" => File.Solo, "group" => File.Group, _ => new CommunityTables(File.Places, File.Bands, File.Difficulties) };
        if (t != null && !(where == "open" && !HasOpenWorld))
        {
            bool Keep(string place) => where switch { "open" => place.EndsWith(OpenWorld), "instances" => !place.EndsWith(OpenWorld), _ => true };
            view = new CommunityView(new CommunityTables(t.Places.Where(p => Keep(p.Place)).ToList(), t.Bands.Where(b => Keep(b.Place)).ToList(),
                where == "open" ? [] : t.Difficulties), File.Crawls + File.Stretches, fragmentValue);
            // Built as the community job builds each player's tables (one difficulty is enough), as of the file's time.
            if (_yours != null)
                view = view.Without(AskData.From(LevelingSegment.Filter(_yours, where, with), RouteAnalysis.FarmPatch, File.Generated,
                    minDifficulties: 1, onlyWithRate: true), File.Rules);
        }
        return _views[(where, with, fragmentValue)] = view;
    }

    // All play, as schema 1 had it (tests and callers that don't narrow).
    public AskData Ask => For()!.Ask;
    public int PlayersAt(string place, int from, int to) => For()!.PlayersAt(place, from, to);
    public int PlayersFor(BandStat b) => For()!.PlayersFor(b);
    public int PlayersFor(DifficultyCompare c, int difficulty) => For()!.PlayersFor(c, difficulty);
    public double? MinutesAt(string place, int from, int to, double xpMultiplier = 1) => For()!.MinutesAt(place, from, to, xpMultiplier);
}
