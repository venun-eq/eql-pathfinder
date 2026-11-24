using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;

namespace EqlPathfinder.Engine.Tests;

public class CommunityReaderTests
{
    private static readonly DateTime Day = new(2026, 11, 15, 12, 0, 0);

    /// <summary>A place published at levels <paramref name="from"/> up to (not including) <paramref name="to"/>, every level at <paramref name="minutes"/>.</summary>
    private static CommunityPlace Place(string name, int from, int to, double minutes, int players = 2)
    {
        var m = new double?[49]; var t5 = new double[49]; var crawls = new int[49]; var p = new int[49]; var last = new DateTime?[49];
        for (int L = from; L < to; L++) (m[L - 1], t5[L - 1], crawls[L - 1], p[L - 1], last[L - 1]) = (minutes, 40, 6, players, Day);
        return new CommunityPlace(name, m, t5, crawls, p, last);
    }

    private static string File(params CommunityPlace[] places) => JsonSerializer.Serialize(new CommunityFile(1, Day, 2, 799, 3,
        new CommunityRules(), places,
        [new CommunityBand("50", "Plane of Hate D4", false, 10, 2, 8.9, 0, 17.1, 1, 9, 0.2, 2, 136, Day)],
        [new CommunityCompare("Kedge Keep", 41, 45, [new CommunityColumn(1, 6, 2, 4, 2, 0, 4, Day), new CommunityColumn(4, 7, 3, 2.5, 30, 0.1, 1.5, Day)])]),
        CommunityBuilder.Json);

    [Fact]
    public void TheFileReadsAsTheSameAskDataYourLogsGive()
    {
        var c = CommunityData.Parse(File(Place("Befallen D4", 11, 21, 6), Place("Estate of Unrest D4", 21, 26, 5)))!;
        Assert.Equal(2, c.Ask.Places.Count);
        Assert.Equal(6, c.Ask.Places[0].At(15));
        Assert.Null(c.Ask.Places[0].At(21));
        // Routes run on it unchanged: 10 levels in Befallen, 4 in Unrest, one change of place; at rank 1, 10% more.
        var r = Ask.Chain(c.Ask, 11, 25, countMotes: false, farmRate: 50, xpMultiplier: 1.1);
        Assert.Equal(["Befallen D4", "Estate of Unrest D4"], r.Legs.Select(l => l.Place));
        Assert.Equal((10 * 6 + 4 * 5) * 1.1 + Ask.SwitchMinutes, r.Minutes, 6);
        Assert.Equal(17.1, Assert.Single(c.Ask.Bands).T5PerHour);
        Assert.Equal([1, 4], Assert.Single(c.Ask.Difficulties).Columns.Select(x => x.Difficulty));
    }

    [Fact]
    public void PlayersAndTypicalTimesNeedEveryLevelPublished()
    {
        var c = CommunityData.Parse(File(Place("Befallen D4", 11, 16, 6, players: 3), Place("Befallen D4 extra", 16, 21, 6, players: 2)))!;
        Assert.Equal(3, c.PlayersAt("Befallen D4", 11, 16));
        Assert.Equal(0, c.PlayersAt("Befallen D4", 11, 17));          // level 16 isn't published there
        Assert.Equal(30, c.MinutesAt("Befallen D4", 11, 16));
        Assert.Equal(33, c.MinutesAt("Befallen D4", 11, 16, 1.1)!.Value, 6);
        Assert.Null(c.MinutesAt("Befallen D4", 11, 17));
        Assert.Null(c.MinutesAt("Najena D4", 11, 16));
        Assert.Equal(2, c.PlayersFor(c.Ask.Bands[0]));
        var kedge = c.Ask.Difficulties[0];
        Assert.Equal((2, 3), (c.PlayersFor(kedge, 1), c.PlayersFor(kedge, 4)));
    }

