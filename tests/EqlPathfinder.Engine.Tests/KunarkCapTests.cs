using System.Globalization;
using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// kunark-cap: the cap is read from the list of expansions by the day the play happened (docs/specs/kunark-cap.md,
/// docs/engineering.md rule 13). Classic is 50; Kunark is 55 from 2026-12-08. Play keeps the cap of its day.
/// </summary>
public partial class KunarkCapTests
{
    static readonly DateTime Kunark = new(2026, 12, 8);
    static readonly DateTime Before = new(2026, 12, 5), After = new(2026, 12, 9);

    static string L(DateTime t, string body) =>
        $"[{t.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture)}] {body}";

    static (PathfinderEngine e, TrackerTests.Sink s) Run(TrackerOptions o, IEnumerable<string> lines)
    {
        var s = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", o, s);
        foreach (var l in lines) e.ProcessLine(l);
        e.Flush();
        return (e, s);
    }

    /// <summary>A crawl on <paramref name="day"/> at <paramref name="hour"/>: in, 8 kills at 2% each, out an hour later.</summary>
    static IEnumerable<string> Crawl(DateTime day, int hour = 10)
    {
        var t = day.AddHours(hour);
        yield return L(t, "You have entered Nektropos Castle 4 (Refined).");
        for (int i = 1; i <= 8; i++)
        {
            yield return L(t.AddMinutes(i), "You gain experience! (2.000%)");
            yield return L(t.AddMinutes(i), "You have slain a ghoul!");
        }
        yield return L(t.AddHours(1), "You have entered Nektropos Castle.");
    }

    static IEnumerable<string> Ding(DateTime t, int level) => [L(t, $"You have gained a level! Welcome to level {level}!")];

    [Fact]
    public void TheCapIs50UntilKunarkAnd55FromItsLaunchDay()
    {
        var o = new TrackerOptions();
        Assert.Equal(50, o.CapOn(Kunark.AddSeconds(-1)));
        Assert.Equal(55, o.CapOn(Kunark));
        Assert.Equal(55, o.CapOn(After));
        Assert.Equal(("Classic", 50), (Expansions.All[0].Name, Expansions.All[0].LevelCap));
        Assert.Equal(new Expansion("Kunark", Kunark, 55), Expansions.All[^1]);
    }

    [Fact]
    public void AnotherExpansionThatRaisesTheCapIsOneMoreEntry()
    {
        // A made-up Velious at 60 from 2027-06-01: one more entry, and the tracker follows it with no other change.
        var velious = new DateTime(2027, 6, 1);
        var o = new TrackerOptions
        {
            Expansions = [new("Classic", DateTime.MinValue, 50), new("Kunark", Kunark, 55), new("Velious", velious, 60)],
        };
        Assert.Equal(55, o.CapOn(velious.AddDays(-1)));
        Assert.Equal(60, o.CapOn(velious));
        var (_, s) = Run(o, [.. Ding(velious.AddDays(-2).AddHours(9), 55), .. Crawl(velious.AddDays(-2)), .. Crawl(velious.AddDays(1))]);
        Assert.Equal([8, 0], s.Closed.Select(v => v.CapKills));
    }

    [Fact]
    public void AKillAt50AddsXpOnlyFromKunarksLaunch()
    {
        var o = new TrackerOptions();
        var (_, s) = Run(o, [.. Ding(Before.AddHours(9), 50), .. Crawl(Before), .. Crawl(After)]);
        Assert.Equal(2, s.Closed.Count);
        var (farmed, leveled) = (s.Closed[0], s.Closed[1]);
        Assert.Equal((8, 0.0), (farmed.CapKills, VisitStats.From(farmed, farmed.EndTime, o).LevelsGained));
        Assert.Equal(0, leveled.CapKills);
        Assert.Equal(0.16, VisitStats.From(leveled, leveled.EndTime, o).LevelsGained, 6);
        Assert.Equal([true, false], s.Closed.Select(v => VisitStats.From(v, v.EndTime, o).AtCap));
    }

    [Fact]
    public void ADingPastTheOldCapMeansTheNewCapHasCome()
    {
        // The fallback: the launch came early (or the list is late), and the log already says 51. Kills there add XP.
        var early = new DateTime(2026, 12, 6);
        var (_, s) = Run(new TrackerOptions(), [.. Ding(early.AddHours(9), 50), .. Ding(early.AddHours(9.5), 51), .. Crawl(early)]);
        var v = Assert.Single(s.Closed);
        Assert.Equal(51, v.LevelStart);
        Assert.Equal(0, v.CapKills);
    }

