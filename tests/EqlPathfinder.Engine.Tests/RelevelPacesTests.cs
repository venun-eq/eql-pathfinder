using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class RelevelPacesTests
{
    private static readonly DateTime T0 = new(2026, 11, 11, 12, 0, 0);
    private static int _id;

    /// <summary>A closed crawl: <paramref name="hours"/> active at <paramref name="lph"/> levels per hour.</summary>
    private static Visit Crawl(string trio, DateTime start, string zone, int diff, int level, double hours, double lph, int kills = 60) => new()
    {
        Id = $"v-{++_id}",
        Zone = $"{zone} {diff}",
        BaseZone = zone,
        Difficulty = diff,
        Start = start,
        ActiveSeconds = hours * 3600,
        CompletedAt = start.AddHours(hours),
        Completed = true,
        Closed = true,
        Trio = trio,
        TrioConfirmed = true,
        LevelStart = level,
        LevelEnd = (int)(level + hours * lph),
        Kills = kills,
        KillPercent = hours * lph * 100,
    };

    private static EngineState Roster(params (string Name, DateTime At, bool Race)[] reincs)
    {
        var s = new EngineState();
        foreach (var (name, at, race) in reincs)
        {
            var i = race ? s.Roster.Race(name) : s.Roster.Class(name);
            i.Seen.Add(at);
            i.Reincarnated = at;
            i.Rank = i.Seen.Count;
            i.Level = 50;
        }
        return s;
    }

    private static (string, DateTime, bool)[] Trio(DateTime at, params string[] classes) => classes.Select(c => (c, at, false)).ToArray();

    /// <summary>Two finished relevels on the same route; the first levels 20% faster in every zone (and the second <paramref name="mistmoore"/> as fast in Castle of Mistmoore).</summary>
    private static (List<Visit> Visits, EngineState State) TwoRelevels(double secondPace = 1 / 1.2, double mistmoore = 1)
    {
        // Every class but BER has reincarnated once; the others did it the day before, with no crawls since.
        var others = ClassNames.All.Select(c => c.Abbrev).Except(["RNG", "DRU", "BRD", "WAR", "CLR", "ROG", "BER"]).ToArray();
        var s = Roster(Trio(T0, "RNG", "DRU", "BRD").Concat(Trio(T0, "WAR", "CLR", "ROG")).Concat(Trio(T0.AddDays(-1), others)).ToArray());
        var v = new List<Visit>();
        var t = T0.AddHours(1);
        foreach (var (trio, pace) in new[] { ("RNG/DRU/BRD", 1.0), ("WAR/CLR/ROG", secondPace) })
        {
            int level = 1;
            foreach (var (zone, lph) in new[] { ("Befallen", 12.0), ("Castle of Mistmoore", 6.0), ("Kedge Keep", 4.0), ("Plane of Hate", 2.0) })
                for (int k = 0; k < 3 && level < 50; k++)
                {
                    double rate = lph * pace * (zone == "Castle of Mistmoore" && trio == "WAR/CLR/ROG" ? mistmoore : 1), hours = Math.Min(1.0, (50 - level) / rate);
                    hours = Math.Max(hours, 0.2);
                    var c = Crawl(trio, t, zone, 4, level, hours, rate);
                    v.Add(c);
                    level = Math.Min(50, c.LevelEnd!.Value);
                    t = t.AddHours(hours + 0.1);
                }
            // Reach the cap in the last crawl so the relevel is finished.
            v[^1].LevelEnd = 50;
            t = t.AddDays(1);
        }
        return (v, s);
    }

    [Fact]
    public void TheFasterRelevelIsAheadLikeForLike()
    {
        var (visits, s) = TwoRelevels();
        var o = new TrackerOptions();
        var js = Journeys.Build(visits, s, o);
        Assert.Equal(2, js.Count(j => j.Done));
        var r = RelevelPaces.Build(visits, js, s, o, new Dictionary<string, (int, int)>());
        var first = r.Paces.Single(p => p.TrioKey == ClassNames.TrioKey(["RNG", "DRU", "BRD"]));
        var second = r.Paces.Single(p => p.TrioKey == ClassNames.TrioKey(["WAR", "CLR", "ROG"]));
        Assert.True(first.Pace > 1 && second.Pace < 1, $"first {first.Pace}, second {second.Pace}");
        Assert.Equal("solo", first.Kind);
        Assert.Equal([1, 2], r.Paces.Select(p => p.Number));
    }

    [Fact]
    public void GroupedCrawlsCountTowardTheGroupedShare()
    {
        var (visits, s) = TwoRelevels(1.0);
        var party = new Dictionary<string, (int, int)>();
        // Half of the second relevel's crawls were grouped.
        foreach (var v in visits.Where(v => v.Trio == "WAR/CLR/ROG").Where((_, i) => i % 2 == 0)) party[v.Id] = (v.Kills, v.Kills);
        var o = new TrackerOptions();
        var js = Journeys.Build(visits, s, o);
        var r = RelevelPaces.Build(visits, js, s, o, party);
        Assert.InRange(r.Paces.Single(p => p.TrioKey == ClassNames.TrioKey(["WAR", "CLR", "ROG"])).GroupedShare, 0.3, 0.7);
        Assert.Equal(0, r.Paces.Single(p => p.TrioKey == ClassNames.TrioKey(["RNG", "DRU", "BRD"])).GroupedShare);
    }

    [Fact]
    public void AStretchSlowerThanYourUsualIsWhereTheHoursWent()
    {
        // The second relevel took Castle of Mistmoore at half speed; everything else ran the same.
        var (visits, s) = TwoRelevels(1.0, mistmoore: 0.5);
        var o = new TrackerOptions();
        var js = Journeys.Build(visits, s, o);
        var r = RelevelPaces.Build(visits, js, s, o, new Dictionary<string, (int, int)>());
        Assert.NotEmpty(r.Detours);
        var top = r.Detours[0];
        Assert.Contains("Castle of Mistmoore", top.Name);
        Assert.Contains(js.Single(j => j.TrioKey == ClassNames.TrioKey(["WAR", "CLR", "ROG"])).Trio, top.Source);
        Assert.True(top.Hours >= 0.25, $"{top.Hours} h");
    }

    [Fact]
    public void ReincarnatingOneClassAgainKeepsTheFinishedRelevel()
    {
        var (visits, s) = TwoRelevels();
        // Later RNG reincarnates again with BER and MNK, and a new relevel starts.
        var again = T0.AddDays(10);
        foreach (var c in new[] { "RNG", "BER", "MNK" })
        {
            var i = s.Roster.Class(c);
            i.Seen.Add(again); i.Reincarnated = again; i.Rank = i.Seen.Count; i.Level = 12;
        }
        visits.Add(Crawl("BER/RNG/MNK", again.AddHours(1), "Befallen", 4, 1, 1, 11));
        var js = Journeys.Build(visits, s, new TrackerOptions());
        var old = js.Single(j => j.TrioKey == "BRD/DRU/RNG");
        Assert.True(old.Done);
        Assert.Equal(1, old.Rank); // the rank it releveled at, not RNG's rank 2 today
        var now = js.Single(j => j.TrioKey == "BER/MNK/RNG");
        Assert.False(now.Done);
        Assert.Equal(2, now.Rank);
    }

    [Fact]
    public void OpenWorldHuntingCountsTowardTheRelevel()
    {
        var (visits, s) = TwoRelevels();
        var first = visits.First(v => v.Trio == "RNG/DRU/BRD");
        // Ten kills a minute apart outside, right after the first crawl: one stretch of 10 minutes.
        var kills = Enumerable.Range(0, 10).Select(i => new KillRecord(first.EndTime.AddMinutes(5 + i), null, "Befallen", null, 5, "a rat", 4, false, null))
            .Append(new KillRecord(first.EndTime.AddMinutes(40), null, "Befallen", null, 5, "a rat", 4, true, null))
            .ToList();
        var field = FieldHunts.From(kills, 50);
        Assert.Equal(2, field.Count);
        Assert.Equal(10, field[0].Minutes, 3);
        var without = Journeys.Build(visits, s, new TrackerOptions()).Single(j => j.TrioKey == "BRD/DRU/RNG");
        var with = Journeys.Build(visits, s, new TrackerOptions(), field).Single(j => j.TrioKey == "BRD/DRU/RNG");
        Assert.Equal(without.ActiveHours + 11 / 60.0, with.ActiveHours, 3);
        Assert.Equal(0.44, with.FieldLevels, 3);
        Assert.Equal(without.Kills + 11, with.Kills);
        // The hunting shows on the route as its own leg, right after the first crawl, however small.
        Assert.Equal("crawl", with.Route[0].Kind);
        Assert.Equal("open world", with.Route[1].Kind);
        Assert.Equal("Befallen (open world)", with.Route[1].Label);
        // The second stretch came after the next crawl began, so it's a leg of its own further on.
        Assert.Equal(2, with.Route.Where(l => l.Kind == "open world").Sum(l => l.Crawls));
    }

    [Fact]
    public void OtherInstancesCountTowardTheRelevelButNotAsCrawls()
    {
        var (visits, s) = TwoRelevels();
        var first = visits.First(v => v.Trio == "RNG/DRU/BRD");
        var task = new Visit
        {
            Id = "v-task",
            Zone = "Torrid Corruptor - Solo",
            BaseZone = "Torrid Corruptor - Solo",
            Start = first.EndTime.AddMinutes(5),
            ActiveSeconds = 1800,
            Closed = true,
            ExitedAt = first.EndTime.AddMinutes(35),
            Trio = "RNG/DRU/BRD",
            TrioConfirmed = true,
            LevelStart = first.LevelEnd,
            LevelEnd = first.LevelEnd,
            Kills = 20,
            KillPercent = 50,
        };
        var before = Journeys.Build(visits, s, new TrackerOptions()).Single(j => j.TrioKey == "BRD/DRU/RNG");
        visits.Add(task);
        var after = Journeys.Build(visits, s, new TrackerOptions()).Single(j => j.TrioKey == "BRD/DRU/RNG");
        Assert.Equal(before.Crawls, after.Crawls);
        Assert.Equal(0.5, after.InstanceHours, 3);
        Assert.Equal(before.ActiveHours + 0.5, after.ActiveHours, 3);
        Assert.Equal(before.Levels + 0.5, after.Levels, 3);
        Assert.Contains(after.Route, l => l.Kind == "instance" && l.BaseZone == "Torrid Corruptor - Solo");
    }
}
