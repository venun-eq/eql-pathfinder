using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>A visit's whole clock, zone-in to zone-out, and where its minutes went (docs/specs/visits-tab.md). The page only draws these.</summary>
public class VisitClockTests
{
    private static string L(string hms, string body) => $"[Sat Nov 14 {hms} 2026] {body}";

    [Fact]
    public void ThePartsOfAVisitsClockAddUpToTheWhole()
    {
        var sink = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), sink);
        foreach (var l in new[]
        {
            L("09:59:00", "You have gained a level! Welcome to level 20!"),
            L("10:00:00", "You have entered Befallen 4 (Refined)."),
            L("10:01:00", "You gain experience! (30.000%)"), L("10:01:00", "You have slain a ghoul!"),
            L("10:04:00", "You have entered Befallen."),                // out for two minutes
            L("10:06:00", "You have entered Befallen 4 (Refined)."),
            L("10:08:00", "You gain experience! (20.000%)"), L("10:08:00", "You have slain a ghoul!"),
            L("10:10:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("10:13:00", "You have entered Befallen."),                // out three minutes after it completed
        }) e.ProcessLine(l);
        e.Flush();
        var v = Assert.Single(sink.Closed);
        var s = VisitStats.From(v, DateTime.Now, new TrackerOptions());
        Assert.Equal(13, s.WholeMinutes, 6);
        Assert.Equal(2, s.OutsideMinutes, 6);
        Assert.Equal(3, s.AfterMinutes, 6);
        Assert.Equal(2, s.IdleMinutes, 6);     // the 5 minutes inside between the kills, 3 allowed
        Assert.Equal(6, s.ActiveMinutes, 6);
        Assert.Equal(s.WholeMinutes, s.ActiveMinutes + s.IdleMinutes + s.AfterMinutes + s.OutsideMinutes, 9);
        Assert.Equal(new DateTime(2026, 11, 14, 10, 13, 0), s.ZonedOut);
        Assert.Equal("finished", s.State);
    }

    [Fact]
    public void AVisitStillInsideHasItsClockSoFar()
    {
        var start = new DateTime(2026, 11, 14, 22, 8, 0);
        var v = new Visit { Id = "v", Start = start, SegmentStart = start, Zone = "Befallen 4 (Refined)", BaseZone = "Befallen", Difficulty = 4 };
        var s = VisitStats.From(v, start.AddMinutes(12), new TrackerOptions());
        Assert.Equal("in progress", s.State);
        Assert.Equal(12, s.WholeMinutes, 6);
        Assert.Null(s.ZonedOut);
        Assert.Equal(s.WholeMinutes, s.ActiveMinutes + s.IdleMinutes + s.AfterMinutes + s.OutsideMinutes, 9);

        // Out and not back yet: its clock stops where it zoned out.
        v.SegmentStart = null; v.ActiveSeconds = 300; v.PausedAt = start.AddMinutes(5); v.ExitedAt = start.AddMinutes(5);
        var out_ = VisitStats.From(v, start.AddMinutes(12), new TrackerOptions());
        Assert.Equal("paused", out_.State);
        Assert.Equal(5, out_.WholeMinutes, 6);
        Assert.Equal(start.AddMinutes(5), out_.ZonedOut);
    }
}
