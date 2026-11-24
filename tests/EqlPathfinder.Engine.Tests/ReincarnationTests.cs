using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class ReincarnationTests
{
    private sealed class Sink : ITrackerSink
    {
        public List<Visit> Closed { get; } = new();
        public void OnVisitClosed(Visit v) => Closed.Add(v);
    }

    private static string L(string hms, string body, int day = 11) => $"[Wed Nov {day:00} {hms} 2026] {body}";

    private static (PathfinderEngine e, Sink s) Run(params string[] lines)
    {
        var s = new Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in lines) e.ProcessLine(l);
        e.Flush();
        return (e, s);
    }

    [Fact]
    public void ReincarnationRaisesRankAndResetsLevel()
    {
        var (e, _) = Run(
            L("10:00:00", "[50 PAL/MNK/ENC] Venun (Froglok) ZONE: Oggok"),
            L("11:59:39", "You have gained the ability \"Race Reincarnation: Froglok\" at a cost of 0 ability points."),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Enchanter\" at a cost of 0 ability points."));
        var r = e.State.Roster;
        Assert.Equal(1, r.Classes["PAL"].Rank);
        Assert.Equal(1, r.Classes["PAL"].Level);
        Assert.Equal(1, r.Races["Froglok"].Rank);
        // The trio is unknown until the next /who: a reincarnation often comes with a loadout swap.
        Assert.Null(e.State.Classes);
    }

    private const string PalReinc = "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points.";

    [Fact]
    public void ASecondReincarnationStatesItsRank()
    {
        // As in Venun's log: SHD reincarnated on the 10th, again on the 16th ("improved … 2"), with BER for the first time.
        var (e, _) = Run(
            L("17:12:00", "You have gained the ability \"Class Reincarnation: Shadow Knight\" at a cost of 0 ability points.", day: 10),
            L("17:24:45", "You have gained the ability \"Race Reincarnation: Human\" at a cost of 0 ability points.", day: 16),
            L("17:24:45", "You have improved Class Reincarnation: Shadow Knight 2 at a cost of 0 ability points.", day: 16),
            L("17:24:45", "You have improved Class Reincarnation: Rogue 2 at a cost of 0 ability points.", day: 16),
            L("17:24:45", "You have gained the ability \"Class Reincarnation: Berserker\" at a cost of 0 ability points.", day: 16),
            L("17:24:45", "You have gained the ability \"Reincarnation: Completionist\" at a cost of 0 ability points.", day: 16));
        var r = e.State.Roster;
        Assert.Equal(2, r.Classes["SHD"].Rank);
        Assert.Equal(2, r.Classes["SHD"].Seen.Count);
        Assert.Equal(2, r.Classes["ROG"].Rank);       // its first was before this log: the line says 2
        Assert.Equal(1, r.Classes["BER"].Rank);
        Assert.Equal(1, r.Races["Human"].Rank);
        Assert.Equal(1, r.Classes["SHD"].Level);
        // The run that starts now is rank 2 (1.2x XP); the SHD relevel of the 10th was rank 1.
        Assert.Equal(2, Journeys.RankAt(r.Classes["SHD"], new DateTime(2026, 11, 16, 17, 37, 0)));
        Assert.Equal(1, Journeys.RankAt(r.Classes["SHD"], new DateTime(2026, 11, 10, 17, 30, 0)));
        Assert.Equal(1.2, Planner.XpMultiplier(2), 3);
    }

    [Fact]
    public void AStatedRankBeatsAnOlderEntryAndANewerEntryBeatsIt()
    {
        var o = new TrackerOptions();
        o.EnteredCounts["class:ROG"] = new EnteredCount(1, new DateTime(2026, 11, 10, 12, 0, 0));
        var e = new PathfinderEngine("Venun", o, new Sink());
        e.ProcessLine(L("11:58:15", "You have improved Class Reincarnation: Rogue 3 at a cost of 0 ability points."));
        Assert.Equal(3, e.State.Roster.Classes["ROG"].Rank);                     // the line is newer than the entry
        e.State.Roster.Apply(new Dictionary<string, EnteredCount> { ["class:ROG"] = new(5, new DateTime(2026, 11, 12)) });
        Assert.Equal(5, e.State.Roster.Classes["ROG"].Rank);                     // an entry made after it wins
        e.State.Roster.Apply(null);
        Assert.Equal(3, e.State.Roster.Classes["ROG"].Rank);
    }

    [Fact]
    public void AnEnteredCountAddsTheLogsLaterReincarnations()
    {
        // A friend reincarnated PAL once with logging off, entered 1 on the 10th, then reincarnated it again with logging on.
        var o = new TrackerOptions();
        o.EnteredCounts["class:PAL"] = new EnteredCount(1, new DateTime(2026, 11, 10, 12, 0, 0));
        var e = new PathfinderEngine("Venun", o, new Sink());
        e.ProcessLine(L("11:59:39", PalReinc));
        Assert.Equal(2, e.State.Roster.Classes["PAL"].Rank);
        Assert.Single(e.State.Roster.Classes["PAL"].Seen);
    }

    [Fact]
    public void AnEnteredCountAlreadyIncludesEarlierLogLines()
    {
        // Entered as "2 so far" after the log saw one: that one is part of the 2, not on top of it.
        var (e, _) = Run(L("11:59:39", PalReinc));
        var entered = new Dictionary<string, EnteredCount> { ["class:PAL"] = new(2, new DateTime(2026, 11, 12)) };
        e.State.Roster.Apply(entered);
        Assert.Equal(2, e.State.Roster.Classes["PAL"].Rank);
        // A class the log never saw gets its entered count; clearing the entries goes back to the log alone.
        entered["race:Ogre"] = new(1, new DateTime(2026, 11, 12));
        e.State.Roster.Apply(entered);
        Assert.Equal(1, e.State.Roster.Races["Ogre"].Rank);
        e.State.Roster.Apply(null);
        Assert.Equal(1, e.State.Roster.Classes["PAL"].Rank);
        Assert.Equal(0, e.State.Roster.Races["Ogre"].Rank);
    }

    [Fact]
    public void ADingOutsideAnInstanceMovesTheRelevelOnBeforeTheCrawlCloses()
    {
        // 11-15: left Kedge Keep 4 for the open zone, dinged 46 there, and the relevel stayed at 45 until the next zone.
        var (e, s) = Run(
            L("11:59:39", PalReinc),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Enchanter\" at a cost of 0 ability points."),
            L("12:00:00", "[1 PAL/MNK/ENC] Venun (Iksar) ZONE: Kedge Keep"),
            L("12:00:10", "You have entered Kedge Keep 4 (Refined)."),
            L("12:05:00", "You gain experience! (60.000%)"),
            L("12:05:00", "You have slain a stiletto tooth piranha!"),
            L("12:10:00", "You gain experience! (30.000%)"),
            L("12:10:00", "You have slain a stiletto tooth piranha!"),
            L("12:12:00", "You have entered Kedge Keep."),
            L("12:13:00", "You have gained a level! Welcome to level 2!"),
            L("12:13:00", "You gain experience! (12.000%)"),
            L("12:13:00", "You have slain a cauldron shark!"));
        Assert.Empty(s.Closed); // The crawl is only paused: you might go back in.
        var visits = new List<Visit>(s.Closed);
        if (e.State.Current is { } cur) visits.Add(cur);
        var j = Assert.Single(Journeys.Build(visits, e.State, new TrackerOptions()));
        Assert.Equal(2, j.LevelNow);
        Assert.Equal(2, j.Curve[^1].Level);
    }

    [Fact]
    public void ALoadoutAlreadyAtTheCapIsNotARelevel()
    {
        // 11-17: PAL/MNK/SHM, all three at 50 from earlier relevels, swapped in for a Plane of Hate solo and /who'd. It showed
        // as a 0.1 h relevel from 50 to 50, the "best" one.
        var (e, _) = Run(
            L("11:59:39", PalReinc),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Shaman\" at a cost of 0 ability points."));
        Visit V(int day, int from, int to) => new()
        {
            Id = $"d{day}",
            Zone = "Befallen 4",
            BaseZone = "Befallen",
            Difficulty = 4,
            Start = new DateTime(2026, 11, day, 14, 0, 0),
            ActiveSeconds = 1800,
            Closed = true,
            Completed = true,
            Trio = "PAL/MNK/SHM",
            TrioConfirmed = true,
            Race = "Froglok",
            LevelStart = from,
            LevelEnd = to,
        };
        Assert.Empty(Journeys.Build([V(17, 50, 50)], e.State, new TrackerOptions()));
        // The relevel that did start low still counts, and farming after it stays out of it.
        var j = Assert.Single(Journeys.Build([V(12, 1, 50), V(17, 50, 50)], e.State, new TrackerOptions()));
        Assert.Equal((1, 50, 1), (j.LevelFrom, j.LevelNow, j.Crawls));
    }

    [Fact]
    public void DingsAndWhoRaiseActiveMembersOnly()
    {
        var (e, _) = Run(
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            L("12:24:00", "[1 PAL/MNK/ENC] Venun (Iksar) ZONE: Befallen"),
            L("12:30:00", "You have gained a level! Welcome to level 2!"),
            L("12:35:00", "You have gained a level! Welcome to level 3!"));
        var r = e.State.Roster;
        Assert.Equal(3, r.Classes["PAL"].Level);
        Assert.Equal(3, r.Classes["ENC"].Level);
        Assert.Equal(3, r.Races["Iksar"].Level);
        Assert.False(r.Classes.ContainsKey("WAR"));
    }

    [Fact]
    public void IllusionRaceIsIgnored()
    {
        var (e, _) = Run(L("12:24:00", "[30 WAR/ROG/CLR] Venun (Ancient Skeleton) ZONE: Kedge"));
        Assert.Null(e.State.Race);
        Assert.Equal("WAR/ROG/CLR", e.State.Trio);
    }

    [Fact]
    public void XpRuleUsesTheHighestRank()
    {
        Assert.Equal(1.1, Planner.XpMultiplier(1), 6);
        Assert.Equal(1.2, Planner.XpMultiplier(2), 6);
        Assert.Equal(4.0, Planner.XpMultiplier(40), 6);   // capped at +300%
        var s = new EngineState();
        s.Roster.Class("BER").Rank = 0;
        s.Roster.Class("RNG").Rank = 1;
        s.Roster.Class("DRU").Rank = 1;
        var idea = Planner.Evaluate(["BER", "RNG", "DRU"], s, []);
        Assert.Equal(2, idea.RankAfter);
        Assert.Contains(idea.Pros, p => p.Contains("RNG bow"));
        Assert.Contains(idea.Pros, p => p.Contains("DRU"));
    }

    [Fact]
    public void CrawlsBeforeTheFirstWhoTakeTheNextConfirmedTrio()
    {
        Visit V(string id, int from, int to, string? trio) => new()
        {
            Id = id,
            Start = DateTime.Parse($"2026-11-11 12:{id}"),
            LevelStart = from,
            LevelEnd = to,
            Trio = trio,
            Difficulty = 4,
            Closed = true,
        };
        var visits = new List<Visit> { V("10", 1, 5, null), V("20", 5, 11, null), V("30", 11, 14, "PAL/MNK/ENC"), V("40", 30, 31, null) };
        var who = Journeys.Attribute(visits);
        Assert.Equal("PAL/MNK/ENC", who["10"].Trio);
        Assert.Equal("PAL/MNK/ENC", who["20"].Trio);
        Assert.Null(who["40"].Trio);   // nothing confirms it afterwards
    }

    [Fact]
    public void PassivesComeFromTheGameFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-dbstr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "dbstr_us.txt"),
        [
            "26000^1^Class Reincarnation: Warrior^0^",
            "26000^4^This passive ability reduces incoming melee damage by #1%.^0^",
            "26127^1^Race Reincarnation: Ogre^0^",
            "26127^4^This passive ability increases your chance to resist any stun by #1%.^0^",
        ]);
        var p = GameData.LoadReincarnationPassives(dir);
        Assert.Equal("Reduces incoming melee damage.", p["WAR"].Description);
        Assert.Equal("Increases your chance to resist any stun.", p["Ogre"].Description);
        Directory.Delete(dir, true);
    }

    // The amounts aren't in the game's files: Roster read "by X%", which looked unfinished. Every shape dbstr_us.txt has.
    [Theory]
    [InlineData("This passive ability increases your base stat caps by #1.", "Increases your base stat caps.")]
    [InlineData("This passive ability will increase the range of your spells and ranged attacks by #1 units.",
        "Increases the range of your spells and ranged attacks.")]
    [InlineData("This passive ability increases your hitpoint regeneration by #1 and your base armor class by #3.",
        "Increases your hitpoint regeneration and your base armor class.")]
    [InlineData("This passive ability will increase your endurance regeneration by #1 and grants an additional #2 points to your Safe Fall skill.",
        "Increases your endurance regeneration and grants additional points to your Safe Fall skill.")]
    [InlineData("This passive ability grants you an additional #1% chance to resist charm and mesmerization spells.",
        "Grants you an additional chance to resist charm and mesmerization spells.")]
    [InlineData("This passive ability will mitigate the effects of any negative movement speed effect by #1%.",
        "Mitigates the effects of any negative movement speed effect.")]
    [InlineData("This passive ability increases the damage of your melee by #1%. You can raise your rank in this ability by completing all Class Reincarnations.",
        "Increases the damage of your melee. You can raise your rank in this ability by completing all Class Reincarnations.")]
    [InlineData("This ability, when activated, triggers <c \"#66CCFF\">Evader's Invisibility</c>.<br>As a passive effect, this ability increases your movement speed by #1%.",
        "This ability, when activated, triggers Evader's Invisibility. As a passive effect, this ability increases your movement speed.")]
    [InlineData("", "")]
    public void PassivesSayWhatTheyDoWithoutTheMissingAmounts(string raw, string shown)
    {
        Assert.Equal(shown, GameData.PassiveText(raw));
        Assert.DoesNotContain("#", GameData.PassiveText(raw));
    }
}
