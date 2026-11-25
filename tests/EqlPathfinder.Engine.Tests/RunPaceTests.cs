using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>"Am I going at a proper speed?" like for like (Analysis/RunPace.cs, docs/pace.md).</summary>
public class RunPaceTests
{
    private static readonly DateTime Patch = new(2026, 11, 10, 12, 0, 0), T0 = new(2026, 11, 16, 18, 0, 0);
    private static int _n;

    /// <summary>A crawl: <paramref name="kills"/> in <paramref name="minutes"/>, <paramref name="killLevels"/> levels from them.</summary>
    private static LevelingSegment Run(int level, double minutes, int kills, double killLevels = 0.5, int rank = 2, bool grouped = false,
        string place = "Kedge Keep D0", double? levels = null, DateTime? start = null) =>
        new(place, "Kedge Keep", int.Parse(place[^1..]), start ?? T0.AddMinutes(++_n), level, minutes, levels ?? killLevels, 0, 0, 0, kills, 0, 0, rank, "crawl",
            grouped ? 1 : 0)
        { VisitId = $"v{++_n}", KillLevels = killLevels };

    /// <summary>Your usual here: 60 kills in 15 minutes, 4 a minute, the day before any run it judges (a usual is earlier visits).</summary>
    private static List<LevelingSegment> Usual(int runs, int level = 43, bool grouped = false) =>
        Enumerable.Range(0, runs).Select(i => Run(level + i % 3 - 1, 15, 60, grouped: grouped, start: T0.AddDays(-1).AddMinutes(++_n))).ToList();

    /// <summary>The user, 11-18: a visit's usual is made of earlier visits only, so its verdict is fixed once it's over.</summary>
    [Fact]
    public void AVisitIsJudgedOnlyAgainstTheVisitsBeforeIt()
    {
        // Five crawls at 4 kills a minute, then this one at 4.8 (fast), then five at 6 a minute that hadn't happened yet.
        var earlier = Usual(5);
        var run = Run(43, 12, 58);
        var later = Enumerable.Range(0, 5).Select(i => Run(42 + i % 3, 10, 60)).ToList();
        var p = RunPace.For(run, [.. earlier, run, .. later]);
        Assert.Equal("fast", p.Verdict);
        Assert.Equal(4, p.UsualKillsPerMinute!.Value, 3);
        Assert.Equal(5, p.Runs);
        // The first visits at a place had nothing to be judged by.
        Assert.Equal("none", RunPace.For(earlier[2], [.. earlier, run, .. later]).Verdict);
        Assert.Equal(3, RunPace.For(earlier[3], [.. earlier, run, .. later]).Runs);
    }

    [Fact]
    public void UnderThreeRunsNearTheLevelThereIsNoVerdict()
    {
        var p = RunPace.For(Run(43, 12, 36), Usual(2));
        Assert.Equal("none", p.Verdict);
        Assert.Equal(2, p.Runs);
        Assert.Null(p.KillSpeed);
    }

    [Fact]
    public void OnPaceMeansWithinOneRunsNormalSwing()
    {
        var usual = Usual(5);
        Assert.Equal(0.15, RunPace.For(Run(43, 12, 42), usual).Band);
        Assert.Equal("pace", RunPace.For(Run(43, 12, 42), usual).Verdict);   // 3.5 a minute: 88%, inside ±15%
        var slow = RunPace.For(Run(43, 12, 36), usual);                      // 3.0 a minute
        Assert.Equal("slow", slow.Verdict);
        Assert.Equal(0.75, slow.KillSpeed!.Value, 3);
        Assert.Equal("fast", RunPace.For(Run(43, 12, 58), usual).Verdict);   // 4.8 a minute
    }

    [Fact]
    public void AUsualFromThreeOrFourRunsHasAWiderBand()
    {
        var p = RunPace.For(Run(43, 12, 41), Usual(3));   // 3.4 a minute: 85%
        Assert.Equal(0.18, p.Band);
        Assert.Equal("pace", p.Verdict);
    }

    [Fact]
    public void GroupRunsDontMakeASoloUsualNorDoOtherDifficultiesOrFarLevels()
    {
        var others = Usual(3, grouped: true)
            .Concat(Enumerable.Range(0, 3).Select(_ => Run(43, 15, 60, place: "Kedge Keep D2")))
            .Concat(Enumerable.Range(0, 3).Select(_ => Run(37, 15, 60))).ToList();
        Assert.Equal("none", RunPace.For(Run(43, 12, 36), others).Verdict);
    }