    [Fact]
    public void ReachingFiftyIsTheCapBeforeKunarkAndNotAfter()
    {
        static IEnumerable<string> To50(DateTime day) =>
        [
            L(day.AddHours(9), "You have entered West Karana."),
            L(day.AddHours(9).AddSeconds(10), "[49 PAL/MNK/SHM] Venun (Ogre) ZONE: West Karana"),
            .. Ding(day.AddHours(10), 50),
        ];
        var (before, _) = Run(new TrackerOptions(), To50(Before));
        Assert.Equal(Before.AddHours(10), before.State.Roster.Classes["PAL"].ReachedCap);

        var (after, _) = Run(new TrackerOptions(), [.. To50(After), .. Enumerable.Range(51, 5).SelectMany(l => Ding(After.AddHours(10 + l - 50), l))]);
        Assert.Equal(After.AddHours(15), after.State.Roster.Classes["PAL"].ReachedCap);   // at 55, not at 50
    }

    static Visit V(DateTime start, int from, int to) => new()
    {
        Id = $"v{start:MMddHH}",
        Zone = "Befallen 4",
        BaseZone = "Befallen",
        Difficulty = 4,
        Start = start,
        ActiveSeconds = 1800,
        Closed = true,
        Completed = true,
        Trio = "PAL/MNK/SHM",
        TrioConfirmed = true,
        Race = "Froglok",
        LevelStart = from,
        LevelEnd = to,
    };

