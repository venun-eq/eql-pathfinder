using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class PlannerTests
{
    /// <summary>Every class reincarnated once except Berserker, like Venun on 11-15.</summary>
    private static EngineState Roster()
    {
        var s = new EngineState();
        foreach (var c in ClassNames.All) s.Roster.Class(c.Abbrev).Rank = c.Abbrev == "BER" ? 0 : 1;
        return s;
    }

    [Fact]
    public void DefaultPrioritiesPutACarryAndHealsInEveryTopIdea()
    {
        var ideas = Planner.Ideas(Roster(), [], 3);
        Assert.All(ideas, i => Assert.Contains("BER", i.Classes));
        Assert.All(ideas, i => Assert.Contains(i.Tags, t => t.Kind == "ok" && t.Text.StartsWith("carry")));
        Assert.All(ideas, i => Assert.Contains(i.Tags, t => t.Kind == "ok" && t.Text.StartsWith("heals")));
    }

    [Fact]
    public void AWizardCountsAsACarryWhenYouSaySo()
    {
        var s = Roster();
        var prefs = new PlanPrefs();
        prefs.Members["carry"].Add("WIZ");
        prefs = prefs.Normalized();
        var before = Planner.Evaluate(["BER", "WIZ", "CLR"], s, []);
        var after = Planner.Evaluate(["BER", "WIZ", "CLR"], s, [], prefs);
        Assert.True(after.Score > before.Score);
        Assert.Contains(after.Tags, t => t.Text == "carry: WIZ");
        Assert.Contains(before.Tags, t => t.Text == "no carry");
    }

    [Fact]
    public void TheOrderSetsTheWeights()
    {
        var tankFirst = new PlanPrefs { Order = ["tank", "carry", "heals", "proven", "burst", "control"] }.Normalized();
        var top = Planner.Ideas(Roster(), [], 1, tankFirst)[0];
        Assert.Contains(top.Classes, c => c is "WAR" or "PAL" or "SHD");
    }

    [Fact]
    public void SavedPreferencesAreRepaired()
    {
        var p = new PlanPrefs { Order = ["heals", "bogus"], Members = new() { ["carry"] = ["wizard", "XYZ"] } }.Normalized();
        Assert.Equal(6, p.Order.Count);
        Assert.Equal("heals", p.Order[0]);
        Assert.Equal(["WIZ"], p.Members["carry"]);
        Assert.Equal(PlanPrefs.DefaultMembers()["tank"], p.Members["tank"]);
    }

    [Fact]
    public void TheXpPenaltyForEveryRankIsTheEnginesAloneForThePagesToRead()
    {
        // 10% more XP to level for each rank, 4x at rank 30 and beyond: the pages read this list, they keep no formula of their own.
        Assert.Equal(Enumerable.Range(0, 31).Select(Planner.XpMultiplier), Planner.XpByRank);
        Assert.Equal(4.0, Planner.XpByRank[^1]);
    }
}