    [Fact]
    public void TheFirstMinutesAreEarlyNotSlow()
    {
        var p = RunPace.For(Run(43, 4, 6), Usual(5));
        Assert.Equal("early", p.Verdict);
        Assert.Equal(4, p.UsualKillsPerMinute!.Value, 3);
    }

    [Fact]
    public void XpPerKillIsReadAtTheRunsOwnLevelAndPenalty()
    {
        // Earlier runs at +10% XP, each level giving 10% less a kill: 2.00%, 1.79%, 1.61%, 1.45% at 40–43.
        var history = new[] { (40, 50), (41, 56), (42, 62), (43, 69) }.Select(x => Run(x.Item1, 15, x.Item2, killLevels: 1, rank: 1)).ToList();
        var run = Run(44, 10, 40, killLevels: 0.5, rank: 2);
        var p = RunPace.For(run, history);
        // Along the place's slope to 44.25, then +20% XP instead of +10%: about 1.23%, not the 1.7% a 2-level window gives.
        Assert.InRange(p.ExpectedXpPerKill!.Value, 1.20, 1.26);
        Assert.Equal(1.25, p.XpPerKill!.Value, 3);
        Assert.Equal("normal", p.Xp);
    }

    /// <summary>
    /// Report #15 (11-20): DRU/BRD/NEC at +20% XP, Kedge Keep D0 33 to 35, 2.93% a kill, read "low XP per kill". Every earlier
    /// run there was at +20% too, and the one played at 33 gave 3.31%: 11% more, inside ±15%. The line through all 17 runs,
    /// 31 to 49, reads 3.55% at 34, because XP per kill falls slower at the low end. The runs near the level set the height.
    /// </summary>
    [Fact]
    public void XpPerKillIsJudgedAgainstTheRunsNearItsLevel()
    {
        // Venun's Kedge Keep D0 runs before it, as played: start level, kills, kill levels, levels gained (with completion).
        (int, int, double, double)[] played = [(31, 60, 2.254, 2.68), (33, 59, 1.953, 2.20), (35, 57, 1.506, 1.80), (37, 56, 1.236, 1.48),
            (41, 55, 0.846, 0.95), (42, 56, 0.846, 1.06), (43, 55, 0.581, 0.71), (43, 54, 0.579, 0.71), (44, 57, 0.568, 0.64),
            (45, 56, 0.479, 0.56), (45, 55, 0.448, 0.56), (46, 59, 0.409, 0.52), (46, 58, 0.407, 0.49), (49, 62, 0.222, 0.41),
            (41, 59, 0.814, 0.97), (42, 61, 0.748, 0.91), (44, 57, 0.573, 0.68)];
        var history = played.Select(r => Run(r.Item1, 15, r.Item2, killLevels: r.Item3, levels: r.Item4, start: T0.AddDays(-1).AddMinutes(++_n))).ToList();

        var p = RunPace.For(Run(33, 26, 66, killLevels: 1.937, levels: 1.94), history);
        Assert.Equal(2.935, p.XpPerKill!.Value, 3);
        Assert.InRange(p.ExpectedXpPerKill!.Value, 3.2, 3.35);   // the runs at 31, 33 and 35, each moved along the place's slope to 34
        Assert.Equal("normal", p.Xp);

        // A run that really gives less there still reads low: 2.5% a kill.
        Assert.Equal("low", RunPace.For(Run(33, 26, 66, killLevels: 1.65, levels: 1.94), history).Xp);
    }

    [Fact]
    public void AFewRunsAtOneLevelUseTheTypicalSlope()
    {
        var history = Enumerable.Range(0, 3).Select(_ => Run(43, 15, 50, killLevels: 1)).ToList();   // 2% a kill at 43.5
        var p = RunPace.For(Run(45, 10, 40, killLevels: 0.4), history);                         // read at 45.2
        Assert.Equal(2 * Math.Pow(0.92, 1.7), p.ExpectedXpPerKill!.Value, 3);
    }

    [Fact]
    public void NoExpectedXpFarFromWhereThePlaceWasPlayed()
    {
        var history = Enumerable.Range(0, 4).Select(i => Run(30 + i, 15, 50, killLevels: 1)).ToList();
        Assert.Null(RunPace.For(Run(40, 10, 40), history).ExpectedXpPerKill);
    }

    private static LevelingSegment Visit(string place, int level, double minutesPerLevel) =>
        new(place, "Kedge Keep", int.Parse(place[^1..]), T0.AddMinutes(++_n), level, minutesPerLevel, 1, 0, 0, 0, 50, 0, 100, 1);

