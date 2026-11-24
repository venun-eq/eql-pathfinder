using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// A race can be a goal without a rank (docs/specs/campaign-race-goals.md): the plan holds only what the player overrides, so a
/// race's goal no longer puts it in the ranked list, and Pathfinder places an unranked goal where it costs the least.
/// </summary>
public class CampaignRaceGoalsTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);
    private static CampaignWant W(string key, int target = 3, bool now = false) => new() { Key = key, Target = target, UnlockNow = now };
    private static string Wants(IEnumerable<CampaignWant>? list) =>
        list == null ? "(none)" : string.Join(", ", list.Select(w => $"{w.Key} {w.Target}{(w.UnlockNow ? " now" : "")}"));
    private static string Seated(CampaignView v) => string.Join("; ", v.Runs.Select(r => $"{r.N}:{r.Race} {string.Join("/", r.Classes)}"));
    private static string RacesInOrder(CampaignView v) => string.Join(", ", v.Runs.Select(r => r.Race).Distinct());

    /// <summary>The new plan with these race goals and only <paramref name="ranked"/> in the list, read as the store reads it.</summary>
    private static CampaignPlan Goals(string? preset, (string Race, int Goal)[] goals, params CampaignWant[] ranked)
    {
        var p = preset == null ? new CampaignPlan() : Campaign.Preset(preset);
        p.ClassOrder = [];
        p.RaceOrder = [.. ranked];
        foreach (var (r, g) in goals) p.Races[r] = g;
        return p.Normalized();
    }

    private static CampaignView Planned(CampaignPlan p, Dictionary<string, int>? cr = null, Dictionary<string, int>? rr = null)
    {
        p.Loadouts = Campaign.Generate(p, cr ?? NoClasses, rr ?? NoRaces);
        return Campaign.Evaluate(p, cr ?? NoClasses, rr ?? NoRaces, []);
    }

    [Fact]
    public void ARaceGoalWithoutARankStaysOutOfTheList()
    {
        var p = Goals(null, [("Troll", 2), ("Iksar", 3)], W("Troll", 2));
        Assert.Equal("Troll 2", Wants(p.RaceOrder));
        Assert.Equal(3, p.Races.GetValueOrDefault("Iksar"));
        Assert.Equal(2, p.Races.GetValueOrDefault("Troll"));
    }

    [Fact]
    public void UnrankedRaceGoalsGoFewestRanksFirstThenTheGamesOrder()
    {
        var rr = NoRaces;
        rr["Human"] = 2; rr["Erudite"] = 1;
        var p = Goals(null, [("Human", 3), ("Barbarian", 3), ("Erudite", 3), ("Troll", 3), ("Dwarf", 3)]);
        p.Locked = ["Dwarf"];
        var v = Planned(p, rr: rr);
        // Nothing ranked: no rank now first (Barbarian, Troll, in the game's order), then Erudite at 1, Human at 2, Dwarf locked last.
        Assert.Equal("Barbarian, Troll, Erudite, Human, Dwarf", RacesInOrder(v));
    }

    [Fact]
    public void ARankedRaceGoesAheadOfAnUnrankedGoalThatCostsTheSame()
    {
        // The race runs: Troll ranked, Barbarian (first in the game's order, same ranks) only a goal.
        var races = Planned(Goals(null, [("Barbarian", 1), ("Troll", 1)], W("Troll", 1)));
        Assert.Equal("Troll, Barbarian", RacesInOrder(races));
        // The class runs' race seat: the first run takes the ranked race when both cost the same.
        var seat = Planned(Goals("classes", [("Barbarian", 1), ("Troll", 1)], W("Troll", 1)));
        Assert.True(seat.Runs[0].Race == "Troll", $"the first class run seats {seat.Runs[0].Race}, not the ranked Troll: {Seated(seat)}");
    }

    [Fact]
    public void EverythingThreeOfThreeWithNothingRankedPlansTheRunsItDidWhenItRankedEveryRace()
    {
        var rr = NoRaces;
        rr["Human"] = 2; rr["Erudite"] = 1; rr["Iksar"] = 1; rr["Ogre"] = 3;
        var cr = NoClasses;
        cr["ROG"] = 1; cr["WAR"] = 2;
        List<string> locked = ["Dwarf"];

        // 0.10.23's Everything 3/3: every race ranked at 3/3 in the preset's order.
        var before = Campaign.Preset("all");
        before.Locked = [.. locked];
        before.ClassOrder = [];
        before.RaceOrder = [.. Campaign.EveryRace(null, locked, rr).Select(r => W(r))];
        var then = Planned(before, cr, rr);

        // Now: every race's goal 3/3, none ranked.
        var after = Campaign.Preset("all");
        after.Locked = [.. locked];
        after.ClassOrder = [];
        after.RaceOrder = [];
        after = after.Normalized();
        Assert.True(after.RaceOrder is { Count: 0 }, $"Everything 3/3 ranks races: {Wants(after.RaceOrder)}");
        var now = Planned(after, cr, rr);

        Assert.Equal(Seated(then), Seated(now));
        Assert.Equal(then.Left, now.Left);
        Assert.True(Math.Abs(then.TotalXp - now.TotalXp) < 0.005, $"XP to level {now.TotalXp}, 0.10.23's {then.TotalXp}");
        Assert.Equal(then.LastXp, now.LastXp, 3);
    }

    [Fact]
    public void EverythingThreeOfThreeKeepsTheRacesYouRankedAndRanksNoOther()
    {
        var ranked = Goals(null, [("Troll", 2), ("Iksar", 3)], W("Troll", 2), W("Iksar", 3, true));
        ranked.Locked = ["Dwarf"];
        var presets = Campaign.Presets(NoClasses, NoRaces, null, ranked);
        var all = presets.Single(x => x.Key == "all");

        // The price is that of the plan the click builds: every race's goal 3/3, Troll then Iksar (Unlock now kept) the only ranked.
        var built = Campaign.Preset("all");
        built.Locked = ["Dwarf"];
        built.ClassOrder = [];
        built.RaceOrder = [W("Troll"), W("Iksar", 3, true)];
        var v = Planned(built.Normalized());
        Assert.Equal(v.Left, all.Runs);
        Assert.Equal(v.TotalXp, all.TotalXp, 3);
        Assert.Equal(v.LastXp, all.LastXp, 3);
    }
}