    [Fact]
    public void AskAnswersFromYourLogsAndThePlayersWithTheirNumbersBeside()
    {
        // Befallen is published from 11: level 10 reads it with the level beside it, as the route does, so its players count.
        var c = CommunityData.Parse(File(Place("Befallen D4", 11, 21, 6, players: 3), Place("Estate of Unrest D4", 21, 26, 5)))!;
        var a = Pages.AskPage.Build(new AskQuery("level", 10, 24, "fast", null, null, null),
            new Pages.AskInputs([], 0, 0, new Tracking.TrackerOptions(), 10, null, 0, c, Day));
        // No logs of your own: the route is the players' places, at their time, and says so.
        Assert.Equal(["Befallen D4", "Estate of Unrest D4"], a.Route.Legs.Select(l => l.Place));
        Assert.All(a.Route.Legs, l => Assert.Equal(0, l.MineShare));
        Assert.Equal([3, 2], a.Route.Legs.Select(l => l.TypicalPlayers));
        Assert.Equal([3, 2], a.Route.Community!.Legs.Select(l => l.Players));   // was 0 for Befallen: level 10 isn't published
        Assert.Empty(a.Route.Versus);
    }

    [Fact]
    public void TheSharedDifficultiesAnswerTheZoneYouAsked()
    {
        // Report #11: you asked about Lair of the Splitpaw 31–35, which you've played at D0 and D4. The shared numbers have
        // only Kedge Keep 41–45, and showed it under your question whatever zone you picked.
        var c = CommunityData.Parse(File(Place("Befallen D4", 11, 21, 6)))!;
        static LevelingSegment Visit(int d, int level) =>
            new($"Lair of the Splitpaw D{d}", "Lair of the Splitpaw", d, Day.AddDays(-1), level, 40, 2, 10 * d, 0, 0, 60, 0, 0);
        Pages.AskDifficulty Diff(string? key, int level, params LevelingSegment[] mine) =>
            Pages.AskPage.Build(new AskQuery("diff", null, null, null, null, null, key),
                new Pages.AskInputs(mine, 0, 0, new Tracking.TrackerOptions(), level, null, 0, c, Day)).Difficulty;
        LevelingSegment[] splitpaw = [Visit(0, 31), Visit(4, 32)];

        // Picked: your Splitpaw, and nothing from the players who share in its place.
        var picked = Diff("Lair of the Splitpaw|31", 50, splitpaw);
        Assert.Equal("Lair of the Splitpaw|31", picked.Pick?.Key);
        Assert.Null(picked.Community);
        // Opened with no pick at 32: your nearest is Splitpaw, and theirs follows it.
        var opened = Diff(null, 32, splitpaw);
        Assert.Equal("Lair of the Splitpaw|31", opened.Pick?.Key);
        Assert.Null(opened.Community);
        // A chip only they have: that zone from them alone, not your nearest zone in the question.
        var theirs = Diff("Kedge Keep|41", 32, splitpaw);
        Assert.Null(theirs.Pick);
        Assert.Equal("Kedge Keep|41", theirs.Community?.Key);
        // No zone of your own: theirs nearest your level, as before.
        Assert.Equal("Kedge Keep|41", Diff(null, 32).Community?.Key);
        // Both have the zone: yours and theirs answer the same one.
        LevelingSegment Kedge(int d, int level) => Visit(d, level) with { Place = $"Kedge Keep D{d}", Zone = "Kedge Keep" };
        var both = Diff("Kedge Keep|41", 32, [.. splitpaw, Kedge(1, 41), Kedge(4, 42)]);
        Assert.Equal(("Kedge Keep|41", "Kedge Keep|41"), (both.Pick?.Key, both.Community?.Key));
        // A pick neither has any more (Where or Who with changed): it starts from your level again.
        var stale = Diff("Najena|21", 32, splitpaw);
        Assert.Equal("Lair of the Splitpaw|31", stale.Pick?.Key);
        Assert.Null(stale.Community);
    }

