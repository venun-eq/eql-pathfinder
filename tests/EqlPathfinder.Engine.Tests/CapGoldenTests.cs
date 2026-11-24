using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// cap-fixture: hand-checked numbers for visits at level 50 (<see cref="TestLog.CapNagafen"/>). Worked out from docs/pace.md,
/// docs/log-formats.md and the raw lines, never from what the engine printed. At the cap a kill is its slain line (yours,
/// your pet's, or a groupmate's inside an instance), none of them is an XP kill, and their XP percents are the bar filling,
/// kept as CapPercent, not levels.
/// </summary>
public class CapGoldenTests
{
    private sealed class Sink : ITrackerSink
    {
        public List<Visit> Closed { get; } = new();
        public void OnVisitClosed(Visit v) => Closed.Add(v);
        public void OnKill(KillRecord k) { }
    }

    private static Visit Visit(string zone, string start)
    {
        var s = new Sink();
        var e = new PathfinderEngine(TestLog.Character, new TrackerOptions(), s);
        e.ReadFile(TestLog.CapNagafen);
        e.Flush();
        return s.Closed.Single(v => v.Zone == zone && v.Start.ToString("HH:mm:ss") == start);
    }

    /// <summary>
    /// The raid, Plane of Hate - Solo 4, zone-in 23:33:15, out 23:40:45 (the next "You have entered"). A raid never completes,
    /// so the visit is the whole clock: 450 s = 7.5 min, nothing after. Kills (slain lines) at 23:34:30, 23:35:29, 23:37:08 and
    /// 23:39:10 (Innoruuk, the boss): gaps 75, 59, 99, 122 and 95 to the exit, none past 180 s, so idle 0 and active 7.5 min.
    /// All 4 are at the cap: CapKills 4, no XP kills, CapPercent 2.018 + 1.896 + 1.896 + 2.317 = 8.127, kill levels 0. One loot line, a Mote of Major Potential = 1 T5-eq
    /// (docs/motes-and-fragments.md). The visit never finished and has 4 kills, under the 8 that make a rate (docs/specs/one-rate.md,
    /// `Rates.MinKills`): kills a minute and T5-eq an hour are null, never 0.
    /// </summary>
    [Fact]
    public void ARaidAtTheCapIsItsWholeClockAndItsKillsAreNotXpKills()
    {
        var v = Visit("The Plane of Hate - Solo 4 (Refined)", "23:33:15");
        var s = VisitStats.From(v, v.EndTime.AddHours(1), new TrackerOptions());
        Assert.Equal(4, s.Kills);
        Assert.Equal(4, v.CapKills);
        Assert.Equal(0, s.XpKills);
        Assert.Equal(8.127, v.CapPercent, 3);
        Assert.Equal(0, v.KillPercent, 6);
        Assert.Null(s.XpPerKill);
        Assert.Equal(7.5, s.WholeMinutes, 3);
        Assert.Equal(0, s.IdleMinutes, 6);
        Assert.Equal(7.5, s.ActiveMinutes, 3);
        Assert.Equal(0, s.AfterMinutes, 6);
        Assert.Null(s.KillsPerMinute);
        Assert.Equal(1.0, s.T5, 3);
        Assert.Null(s.T5PerHour);
        Assert.True(s.Raid);
        Assert.Equal("Innoruuk, the Prince of Hate", s.Boss);
    }

    /// <summary>
    /// Nagafen's Lair 4 (Refined), a finished crawl at 50. Zone-in 05:11:43; completed 05:27:46; the last kill, Efreeti Lord
    /// Djarn, is read at 05:27:47 (a second after completion, like the Befallen stragglers): inside 964 s = 16.0667 min. The
    /// next "You have entered" is 05:30:46: whole 1143 s = 19.05 min, after 179 s. No gap between kills passes 150 s, so idle 0.
    /// Kills: 49 "You have slain" + 16 "has been slain by" (all by the player's own pet) = 65; 65 x 60 / 964 s = 4.0456 a minute.
    /// 55 of them have an XP line with a percent, summing 23.624 (a hand sum of the log's lines): CapPercent, never levels, and
    /// the other 10 have none. Every one is a cap kill, so XP per kill has nothing to divide: null. T5-eq: a Major mote (1)
    /// and the chest's 4 Major fragments at the app's fragment value 3.56 = 1 + 14.24 = 15.24; x 60 / 16.0667 min = 56.91 an hour.
    /// </summary>
    [Fact]
    public void AFinishedCrawlAtTheCapHasItsClockKillsAndMotesButNoLevels()
    {
        var v = Visit("Nagafen's Lair 4 (Refined)", "05:11:43");
        var s = VisitStats.From(v, v.EndTime.AddHours(1), new TrackerOptions());
        Assert.True(v.Completed);
        Assert.Equal(964 / 60.0, s.ActiveMinutes, 3);
        Assert.Equal(0, s.IdleMinutes, 6);
        Assert.Equal(1143 / 60.0, s.WholeMinutes, 3);
        Assert.Equal(179 / 60.0, s.AfterMinutes, 3);
        Assert.Equal(65, s.Kills);
        Assert.Equal(65, v.CapKills);
        Assert.Equal(0, s.XpKills);
        Assert.Equal(23.624, v.CapPercent, 2);
        Assert.Equal(0, v.KillPercent, 6);
        Assert.Null(s.XpPerKill);
        Assert.Equal(65 * 60 / 964.0, s.KillsPerMinute!.Value, 3);
        Assert.Equal(0, s.KillLevels, 6);
        Assert.Equal(15.24, s.T5, 2);
        Assert.Equal(15.24 * 60 / (964 / 60.0), s.T5PerHour!.Value, 1);
        Assert.False(s.Raid);
    }

    /// <summary>
    /// The fixture's whole History, not only the two visits held above: the instances the log enters, in order, and no others.
    /// Read off the "You have entered" lines for an instance (a name ending in a difficulty): zone-ins at 23:33:15, 23:42:48,
    /// 00:10:08, 01:47:57 and 05:11:43. The other zones entered (Oasis of Marr, Lavastorm, Paineel, ...) are not instance visits.
    /// </summary>
    [Fact]
    public void TheWholeHistoryIsTheseInstancesAndNoOthers()
    {
        var s = new Sink();
        var e = new PathfinderEngine(TestLog.Character, new TrackerOptions(), s);
        e.ReadFile(TestLog.CapNagafen);
        e.Flush();
        var got = s.Closed.Where(v => v.Difficulty != null).OrderBy(v => v.Start).Select(v => v.Start.ToString("HH:mm:ss") + " " + v.Zone).ToList();
        Assert.Equal(new[]
        {
            "23:33:15 The Plane of Hate - Solo 4 (Refined)",
            "23:42:48 The Plane of Hate - Solo 3 (Fused)",
            "00:10:08 Nagafen's Lair - Solo 4 (Refined)",
            "01:47:57 The Ruins of Old Paineel - Solo 4 (Refined)",
            "05:11:43 Nagafen's Lair 4 (Refined)",
        }, got);
    }
}
