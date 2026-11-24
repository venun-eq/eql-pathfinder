using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Combat;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The fight meter's trio is the app's: /who, or carried and checked against the spells cast (11-16).</summary>
public class TrioTimelineTests
{
    private static readonly DateTime T0 = new(2026, 11, 15, 20, 0, 0);

    private static TrioTimeline Timeline() => new([
        (T0, "PAL/MNK/ENC"), (T0.AddHours(1), "PAL/MNK/ENC"),   // the same trio twice: one stretch
        (T0.AddHours(2), "PAL/MNK/SHM"),
    ]);

    [Fact]
    public void ATimeFallsInTheStretchThatStartedLast()
    {
        var t = Timeline();
        Assert.Equal(2, t.Count);
        Assert.Null(t.At(T0.AddMinutes(-1)));
        Assert.Equal("PAL/MNK/ENC", t.At(T0.AddMinutes(90))?.Trio);
        Assert.Equal(("PAL/MNK/SHM", T0.AddHours(2)), (t.At(T0.AddDays(3))?.Trio, t.At(T0.AddDays(3))?.From));
        Assert.Equal([(T0, T0.AddHours(2), "PAL/MNK/ENC"), (T0.AddHours(2), DateTime.MaxValue, "PAL/MNK/SHM")], t.Spans());
    }

    [Fact]
    public void TheMeterUsesTheAppsTrioUnlessANewerWhoSaysOtherwise()
    {
        var c = new FightContext { Trio = "WAR/CLR/ROG", WhoAt = T0.AddMinutes(-30), Timeline = Timeline() };
        Assert.Equal("PAL/MNK/ENC", c.TrioAt(T0.AddMinutes(10)));          // the /who is older than the stretch
        Assert.Equal("WAR/CLR/ROG", c.TrioAt(T0.AddMinutes(-10)));         // before the first visit: the /who
        c.WhoAt = T0.AddHours(3);                                          // a /who after the last visit began
        Assert.Equal("WAR/CLR/ROG", c.TrioAt(T0.AddHours(4)));
        Assert.Equal("PAL/MNK/SHM", c.TrioAt(T0.AddHours(2.5)));           // ...but not before it was typed
        Assert.Equal("WAR/CLR/ROG", new FightContext { Trio = "WAR/CLR/ROG" }.TrioAt(T0));   // no timeline yet
    }

    [Fact]
    public void StoredFightsTakeTheAppsTrio()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fights-{Guid.NewGuid():N}.db");
        try
        {
            using (var store = new FightStore(path))
            {
                Fight F(double h, string trio) => new()
                {
                    Start = T0.AddHours(h),
                    End = T0.AddHours(h).AddSeconds(9),
                    Mob = "a rat",
                    Zone = "Befallen",
                    Difficulty = 4,
                    Trio = trio,
                    Session = T0,
                    Stint = T0
                };
                store.Save("Venun_oggok", [F(-1, "BRD/DRU/RNG"), F(0.5, "WAR/CLR/ROG"), F(2.5, "WAR/CLR/ROG")], null);
                Assert.Equal(2, store.Relabel("Venun_oggok", Timeline()));
                Assert.Single(store.InPlace("Venun_oggok", "Befallen", 4, "PAL/MNK/ENC"));
                Assert.Single(store.InPlace("Venun_oggok", "Befallen", 4, "PAL/MNK/SHM"));
                Assert.Single(store.InPlace("Venun_oggok", "Befallen", 4, "BRD/DRU/RNG"));   // before the first visit: kept
            }
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Fact]
    public void AStintToAKnownSlotWithNoVisitGivesItsTrioToAFightAfterIt()
    {
        var t = new TrioTimeline([(T0, "PAL/MNK/ENC"), (T0.AddHours(1), "PAL/MNK/SHM")]);   // the second from a swap, no visit
        Assert.Equal("PAL/MNK/SHM", new FightContext { Trio = "WAR/CLR/ROG", Timeline = t }.TrioAt(T0.AddHours(1.5)));
    }

    [Fact]
    public void ASwapToASlotNotMetGivesAFightAfterItNoTrio()
    {
        var t = new TrioTimeline([(T0, "PAL/MNK/ENC"), (T0.AddHours(1), null)]);
        Assert.Null(new FightContext { Trio = "WAR/CLR/ROG", Timeline = t }.TrioAt(T0.AddHours(1.5)));
        Assert.Equal("PAL/MNK/ENC", new FightContext { Trio = "WAR/CLR/ROG", Timeline = t }.TrioAt(T0.AddMinutes(30)));
    }

    [Fact]
    public void AFightBetweenTheSwapAndTheWhoTakesTheWhosTrio()
    {
        // The engine's stint: opened with no trio at the swap, filled by the /who that met the slot.
        var e = new PathfinderEngine("Venun", new TrackerOptions(), new TrackerTests.Sink());
        e.Tracker.Apply(new Parsing.LoadoutSwapped(T0, 2));
        e.ProcessLine("[Sun Nov 15 20:10:00 2026] [1 SHD/SHM/WIZ] Venun (Iksar) ZONE: Befallen");
        e.Flush();
        var t = new TrioTimeline(e.State.Stints.Select(s => (s.From, s.Trio)));
        Assert.Equal("SHD/SHM/WIZ", t.At(T0.AddMinutes(5))?.Trio);
    }

    [Fact]
    public void ALaterVisitsPointStillWinsAfterAStint()
    {
        var t = new TrioTimeline([(T0, null), (T0.AddHours(1), "PAL/MNK/ENC"), (T0.AddHours(2), "PAL/MNK/SHM")]);
        Assert.Equal("PAL/MNK/SHM", t.At(T0.AddHours(3))?.Trio);
        Assert.Null(t.At(T0.AddMinutes(10))?.Trio);
    }
}