    [Fact]
    public void AsksVersusRowsCarryTheGapItsCausesItsToneAndTheOverallGapForThePage()
    {
        // You: two visits at Befallen 11–16, 75 minutes for 5 levels (15 a level) and 150 kills (2 a minute). Them: 10 minutes a
        // level at 2.5 kills a minute. 50% longer; kills 20% slower; each kill fills 1 - 25 / 30 = 17% less: both.
        var theirs = Place("Befallen D4", 11, 21, 10, players: 3);
        var kills = new double[49]; var zero = new double[49]; for (int L = 11; L < 21; L++) kills[L - 1] = 2.5;
        var c = CommunityData.Parse(File(theirs with { Hours = zero, HighTierPerHour = zero, FragmentsPerHour = zero, DeathsPerHour = zero, KillsPerMinute = kills, Dps = zero }))!;
        static LevelingSegment Visit(int days) => new("Befallen D4", "Befallen", 4, Day.AddDays(-days), 11, 75, 5, 0, 0, 0, 150, 0, 0);
        var a = Pages.AskPage.Build(new AskQuery("level", 11, 16, "fast", null, null, null),
            new Pages.AskInputs([Visit(1), Visit(2)], 0, 0, new Tracking.TrackerOptions(), 11, null, 0, c, Day));
        var v = Assert.Single(a.Route.Versus);
        Assert.Equal(0.5, v.Gap, 6);
        Assert.Equal("both", v.Why);
        Assert.Equal((20, 17), (v.KillPercent, v.PerKillPercent));
        Assert.Equal((2, 2.5), (v.KillsMine, v.KillsTheirs));
        Assert.True(v.Slower);
        Assert.Equal("warn", v.Tone);
        Assert.Equal(0.5, a.Route.VersusGap, 6);
        Assert.False(a.Route.VersusSame);
    }
    [Fact]
    public void WhenYouShareYourOwnPlayLeavesTheirNumbers()
    {
        // Befallen 11–20: 3 players, 6 visits a level, 6 minutes a level. You: one visit, 9 minutes a level at 11–15. Up to 5
        // players count equally, so the other two take (3 × 6 − 9) ÷ 2 = 4.5. Unrest rests on you and one other: it goes.
        var c = CommunityData.Parse(File(Place("Befallen D4", 11, 21, 6, players: 3), Place("Estate of Unrest D4", 21, 26, 5)))!;
        static LevelingSegment Visit(string zone, int level, double minutes, int levels) =>
            new($"{zone} D4", zone, 4, Day.AddDays(-1), level, minutes, levels, 0, 0, 0, 50, 0, 0);
        var view = c.LeavingOut([Visit("Befallen", 11, 45, 5), Visit("Estate of Unrest", 21, 25, 5)], DateTime.MinValue).For()!;
        Assert.True(view.LeftYouOut);
        var befallen = view.Ask.Places.Single(p => p.Place == "Befallen D4");
        Assert.Equal(4.5, befallen.At(11));
        Assert.Equal(6, befallen.At(16));   // a level you haven't played: as published
        Assert.Equal(5, befallen.CrawlsAt(11));
        Assert.Equal((2, 3), (view.PlayersAt("Befallen D4", 11, 16), view.PlayersAt("Befallen D4", 16, 21)));
        Assert.DoesNotContain(view.Ask.Places, p => p.Place == "Estate of Unrest D4");
        Assert.Equal(["Estate of Unrest D4"], view.YouAndOne);
        // Play from before you started sharing isn't in the file: nothing to take out. Without sharing, nothing changes.
        Assert.Equal(6, c.LeavingOut([Visit("Befallen", 11, 45, 5)], Day).For()!.Ask.Places.Single(p => p.Place == "Befallen D4").At(11));
        Assert.False(c.For()!.LeftYouOut);
    }

    [Fact]
    public void SlicesCanBeReadFromManyThreadsAtOnce()
    {
        // The main window and the tracker read the same file at once; a refresh once failed on a "concurrent update" of
        // its cache (11-17). Every thread asking for a slice gets the one cached for it.
        string[] wheres = ["all", "instances", "open"], withs = ["either", "solo", "group"];
        for (int round = 0; round < 20; round++)
        {
            var c = CommunityData.Parse(File(Place("Befallen D4", 11, 21, 6, players: 3), Place("Estate of Unrest D4", 21, 26, 5)))!;
            var got = new System.Collections.Concurrent.ConcurrentBag<(string Key, CommunityView? View)>();
            Parallel.For(0, 64, i =>
            {
                string w = wheres[i % 3], x = withs[i / 3 % 3];
                got.Add(($"{w}|{x}", c.For(w, x)));
            });
            foreach (var g in got.GroupBy(x => x.Key))
                Assert.Single(g.Select(x => x.View).Distinct());
        }
    }

    [Fact]
    public void AFileThisVersionDoesNotReadIsIgnored()
    {
        Assert.Null(CommunityData.Parse("not json"));
        Assert.Null(CommunityData.Parse(File().Replace("\"schema\":1", "\"schema\":3")));   // newer than this version reads
        var shortPlace = new CommunityPlace("Befallen D4", new double?[10], new double[10], new int[10], new int[10], new DateTime?[10]);
        Assert.Null(CommunityData.Parse(File(shortPlace)));
        Assert.NotNull(CommunityData.Parse(File()));
    }
}