    static PathfinderEngine Reincarnated(DateTime t)
    {
        var (e, _) = Run(new TrackerOptions(),
        [
            L(t, "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            L(t, "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L(t, "You have gained the ability \"Class Reincarnation: Shaman\" at a cost of 0 ability points."),
        ]);
        return e;
    }

    [Fact]
    public void ARelevelThatReached50BeforeKunarkStaysDoneAndOneAt50AfterItIsStillGoing()
    {
        // Done at 50 on 12-03, then played on after the launch: the relevel stays done at 50.
        var old = Reincarnated(new DateTime(2026, 12, 1, 12, 0, 0));
        var done = Assert.Single(Journeys.Build([V(new(2026, 12, 3, 14, 0, 0), 1, 50), V(After.AddHours(14), 50, 52)], old.State, new TrackerOptions()));
        Assert.Equal((true, 50), (done.Done, done.LevelNow));

        // Reincarnated on launch day and at 50 the day after: five levels to go.
        var fresh = Reincarnated(Kunark.AddHours(12));
        var going = Assert.Single(Journeys.Build([V(After.AddHours(14), 1, 50)], fresh.State, new TrackerOptions()));
        Assert.Equal((false, 50), (going.Done, going.LevelNow));
    }

    [Fact]
    public void PlayAt50AfterKunarkIsLevelingAndLeavesTheFarmRate()
    {
        var o = new TrackerOptions();
        var lines = new List<string>(Ding(Before.AddHours(9), 50));
        foreach (var day in new[] { Before, After })
            foreach (var hour in new[] { 10, 12, 14 }) lines.AddRange(Crawl(day, hour));
        var (_, s) = Run(o, lines);
        Assert.Equal(6, s.Closed.Count);
        var info = RouteAnalysis.FarmRates(s.Closed, o);
        Assert.Equal(3, info.Crawls + info.Unrated);   // the three from 12-05 only
    }

    static SharedCrawl SharedAt50(string day) =>
        new("c" + Guid.NewGuid().ToString("N")[..8], day, 1, "crawl", "Befallen", 4, "WIZ/ENC/CLR", true, "who", "Ogre", 0, 50,
            50, 1800, true, false, null,
            Enumerable.Range(0, 20).Select(i => new CompactRow(FormattableString.Invariant($"[{i * 60},2,0,8,50]"))).ToList(), [], [], [],
            new SharedDamage(1000, 900, 0, new Dictionary<string, long>(), new Dictionary<string, long> { ["Fireball"] = 1000 }), new Dictionary<string, int>(),
            new SharedLoot(new Dictionary<int, int> { [5] = 4 }, new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>()),
            End: 0);

    [Fact]
    public void TheCommunityJobReadsAKillAt50ByTheDayOfTheCrawl()
    {
        // A share carries raw facts and its day; the job applies that day's cap (docs/pace.md).
        Assert.Equal(0, CommunityBuilder.Recompute(SharedAt50("2026-12-05")).KillLevels, 6);
        Assert.True(CommunityBuilder.Recompute(SharedAt50("2026-12-09")).KillLevels > 0);
    }

    [Fact]
    public async Task At50AfterKunarkHomeIsLevelingTowards55AndAskHasNoRouteYet()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-kunark-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            async Task<(string Mode, int Cap, AskAnswer Ask, JsonElement New)> HomeOn(DateTime day)
            {
                var log = Path.Combine(dir, $"eqlog_Venun_oggok_{day:MMdd}.txt");
                var t = day.AddHours(12);
                File.WriteAllLines(log,
                [
                    L(t, "You have entered Nagafen's Lair."),
                    L(t.AddSeconds(10), "[50 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"),
                    .. Crawl(day, 12).Skip(1),
                ]);
                using var store = new PathfinderStore(Path.Combine(dir, $"t{day:MMdd}.db"));
                using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), new SetClock(t.AddMinutes(62)));
                await svc.ImportHistoryAsync();
                svc.Start();
                var snap = await svc.Query(s => s.BuildSnapshot());
                var reinc = JsonSerializer.SerializeToElement(await svc.Query(s => s.ReincarnationData()));
                var ask = await svc.Query(s => s.AskPage(new AskQuery("level", 50, 55, "fast", null, null, null)));
                return (snap.Mode, reinc.GetProperty("levelCap").GetInt32(), ask, reinc.GetProperty("newLevels").Clone());
            }

            var before = await HomeOn(Before);
            Assert.Equal(("farming", 50), (before.Mode, before.Cap));
            Assert.Equal(JsonValueKind.Null, before.New.ValueKind);   // no new levels before the day: the pages read as today

            var after = await HomeOn(After);
            Assert.Equal(("leveling", 55), (after.Mode, after.Cap));
            Assert.Equal(("Kunark", "2026-12-08", 50, 55), (after.New.GetProperty("expansion").GetString(), after.New.GetProperty("day").GetString(), after.New.GetProperty("from").GetInt32(), after.New.GetProperty("to").GetInt32()));
            Assert.Equal((55, 50, 55), (after.Ask.LevelCap, after.Ask.Route.From, after.Ask.Route.To));
            Assert.Empty(after.Ask.Route.Legs);   // nobody has a route past 50 yet: the page says so in the designer's words
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    sealed class SetClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    static IEnumerable<string> OpenWorld(DateTime day, int hour = 16)
    {
        var t = day.AddHours(hour);
        yield return L(t, "You have entered West Karana.");
        for (int i = 1; i <= 6; i++)
        {
            yield return L(t.AddMinutes(i), "You gain experience! (2.000%)");
            yield return L(t.AddMinutes(i), "You have slain a gnoll!");
        }
    }

    [Fact]
    public void SharesOfPlayBeforeKunarkAreTheSameUnitsAfterIt()
    {
        // The same history, built before the launch (nothing after 12-05) and after it (12-09 play added): each unit from before the
        // launch keeps its id, and the kills at 50 on 12-05 are no stretch, as at 50 they were farming at the cap.
        static SharePayload Share(IEnumerable<string> lines)
        {
            var o = new TrackerOptions();
            var (e, s) = Run(o, lines);
            var js = Journeys.Build(s.Closed.ToList(), e.State, o, FieldHunts.From(s.Kills, o));
            return ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", new byte[16], s.Closed.ToList(), js, s.Kills, [], e.State, o);
        }
        var early = new List<string>([.. Ding(Before.AddHours(9), 50), .. Crawl(Before), .. OpenWorld(Before)]);
        var late = Share([.. early, .. Crawl(After), .. OpenWorld(After)]);
        var soon = Share(early);
        Assert.Empty(soon.Stretches);
        Assert.Single(soon.Crawls);
        Assert.Equal(soon.Crawls.Select(c => c.Id), late.Crawls.Take(1).Select(c => c.Id));
        Assert.Equal(2, late.Crawls.Count);
        Assert.Single(late.Stretches);   // the open-world play on 12-09 is leveling, so it is a stretch
        Assert.Equal("2026-12-09", late.Stretches[0].Day);
    }

    sealed class MovingClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(Now, TimeZoneInfo.Local.GetUtcOffset(Now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    [Fact]
    public async Task TheAppLeftOpenOverMidnightTurnsToLeveling()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-kunark-mid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var day = new DateTime(2026, 12, 7);
            var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
            File.WriteAllLines(log,
            [
                L(day.AddHours(12), "You have entered Nagafen's Lair."),
                L(day.AddHours(12).AddSeconds(10), "[50 PAL/MNK/SHM] Venun (Ogre) ZONE: Nagafen's Lair"),
            ]);
            var clock = new MovingClock(day.AddHours(23).AddMinutes(59));
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions(), clock);
            await svc.ImportHistoryAsync();
            svc.Start();
            async Task<(string, int)> Read() => ((await svc.Query(s => s.BuildSnapshot())).Mode,
                JsonSerializer.SerializeToElement(await svc.Query(s => s.ReincarnationData())).GetProperty("levelCap").GetInt32());
            Assert.Equal(("farming", 50), await Read());
            clock.Now = day.AddDays(1).AddMinutes(1);   // 12-08 00:01: no new log line, nothing else changed
            Assert.Equal(("leveling", 55), await Read());
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
