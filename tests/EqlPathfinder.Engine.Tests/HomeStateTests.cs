using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Home's state from what the log says about the character on now (docs/specs/home-follows-loadout.md).</summary>
public sealed class HomeStateTests
{
    static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0);

    /// <summary>A reincarnation as the roster holds it: the race and the three classes in one burst, the classes at <paramref name="level"/>.</summary>
    static void Reinc(Roster r, string race, string[] classes, int daysIn, int level)
    {
        var t = T0.AddDays(daysIn);
        r.Race(race).Seen.Add(t);
        foreach (var c in classes) { var i = r.Class(c); i.Seen.Add(t.AddSeconds(1)); i.Level = level; }
    }

    /// <summary>A journey, as built from visits after a reincarnation. Only a relevel you have played has one.</summary>
    static Journey J(string trio, string race, int daysIn, int levelNow) =>
        new(trio, ClassNames.TrioKey(trio), race, T0.AddDays(daysIn).AddSeconds(1), T0.AddDays(daysIn + 1), T0.AddDays(daysIn + 2), 1, levelNow, false, 10, 12, 3, 100, 0, 0, 0, 0,
            [], null, null, null, []);

    /// <summary>
    /// What the log gives: six finished relevels, Kerran's WAR/CLR/RNG (7th) played to 32 (it has a journey), then Barbarian's
    /// BRD/DRU/NEC (8th) reincarnated and not played at all, so it has no journey (<c>Journeys.Build</c> makes one only from
    /// visits after the reincarnation).
    /// </summary>
    static (Roster, List<Journey>) TwoOpen()
    {
        var r = new Roster();
        for (int i = 0; i < 6; i++) Reinc(r, "Human", ["PAL", "MNK", "SHM"], i, 50);
        Reinc(r, "Kerran", ["WAR", "CLR", "RNG"], 10, 32);
        Reinc(r, "Barbarian", ["BRD", "DRU", "NEC"], 11, 1);
        return (r, [J("WAR/CLR/RNG", "Kerran", 10, 32)]);
    }

    static HomeNow Of(int? level, string? trio, string? race, int cap = 50)
    {
        var (r, j) = TwoOpen();
        return HomeState.Of(level, trio, race, cap, r, j);
    }

    [Fact]
    public void At_the_cap_Home_farms_and_parks_every_open_relevel_oldest_first()
    {
        var h = Of(50, "PAL/MNK/SHM", "Froglok");
        Assert.Equal("farming", h.Mode);
        Assert.Null(h.Now);
        Assert.Equal(["CLR/RNG/WAR", "BRD/DRU/NEC"], h.Parked.Select(p => p.TrioKey));
        Assert.Equal([7, 8], h.Parked.Select(p => p.Number));
        Assert.Equal([32, 1], h.Parked.Select(p => p.Level));
        Assert.Equal([0, (int?)null], h.Parked.Select(p => p.JourneyIndex));
    }

    [Fact]
    public void At_the_cap_with_the_loadout_not_known_Home_still_farms()
    {
        Assert.Equal("farming", Of(50, null, null).Mode);
    }

    [Fact]
    public void A_relevel_reincarnated_and_not_played_is_open_with_its_level_from_the_roster_and_no_journey()
    {
        var h = Of(1, "BRD/DRU/NEC", "Barbarian");
        Assert.Equal("relevel", h.Mode);
        Assert.Equal(("BRD/DRU/NEC", 8, 1, (int?)null), (h.Now!.TrioKey, h.Now.Number, h.Now.Level, h.Now.JourneyIndex));
        Assert.Equal(["CLR/RNG/WAR"], h.Parked.Select(p => p.TrioKey));
    }

    [Fact]
    public void The_one_on_now_with_a_journey_points_at_it()
    {
        var h = Of(32, "WAR/CLR/RNG", "Kerran");
        Assert.Equal(("CLR/RNG/WAR", 7, (int?)0), (h.Now!.TrioKey, h.Now.Number, h.Now.JourneyIndex));
        Assert.Equal(["BRD/DRU/NEC"], h.Parked.Select(p => p.TrioKey));
    }

    [Fact]
    public void A_relevel_of_the_same_trio_and_another_race_is_not_the_one_on_now()
    {
        var h = Of(32, "WAR/CLR/RNG", "Human");
        Assert.Equal("leveling", h.Mode);
        Assert.Null(h.Now);
    }

    /// <summary>/who under an illusion leaves the race null (Tracker.OnWho keeps only a playable race): never the newest of two.</summary>
    [Fact]
    public void With_the_race_unknown_a_relevel_counts_only_when_it_is_the_one_open_of_its_trio()
    {
        Assert.Equal("CLR/RNG/WAR", Of(32, "WAR/CLR/RNG", null).Now!.TrioKey);
        var (r, j) = TwoOpen();
        Reinc(r, "Human", ["WAR", "CLR", "RNG"], 12, 20);   // a second open relevel of the same trio
        var h = HomeState.Of(20, "WAR/CLR/RNG", null, 50, r, j);
        Assert.Equal("leveling", h.Mode);
        Assert.Null(h.Now);
    }

    [Fact]
    public void A_level_that_matches_a_relevel_is_not_enough_without_the_loadout()
    {
        var h = Of(32, null, null);
        Assert.Equal("swapped", h.Mode);
        Assert.Null(h.Now);
        Assert.Equal(2, h.Parked.Count);
    }

    [Fact]
    public void Below_the_cap_with_a_trio_that_has_no_relevel_open_Home_levels()
    {
        Assert.Equal("leveling", Of(20, "PAL/MNK/SHM", "Froglok").Mode);
    }

    [Fact]
    public void Below_the_cap_with_no_loadout_known_and_nothing_open_Home_levels()
    {
        Assert.Equal("leveling", HomeState.Of(20, null, null, 50, new Roster(), []).Mode);
    }

    [Fact]
    public void With_no_level_seen_yet_Home_asks_for_a_who_even_with_a_relevel_open()
    {
        Assert.Equal("nolevel", Of(null, null, null).Mode);
        Assert.Equal("nolevel", HomeState.Of(null, null, null, 50, new Roster(), []).Mode);
    }

    [Fact]
    public void The_next_relevel_is_numbered_after_every_relevel_started_open_ones_too()
    {
        Assert.Equal(9, Of(50, "PAL/MNK/SHM", "Froglok").NextNumber);
    }

    [Fact]
    public void When_the_cap_is_55_a_character_at_50_is_below_it()
    {
        var h = Of(50, "WAR/CLR/RNG", "Kerran", 55);
        Assert.Equal("relevel", h.Mode);
        Assert.Equal(55, h.Cap);
        Assert.Equal("leveling", Of(50, "PAL/MNK/SHM", "Froglok", 55).Mode);
        Assert.Equal("farming", Of(55, "PAL/MNK/SHM", "Froglok", 55).Mode);
    }

    /// <summary>The real path: reincarnation lines with no visit after them. Journeys.Build makes nothing from them, and Home still sees both relevels.</summary>
    [Fact]
    public void Reincarnations_in_a_log_with_no_visit_after_them_are_open_relevels()
    {
        static string L(string hms, string body) => $"[Wed Nov 11 {hms} 2026] {body}";
        static string Gain(string kind, string name) => $"You have gained the ability \"{kind} Reincarnation: {name}\" at a cost of 0 ability points.";
        var e = new PathfinderEngine("Venun", new TrackerOptions(), new Sink());
        foreach (var l in new[]
        {
            L("10:00:00", "[50 PAL/MNK/ENC] Venun (Froglok) ZONE: Oggok"),
            L("11:00:00", Gain("Race", "Kerran")), L("11:00:00", Gain("Class", "Warrior")), L("11:00:00", Gain("Class", "Cleric")), L("11:00:00", Gain("Class", "Ranger")),
            L("12:00:00", Gain("Race", "Barbarian")), L("12:00:00", Gain("Class", "Bard")), L("12:00:00", Gain("Class", "Druid")), L("12:00:00", Gain("Class", "Necromancer")),
        }) e.ProcessLine(l);
        e.Flush();
        var journeys = Journeys.Build([], e.State, new TrackerOptions());
        Assert.Empty(journeys);
        var h = HomeState.Of(50, null, null, 50, e.State.Roster, journeys);
        Assert.Equal([1, 2], h.Parked.Select(p => p.Number));
        Assert.Equal([1, 1], h.Parked.Select(p => p.Level));
        Assert.Equal(3, h.NextNumber);
    }

    /// <summary>The real log, 11-11: four reincarnations within 3 minutes, the events' classes crossing the journeys'. One relevel each.</summary>
    [Fact]
    public void A_burst_of_reincarnations_whose_classes_cross_between_events_is_one_relevel_each()
    {
        var r = new Roster();
        var t = T0.AddDays(5);
        string[][] trios = [["PAL", "MNK", "SHM"], ["WAR", "CLR", "RNG"], ["BRD", "DRU", "NEC"], ["ROG", "ENC", "MAG"]];
        string[] races = ["Human", "Kerran", "Barbarian", "Gnome"];
        for (int k = 0; k < 4; k++)
        {
            r.Race(races[k]).Seen.Add(t.AddSeconds(k * 50));
            // each class is seen nearer another event's race, so the events' classes differ from the journeys'
            foreach (var c in trios[k]) { var i = r.Class(c); i.Seen.Add(t.AddSeconds(((k + 1) % 4) * 50 + 1)); i.Level = 50; }
        }
        var journeys = Enumerable.Range(0, 4).Select(k =>
            new Journey(string.Join('/', trios[k]), ClassNames.TrioKey(trios[k]), races[k], t.AddSeconds(k * 50 + 1), t, t, 1, 50, true, 10, 12, 3, 100, 0, 0, 0, 0,
                [], null, null, null, [])).ToList();
        var all = Relevels.Of(r, journeys, 50);
        Assert.Equal(4, all.Count);
        Assert.All(all, x => Assert.NotNull(x.Journey));
    }

    [Fact]
    public void A_relevel_not_played_is_done_once_one_of_its_classes_is_reincarnated_again()
    {
        var (r, j) = TwoOpen();
        r.Class("BRD").Seen.Add(T0.AddDays(20));
        var all = Relevels.Of(r, j, 50);
        Assert.True(all.Single(x => x.Race == "Barbarian").Done);
        Assert.False(all.Single(x => x.Race == "Kerran").Done);
    }

    [Fact]
    public void A_relevel_not_played_shows_its_trio_in_key_order()
    {
        var (r, j) = TwoOpen();
        Assert.Equal("BRD/DRU/NEC", Relevels.Of(r, j, 50).Single(x => x.Race == "Barbarian").Trio);
    }

    private sealed class Sink : ITrackerSink { public void OnVisitClosed(Visit v) { } }
}
