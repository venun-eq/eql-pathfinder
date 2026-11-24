using System.Text.Json;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class TrackerTests
{
    internal sealed class Sink : ITrackerSink
    {
        public List<Visit> Closed { get; } = new();
        public List<Visit> Updated { get; } = new();
        public List<KillRecord> Kills { get; } = new();
        public List<HighTierDrop> High { get; } = new();
        public void OnVisitClosed(Visit v) => Closed.Add(v);
        public void OnVisitUpdated(Visit v) => Updated.Add(v);
        public void OnKill(KillRecord k) => Kills.Add(k);
        public void OnHighTier(HighTierDrop d) => High.Add(d);
    }

    private static (PathfinderEngine e, Sink s) Run(params string[] lines)
    {
        var s = new Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in lines) e.ProcessLine(l);
        e.Flush();
        return (e, s);
    }

    private static string L(string hms, string body) => $"[Sat Nov 14 {hms} 2026] {body}";

    [Fact]
    public void CrawlRewardIsSizedAtTheNextDing()
    {
        var (e, s) = Run(
            L("10:00:00", "You have gained a level! Welcome to level 20!"),
            L("10:00:10", "You have entered Befallen 4 (Refined)."),
            L("10:01:00", "You gain experience! (30.000%)"),
            L("10:01:00", "You have slain a ghoul!"),
            L("10:10:00", "You gain experience! (20.000%)"),
            L("10:10:00", "You have slain a ghoul!"),
            L("10:10:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("10:10:00", "You gained reward experience from the Dungeon Crawl!"),
            L("10:11:00", "You have entered Befallen."),
            L("10:20:00", "You gain experience! (10.000%)"),
            L("10:20:00", "You have slain a ghoul!"),
            L("10:20:00", "You have gained a level! Welcome to level 21!"));
        var v = Assert.Single(s.Closed);
        Assert.Equal(0.5, v.KillPercent / 100, 3);
        // 100 - (30 + 20 + 10) = 40% of level 20 came from the reward.
        var r = Assert.Single(v.Rewards);
        Assert.Equal(40, r.Share, 3);
        Assert.False(r.Sizing);
        Assert.Single(s.Updated);
        Assert.Equal(2, s.Kills.Count(k => k.VisitId == v.Id));
        Assert.Equal("a ghoul", s.Kills[0].Mob);
        Assert.Equal(20, v.LevelStart);
        Assert.Equal(9 * 60 + 50, v.ActiveSeconds, 0);
    }

    [Fact]
    public void BonusXpKillsCountAndMarkTheEvent()
    {
        var (e, s) = Run(
            L("10:00:00", "You have gained a level! Welcome to level 20!"),
            L("10:00:10", "You have entered Befallen 4 (Refined)."),
            L("10:01:00", "You gain party experience (with a bonus)! (30.000%)"),
            L("10:01:00", "You have slain a ghoul!"),
            L("10:20:00", "You gain experience (with a bonus)! (20.000%)"),
            L("10:20:00", "You have slain a ghoul!"),
            L("10:21:00", "You have entered Befallen."),
            L("12:30:00", "You gain experience (with a bonus)! (5.000%)"),
            L("12:30:00", "You have slain a ghoul!"));
        var v = Assert.Single(s.Closed);
        Assert.Equal(2, v.Kills);
        Assert.Equal(50, v.KillPercent, 3);
        // One event while the lines come within an hour of each other; a later one is its own.
        Assert.Equal([(At(10, 1), At(10, 20)), (At(12, 30), At(12, 30))], e.State.BonusXp.Select(b => (b.Start, b.End)));
        var facts = EqlPathfinder.Engine.Analysis.PlayFacts.From([], e.State);
        Assert.True(facts.DuringBonus(At(10, 0), At(10, 5)));
        Assert.False(facts.DuringBonus(At(10, 21), At(12, 29)));
        // Ask and the pace checks leave the bonus crawl out (like for like), and say how many they left out.
        var (segments, _, _, bonusLeftOut) = EqlPathfinder.Engine.Analysis.PlayFacts.From(s.Closed, e.State).Segments([], [], [], new TrackerOptions());
        Assert.Empty(segments);
        Assert.Equal(1, bonusLeftOut);
        static DateTime At(int h, int m) => new(2026, 11, 14, h, m, 0);
    }

    [Fact]
    public void RewardThatCausesADingSpillsIntoTheNextLevel()
    {
        var (_, s) = Run(
            L("10:00:00", "You have gained a level! Welcome to level 20!"),
            L("10:00:10", "You have entered Befallen 4 (Refined)."),
            L("10:05:00", "You gain experience! (90.000%)"),
            L("10:05:00", "You have slain a ghoul!"),
            L("10:05:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("10:05:00", "You gained reward experience from the Dungeon Crawl!"),
            L("10:05:00", "You have gained a level! Welcome to level 21!"),
            L("10:06:00", "You have entered Befallen."),
            L("10:20:00", "You gain experience! (75.000%)"),
            L("10:20:00", "You have slain a ghoul!"),
            L("10:30:00", "You have gained a level! Welcome to level 22!"));
        var r = Assert.Single(Assert.Single(s.Closed).Rewards);
        // 10% to finish 20, then 25% of 21 (100 - 75 from kills).
        Assert.Equal(35, r.Share, 3);
        Assert.False(r.Sizing);
    }

    [Fact]
    public void LeavingAndComingBackResumesTheSameVisit()
    {
        var (e, s) = Run(
            L("10:00:00", "You have entered Kedge Keep 4 (Refined)."),
            L("10:05:00", "You gain experience! (2.000%)"),
            L("10:05:00", "You have slain a ghoul!"),
            L("10:06:00", "You have been slain by a seahorse patriarch!"),
            L("10:06:05", "You have entered Dagnor's Cauldron."),
            L("10:09:05", "You have entered Kedge Keep 4 (Refined)."),
            L("10:15:05", "You gain experience! (2.000%)"),
            L("10:15:05", "You have slain a ghoul!"),
            L("10:15:05", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("10:16:00", "You have entered Dagnor's Cauldron."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(2, v.Kills);
        Assert.Equal(1, v.Deaths);
        // 6:05 inside, 3:00 outside (not counted), 6:00 inside.
        Assert.Equal(12 * 60 + 5, v.ActiveSeconds, 0);
    }

    [Fact]
    public void IdleCountsFromZoneInToTheEndNotJustBetweenKills()
    {
        // As on 11-16: zoned in, eight minutes on the phone, then two kills a minute apart and the crawl done.
        var (_, s) = Run(
            L("20:18:49", "You have entered Kedge Keep 4 (Refined)."),
            L("20:26:49", "You gain experience! (20.000%)"),
            L("20:26:49", "You have slain a ghoul!"),
            L("20:27:49", "You gain experience! (20.000%)"),
            L("20:27:49", "You have slain a ghoul!"),
            L("20:28:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("20:30:00", "You have entered Dagnor's Cauldron."));
        var v = Assert.Single(s.Closed);
        Assert.Equal((8 * 60 - 180), v.IdleSeconds(180, v.EndTime), 3);   // 8 minutes before the first kill: 3 count
        Assert.Equal(9 * 60 + 11, v.CrawlSeconds(v.EndTime), 3);
    }

    [Fact]
    public void AnOpenCrawlDoesNotSinkWhileYouAreAway()
    {
        var (e, _) = Run(
            L("10:00:00", "You have entered Kedge Keep 4 (Refined)."),
            L("10:00:30", "You gain experience! (20.000%)"),
            L("10:00:30", "You have slain a ghoul!"),
            L("10:01:00", "You gain experience! (20.000%)"),
            L("10:01:00", "You have slain a ghoul!"));
        var c = e.State.Current!;
        var later = new DateTime(2026, 11, 14, 10, 11, 0);                  // ten minutes since the last kill
        Assert.Equal(7 * 60, c.IdleSeconds(180, later), 3);
        Assert.Equal(0, c.IdleSeconds(180, new DateTime(2026, 11, 14, 10, 2, 0)), 3);
    }

    [Fact]
    public void TimeOutsideTheInstanceIsNotTakenOffTwice()
    {
        // A kill, out at once (a death), back eight minutes later, a kill: inside, 30 s each side, no idle at all.
        var (_, s) = Run(
            L("10:00:00", "You have entered Kedge Keep 4 (Refined)."),
            L("10:00:30", "You gain experience! (20.000%)"),
            L("10:00:30", "You have slain a ghoul!"),
            L("10:01:00", "You have entered Neriak - Commons."),
            L("10:09:00", "You have entered Kedge Keep 4 (Refined)."),
            L("10:09:30", "You gain experience! (20.000%)"),
            L("10:09:30", "You have slain a ghoul!"),
            L("10:10:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("10:11:00", "You have entered Neriak - Commons."));
        var v = Assert.Single(s.Closed);
        Assert.Single(v.Pauses);
        Assert.Equal(0, v.IdleSeconds(180, v.EndTime), 3);
        Assert.Equal(120, v.CrawlSeconds(v.EndTime), 3);
    }

    [Fact]
    public void ACrawlWithNoKillsHasNoIdle()
    {
        // Nothing slain: nothing to measure activity by (at the cap too, now that kills there count: KillsAtCapTests).
        var (e, _) = Run(L("10:00:00", "You have entered Kedge Keep 4 (Refined)."), L("10:00:05", "You looted a Mote of Major Potential from a ghoul's corpse and stored it in your currency"));
        Assert.Equal(0, e.State.Current!.IdleSeconds(180, new DateTime(2026, 11, 14, 10, 30, 0)));
    }

    [Fact]
    public void AD0InstanceEntersUnderThePlainZoneNameAndIsACrawl()
    {
        // As in Venun's log on 11-16: D0 has no "0 (Normal)" after the name; only the line before says it's an instance.
        var (_, s) = Run(
            L("18:57:35", "You have entered The Estate of Unrest."),
            L("18:57:43", "Player Venun creating instance The Estate of Unrest 274."),
            L("18:58:03", "You have entered The Estate of Unrest."),
            L("18:59:00", "You gain experience! (2.000%)"),
            L("18:59:00", "You have slain a ghoul!"),
            L("19:08:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("19:08:30", "You have entered The Estate of Unrest."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(0, v.Difficulty);
        Assert.Equal("Normal", v.DifficultyLabel);
        Assert.Equal("The Estate of Unrest", v.BaseZone);
        Assert.True(v.Completed);
        Assert.Equal(1, v.Kills);
    }

    [Fact]
    public void ThePlainZoneWithoutANewInstanceIsTheOpenWorld()
    {
        var (_, s) = Run(
            L("10:00:00", "Player Venun creating instance Kedge Keep 381."),
            L("10:01:00", "You have entered Dagnor's Cauldron."),          // made it, went elsewhere: not an instance
            L("10:02:00", "You have entered The Estate of Unrest."),
            L("10:03:00", "You gain experience! (2.000%)"),
            L("10:03:00", "You have slain a ghoul!"),
            L("10:30:00", "You have entered Oggok."));
        Assert.Empty(s.Closed);
    }

    [Fact]
    public void ADeathInAD0CrawlComesBackToTheSameVisit()
    {
        var (_, s) = Run(
            L("10:00:00", "Player Venun creating instance The Estate of Unrest 274."),
            L("10:00:20", "You have entered The Estate of Unrest."),
            L("10:01:00", "You gain experience! (2.000%)"),
            L("10:01:00", "You have slain a ghoul!"),
            L("10:05:00", "You have entered Neriak - Commons."),             // died, bound elsewhere
                                                                             // The way back is through the entrance: the open world, then the instance (docs/log-formats.md).
            L("10:09:00", "You have entered The Estate of Unrest."),         // the open world
            L("10:09:20", "You have entered The Estate of Unrest."),         // back in within 10 minutes
            L("10:10:00", "You gain experience! (2.000%)"),
            L("10:10:00", "You have slain a ghoul!"),
            L("10:12:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("10:13:00", "You have entered Neriak - Commons."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(0, v.Difficulty);
        Assert.Equal(2, v.Kills);
    }

    [Fact]
    public void AD0EntryFromANeighbourIsTheOpenWorldAndTheNextIsTheInstance()
    {
        // A player's Nagafen's Lair, scrubbed (visit-boundaries): out by a zone line to a neighbour, then the plain name
        // twice. A zone line lands in the open world; the instance is entered from there.
        var (_, s) = Run(
            L("10:00:00", "You have entered Nagafen's Lair."),
            L("10:00:40", "Player Venun creating instance Nagafen's Lair 101."),
            L("10:01:10", "You have entered Nagafen's Lair."),
            L("10:02:00", "You gain experience! (2.000%)"),
            L("10:02:00", "You have slain a greater kobold!"),
            L("10:02:05", "You looted a Mote of Infinitesimal Potential from a greater kobold's corpse and stored it in your currency"),
            L("10:20:00", "You gain experience! (2.000%)"),
            L("10:20:00", "You have slain a kobold!"),
            L("10:38:30", "You have entered Solusek's Eye."),
            L("10:38:45", "You have entered Nagafen's Lair."),               // the open world
            L("10:39:02", "You have entered Nagafen's Lair."),               // back in the instance
            L("10:40:00", "You gain experience! (2.000%)"),
            L("10:40:00", "You have slain a greater kobold!"),
            L("10:40:05", "You looted a Mote of Lesser Potential from Targin the Rock's corpse and stored it in your currency"),
            L("11:10:00", "You gain experience! (2.000%)"),
            L("11:10:00", "You have slain a kobold!"),
            L("11:40:00", "You gain experience! (2.000%)"),
            L("11:40:00", "You have slain a kobold!"),
            L("12:10:00", "You gain experience! (2.000%)"),
            L("12:10:00", "You have slain a kobold!"),
            L("12:30:00", "You gain experience! (2.000%)"),
            L("12:30:00", "You have slain a kobold!"),
            L("12:50:00", "You gain experience! (2.000%)"),
            L("12:50:00", "You have slain a kobold champion!"),
            L("12:51:00", "You have entered Toxxulia Forest."),
            L("13:15:00", "You have entered Toxxulia Forest."));    // later: the pause ran out, the visit is closed
        var v = Assert.Single(s.Closed);
        Assert.Equal(0, v.Difficulty);
        Assert.Equal(8, v.Kills);
        Assert.Equal(2, v.Loot.MoteCount);
        var p = Assert.Single(v.Pauses);
        Assert.Equal(new DateTime(2026, 11, 14, 10, 38, 30), p.From);
        Assert.Equal(new DateTime(2026, 11, 14, 10, 39, 2), p.To);
        Assert.Equal(new DateTime(2026, 11, 14, 12, 51, 0), v.ExitedAt);
    }

    [Fact]
    public void NothingInTheOpenWorldCountsToTheVisitItLeft()
    {
        // Walked out of a D0 crawl into the open world of the same name, and played there.
        var (_, s) = Run(
            L("10:00:00", "Player Venun creating instance The Estate of Unrest 274."),
            L("10:00:20", "You have entered The Estate of Unrest."),
            L("10:01:00", "You gain experience! (2.000%)"),
            L("10:01:00", "You have slain a ghoul!"),
            L("10:05:00", "You have entered The Estate of Unrest."),         // walked out: the open world
            L("10:06:00", "You gain experience! (2.000%)"),
            L("10:06:00", "You have slain a rat!"),
            L("10:06:05", "You looted a Mote of Infinitesimal Potential from a rat's corpse and stored it in your currency"),
            L("10:07:00", "You have been slain by a rat!"),
            L("10:07:10", "You have entered Neriak - Commons."),
            L("10:30:00", "You have entered Neriak - Commons."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(1, v.Kills);
        Assert.Equal(0, v.Loot.MoteCount);
        Assert.Equal(0, v.Deaths);
    }

    [Fact]
    public void AGroupInviteSaysTheDifficulty()
    {
        var (_, s) = Run(
            L("01:22:30", "Slasher has asked you to join the instance: Nagafen's Lair - Group 0 (Normal).        Would you like to join?"),
            L("01:22:50", "You have entered Nagafen's Lair - Group."),
            L("01:23:00", "You gain experience! (2.000%)"),
            L("01:23:00", "You have slain a kobold!"),
            L("01:29:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("01:30:00", "You have entered Lavastorm Mountains."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(0, v.Difficulty);
        Assert.Equal("Nagafen's Lair - Group", v.BaseZone);
    }

    [Fact]
    public void ANewInstanceIsANewVisit()
    {
        var (_, s) = Run(
            L("10:00:00", "You have entered Kedge Keep 4 (Refined)."),
            L("10:05:00", "You gain experience! (2.000%)"),
            L("10:05:00", "You have slain a ghoul!"),
            L("10:06:00", "You have entered Dagnor's Cauldron."),
            L("10:07:00", "Player Venun creating instance Kedge Keep 381."),
            L("10:07:30", "You have entered Kedge Keep 4 (Refined)."),
            L("10:08:00", "You gain experience! (2.000%)"),
            L("10:08:00", "You have slain a ghoul!"),
            L("10:09:00", "You have entered Dagnor's Cauldron."),
            L("10:30:00", "You have entered Oggok."));
        Assert.Equal(2, s.Closed.Count);
        Assert.Equal(1.5, s.Closed[1].ResetMinutes!.Value, 2);
    }

    [Fact]
    public void KillsAfterCompletionCountAndExtendActiveTime()
    {
        var (_, s) = Run(
            L("01:29:00", "You have entered Befallen 4 (Refined)."),
            L("01:49:19", "You gain experience! (7.249%)"),
            L("01:49:19", "You have slain a ghoul!"),
            L("01:49:19", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("01:49:48", "You gain experience! (6.884%)"),
            L("01:49:48", "You have slain a Teir`Dal shadowknight!"),
            L("01:50:19", "You have entered Befallen."));
        var v = Assert.Single(s.Closed);
        Assert.Equal(2, v.Kills);
        Assert.Equal(20 * 60 + 48, v.ActiveSeconds, 0);
    }

    [Fact]
    public void HighTierDropsAreCalledOutAndValued()
    {
        var (e, s) = Run(
            L("01:00:00", "You have entered The Plane of Hate 4 (Refined)."),
            L("01:22:13", "You looted a Fragment of Ascendant Potential from Reward Chest and stored it in your currency"),
            L("01:22:14", "You looted 4 Fragment of Major Potential from Reward Chest and stored it in your currency"),
            L("01:22:15", "You looted a Mote of Greater Potential from a spite golem's corpse and stored it in your currency"));
        Assert.Equal(2, s.High.Count);
        var v = e.State.Current!;
        // Ascendant fragment 16 x 1.6 + 4 Major fragments x 1.6 + Greater mote 2
        Assert.Equal(16 * 1.6 + 4 * 1.6 + 2, v.Loot.T5(1.6), 6);
        Assert.Equal(16 * 1.6 + 2, v.Loot.HighTierT5(1.6), 6);
        Assert.Equal(25.6 + 6.4, v.Loot.ChestT5(1.6), 6);
    }

    [Fact]
    public void StateRoundTripsThroughJson()
    {
        var (e, _) = Run(
            L("10:00:00", "You have gained a level! Welcome to level 20!"),
            L("10:00:10", "You have entered Befallen 4 (Refined)."),
            L("10:01:00", "Jarann told you, 'Attacking a ghoul Master.'"),
            L("10:01:00", "You gain experience! (30.000%)"),
            L("10:01:00", "You have slain a ghoul!"),
            L("10:02:00", "You looted a Mote of Major Potential from a ghoul's corpse and stored it in your currency"));
        var json = JsonSerializer.Serialize(e.State);
        var back = JsonSerializer.Deserialize<EngineState>(json)!;
        Assert.Equal(e.State.Level, back.Level);
        Assert.Equal(e.State.Current!.Kills, back.Current!.Kills);
        Assert.Equal(e.State.Current.Loot.MoteT5, back.Current.Loot.MoteT5);
        Assert.Contains("Jarann", back.Pets);

        // A resumed engine keeps going from the restored state.
        var s2 = new Sink();
        var e2 = new PathfinderEngine("Venun", new TrackerOptions(), s2, back);
        e2.ProcessLine(L("10:05:00", "Jarann punches a ghoul for 75 points of damage."));
        e2.ProcessLine(L("10:06:00", "You have entered Befallen."));
        Assert.Empty(s2.Closed);   // unfinished and outside: paused, could still resume
        e2.ProcessLine(L("10:20:00", "You have entered Oggok."));
        var v = Assert.Single(s2.Closed);
        Assert.Equal(75, v.Damage.Total);
    }

    [Fact]
    public void ASessionHasNoRateUntilAFinishedCrawlOrEightKills()
    {
        // Two minutes in a crawl for a task's boss and one fragment read as 124.6 T5-eq an hour on Home (a report in 0.10.3).
        string[] boss =
        [
            L("16:06:59", "You have entered The Ruins of Old Paineel - Solo 4 (Refined)."),
            L("16:08:33", "You looted a Fragment of Major Potential from Master Yael's corpse and stored it in your currency"),
            L("16:09:09", "You have entered Paineel."),
        ];
        var o = new TrackerOptions();
        var one = Session(Run(boss), At(16, 10));
        Assert.True(one.Early);
        Assert.True(one.T5 > 3, $"{one.T5} T5-eq");   // a Major fragment, 3.56 T5-eq at the recommended value
        Assert.Null(one.T5PerHourCrawl);
        Assert.Null(one.T5PerHourWall);
        Assert.Null(one.LevelsPerHourCrawl);
        // Home reads these from the snapshot (home.js farmingHero).
        var json = JsonSerializer.Serialize(one, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"early\":true", json);
        Assert.Contains("\"rateFromKills\":8", json);

        // Another few minutes there soon after is still no rate: time in crawls is not what makes one (the user, 11-18), a finished crawl or 8 kills is.
        var two = Session(Run([.. boss,
            L("16:20:00", "You have entered The Ruins of Old Paineel - Solo 4 (Refined)."),
            L("16:23:00", "You looted a Fragment of Major Potential from Master Yael's corpse and stored it in your currency"),
            L("16:24:10", "You have entered Paineel.")]), At(16, 30));
        Assert.True(two.Early);
        Assert.Equal(2 * one.T5, two.T5, 6);
        Assert.Null(two.T5PerHourCrawl);
        Assert.Null(two.T5PerHourWall);

        SessionStats Session((PathfinderEngine e, Sink s) run, DateTime now)
        {
            var visits = run.s.Closed.ToList();
            if (run.e.State.Current is { } c && !visits.Contains(c)) visits.Add(c);
            return SessionStats.From(SessionStats.CurrentSession(visits), now, o);
        }
        static DateTime At(int h, int m) => new(2026, 11, 14, h, m, 0);
    }
}