    private static AskData Places(double d0At44, double d2At44) => AskData.From(
        Enumerable.Range(0, 3).SelectMany(_ => new[]
        {
            Visit("Kedge Keep D1", 41, 10), Visit("Kedge Keep D1", 44, d0At44),
            Visit("Befallen D1", 41, 12), Visit("Befallen D1", 44, d2At44),
        }).ToList(), Patch, T0.AddHours(1));

    [Fact]
    public void APlaceThatWasBestAndIsNowClearlyPassedIsFading()
    {
        var c = PlaceCheck.At(Places(20, 12), "Kedge Keep D1", 44, 1.2, (_, _) => true)!;
        Assert.Equal("fading", c.Kind);
        Assert.Equal("Befallen D1", c.Better);
        // Those runs needed +10% XP; asked at +20%, each place is re-figured for it.
        Assert.InRange(c.BetterLevelsPerHour, 4.55, 4.62);   // 60 / (12 / 1.1 × 1.2) = 4.58, Ask rounds its minutes
        Assert.InRange(c.LevelsPerHour, 2.72, 2.78);         // 60 / (20 / 1.1 × 1.2) = 2.75
    }

    [Fact]
    public void AGapInsideTheNoiseIsNotFlagged()
    {
        Assert.Null(PlaceCheck.At(Places(20, 18), "Kedge Keep D1", 44, 1.2, (_, _) => true));   // 11% faster on 3 runs each: within the noise
        Assert.Null(PlaceCheck.At(Places(20, 12), "Befallen D1", 44, 1.2, (_, _) => true));   // the faster place has nothing to beat it
    }

    [Fact]
    public void APlaceThatWasNeverTheBestIsASlowerPlace()
    {
        var c = PlaceCheck.At(Places(20, 12), "Befallen D1", 41, 1.2, (_, _) => true);
        Assert.Null(c);   // at 41 D2 (12) loses to D0 (10) by 18%: inside the noise on 3 runs each
        var far = AskData.From(Enumerable.Range(0, 4).SelectMany(_ => new[] { Visit("Kedge Keep D1", 41, 8), Visit("Befallen D1", 41, 14),
            Visit("Kedge Keep D1", 38, 8), Visit("Befallen D1", 38, 14) }).ToList(), Patch, T0.AddHours(1));
        Assert.Equal("slower", PlaceCheck.At(far, "Befallen D1", 41, 1.2, (_, _) => true)!.Kind);
    }

    /// <summary>The shared pool at Befallen D4 around 10: each player's crawls are 20 kills and 1 level in 30 minutes, at rank 0.</summary>
    private static CommunityView Pool(int players)
    {
        SharedCrawl Crawl() => new("c" + Guid.NewGuid().ToString("N")[..8], "2026-11-14", 1, "crawl", "Befallen", 4, "WIZ/ENC/CLR", true, "who",
            "Ogre", 0, 10, 11, 1800, true, false, null,
            Enumerable.Range(0, 20).Select(i => new CompactRow($"[{i},5,0,8,10]")).ToList(), [], [], [],
            new SharedDamage(360000, 1800, 0, new Dictionary<string, long>(), new Dictionary<string, long> { ["Fireball"] = 360000 }),
            new Dictionary<string, int>(), new SharedLoot(new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>()),
            End: 0);
        var crawls = Enumerable.Range(0, players).SelectMany(p => Enumerable.Range(0, 2).Select(_ => new PlayerCrawl("p" + p, Crawl()))).ToList();
        var file = CommunityBuilder.Build(crawls, new CommunityRules(MinPlayers: 2, MinCrawls: 3), new DateTime(2026, 11, 16, 12, 0, 0));
        return CommunityData.Parse(JsonSerializer.Serialize(file, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }))!.For(null, null)!;
    }

    [Fact]
    public void WithNoUsualOtherPlayersShowAsContextOnceThreeShare()
    {
        var o = OthersHere.At(Pool(3), "Befallen D4", 10, 1.2)!;
        Assert.Equal(3, o.Players);
        Assert.Equal(20 / 30.0, o.KillsPerMinute, 2);
        Assert.Equal(60 / (30 * 1.2), o.LevelsPerHour, 1);   // a level in 30 minutes at rank 0, re-figured for +20% XP
        Assert.Null(OthersHere.At(Pool(2), "Befallen D4", 10, 1.2));   // two players aren't enough to show
    }
}
