using System.Text.Json;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// What you want first, the page's engine (docs/specs/campaign-first-lane-page.md, part 1): the preset order the ready-made goals write,
/// the presets priced as the click builds them, the clash on each Spread out row, and the lines of what came across.
/// </summary>
public class CampaignFirstLanePageEngineTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);

    private static CampaignPlan Ranked(params string[] races)
    {
        var p = Campaign.Preset("classes");
        p.ClassOrder = [];
        p.RaceOrder = [.. races.Select(r => new CampaignWant { Key = r, Target = 3 })];
        foreach (var r in races) p.Races[r] = 3;
        return p;
    }

    [Fact]
    public void EveryRaceIsTheRankedRacesThenTheFewestRanksThenTheGamesOrderWithLockedLast()
    {
        var p = Ranked("Troll", "Iksar");
        p.Locked = ["Dwarf"];
        var rr = NoRaces;
        rr["Human"] = 2; rr["Erudite"] = 1;
        p.Loadouts = Campaign.Generate(p, NoClasses, rr);
        var lane = Campaign.FirstLane(p, Campaign.Evaluate(p, NoClasses, rr, []), NoClasses, rr, [], null);

        var game = ClassNames.Races.ToList();
        var rest = game.Where(r => r is not ("Troll" or "Iksar")).OrderBy(r => r == "Dwarf").ThenBy(r => rr[r]).ThenBy(game.IndexOf);
        Assert.Equal(["Troll", "Iksar", .. rest], lane.EveryRace);
        Assert.Equal(16, lane.EveryRace!.Count);
    }

    [Fact]
    public void TheReadyMadeGoalsArePricedAsTheClickBuildsThem()
    {
        var p = Ranked("Troll", "Iksar");
        p.Locked = ["Dwarf"];
        var order = Campaign.FirstLane(p, Campaign.Evaluate(p, NoClasses, NoRaces, []), NoClasses, NoRaces, [], null).EveryRace!;

        // The click keeps the races you ranked (Troll, Iksar) and ranks no other: every race is a goal of 3/3.
        var all = new CampaignPlan { ClassOrder = [], RaceOrder = [.. p.RaceOrder!.Select(w => new CampaignWant { Key = w.Key, Target = 3 })], Locked = ["Dwarf"] };
        foreach (var c in ClassNames.All) all.Classes[c.Abbrev] = 3;
        foreach (var r in order) all.Races[r] = 3;
        all.Loadouts = Campaign.Generate(all, NoClasses, NoRaces);
        var wantAll = Campaign.Evaluate(all, NoClasses, NoRaces, []);

        var presets = Campaign.Presets(NoClasses, NoRaces, null, p);
        var got = presets.Single(x => x.Key == "all");
        Assert.Equal(wantAll.Left, got.Runs);
        Assert.True(Math.Abs(wantAll.TotalXp - got.TotalXp) < 0.005, $"Everything 3/3 is priced {got.TotalXp}, the click builds {wantAll.TotalXp}");
        Assert.Equal(wantAll.LastXp, got.LastXp, 3);   // the order shows in the last run's XP
        // Every class 3/3 leaves the races to Pathfinder: the plan it builds ranks none.
        var cls = new CampaignPlan { ClassOrder = [], RaceOrder = [] };
        foreach (var c in ClassNames.All) cls.Classes[c.Abbrev] = 3;
        cls.Loadouts = Campaign.Generate(cls, NoClasses, NoRaces);
        Assert.Equal(Campaign.Evaluate(cls, NoClasses, NoRaces, []).TotalXp, presets.Single(x => x.Key == "classes").TotalXp, 3);
    }

    [Fact]
    public void TheReadyMadeGoalsKeepTheClassesYouRankedAndTheirUnlockNowBecauseTheClickLeavesThemAlone()
    {
        var p = Ranked("Troll");
        p.ClassOrder = [new() { Key = "ROG", Target = 3, UnlockNow = true }, new() { Key = "SHD", Target = 3, UnlockNow = true }, new() { Key = "BRD", Target = 3 }];
        foreach (var c in new[] { "ROG", "SHD", "BRD" }) p.Classes[c] = 3;
        var rr = NoRaces; rr["Human"] = 2;

        var cls = new CampaignPlan { ClassOrder = [.. p.ClassOrder.Select(w => w.Copy())], RaceOrder = [] };
        foreach (var c in ClassNames.All) cls.Classes[c.Abbrev] = 3;
        cls.Loadouts = Campaign.Generate(cls, NoClasses, rr);
        var want = Campaign.Evaluate(cls, NoClasses, rr, []);

        var got = Campaign.Presets(NoClasses, rr, null, p).Single(x => x.Key == "classes");
        Assert.Equal(want.Left, got.Runs);
        Assert.True(Math.Abs(want.TotalXp - got.TotalXp) < 0.005, $"Every class 3/3 is priced {got.TotalXp}, the click builds {want.TotalXp}");
    }

    [Fact]
    public void TheReadyMadeGoalsCountTheRunsToPlayFromHereKeepingThePlansPinsAndPlayedRuns()
    {
        // The count on a ready-made goal is the number the plan shows after the click: pinned runs and runs you played stay.
        var p = Ranked("Troll");
        foreach (var c in ClassNames.All) p.Classes[c.Abbrev] = 3;
        p.Loadouts = [new() { Race = "Human", Classes = ["WAR", "CLR", "PAL"], Times = 4, Pinned = true }];
        var bare = Campaign.Presets(NoClasses, NoRaces, null, new CampaignPlan { ClassOrder = [], RaceOrder = [] }).Single(x => x.Key == "classes");

        // A reincarnation played since the plan began, and a Must combo: the click keeps both (RebuildCampaign), so the count must too.
        p.Since = new DateTime(2026, 11, 1);
        p.Combos = [new() { Classes = ["BRD", "WAR"], Must = true, EveryRun = true }];
        var events = new[] { new ReincarnationEvent(new DateTime(2026, 11, 2), "Human", ["WAR", "CLR", "PAL"]) };

        var cls = new CampaignPlan { ClassOrder = [], RaceOrder = [], Loadouts = [.. p.Loadouts.Select(l => l.Copy())], Since = p.Since, Combos = [.. p.Combos.Select(c => c.Copy())] };
        foreach (var c in ClassNames.All) cls.Classes[c.Abbrev] = 3;
        var (done, off) = Campaign.DoneCounts(cls, events);
        cls.Loadouts = Campaign.Generate(cls, NoClasses, NoRaces, done, null, off);
        var want = Campaign.Evaluate(cls, NoClasses, NoRaces, events);
        var noCombo = new CampaignPlan { ClassOrder = [], RaceOrder = [], Loadouts = [.. p.Loadouts.Select(l => l.Copy())], Since = p.Since };
        foreach (var c in ClassNames.All) noCombo.Classes[c.Abbrev] = 3;
        var (d2, o2) = Campaign.DoneCounts(noCombo, events);
        noCombo.Loadouts = Campaign.Generate(noCombo, NoClasses, NoRaces, d2, null, o2);
        var plain = Campaign.Evaluate(noCombo, NoClasses, NoRaces, events);
        Assert.True(plain.Left != want.Left || plain.TotalXp != want.TotalXp, $"the combo should move the price ({want.Left} runs, {want.TotalXp})");   // the combo moves the price, so the test can tell

        var got = Campaign.Presets(NoClasses, NoRaces, null, p, events).Single(x => x.Key == "classes");
        Assert.Equal(want.Left, got.Runs);
        Assert.True(Math.Abs(want.TotalXp - got.TotalXp) < 0.005, $"Every class 3/3 is priced {got.TotalXp}, the click builds {want.TotalXp}");
        Assert.True(got.Runs > bare.Runs, $"{got.Runs} runs with the pin, {bare.Runs} without: the pinned runs are kept, not rebuilt");
    }

    private static CampaignPlan Pinned(bool unlockNow)
    {
        var p = Campaign.Preset("classes");
        p.ClassOrder = [new() { Key = "ROG", Target = 3, UnlockNow = unlockNow }];
        p.Classes["ROG"] = 3;
        p.Loadouts =
        [
            new() { Race = "Half Elf", Classes = ["WAR", "RNG", "ROG"], Times = 1, Pinned = true },
            new() { Race = "Human", Classes = ["CLR", "ENC", "MAG"], Times = 1, Pinned = true },
        ];
        return p;
    }

    [Fact]
    public void ASpreadOutRowThatWouldClashWithAPinCarriesItsClash()
    {
        var p = Pinned(false);
        var lane = Campaign.FirstLane(p, Campaign.Evaluate(p, NoClasses, NoRaces, []), NoClasses, NoRaces, [], null);
        var rog = lane.Classes.Single();
        Assert.Equal(0, rog.ClashLoadout);
        Assert.StartsWith("Clashes with your pinned loadout 1 (Half Elf · WAR / RNG / ROG): it seats ROG", rog.Clash);
    }

    [Fact]
    public void AnUnlockNowRowAndARowWithNothingToClashWithCarryNoClash()
    {
        var p = Pinned(true);
        var lane = Campaign.FirstLane(p, Campaign.Evaluate(p, NoClasses, NoRaces, []), NoClasses, NoRaces, [], null);
        Assert.Null(lane.Classes.Single().Clash);   // the click that set it already told the player; the row says nothing more
        p = Pinned(false);
        p.Loadouts[1].Pinned = false;
        lane = Campaign.FirstLane(p, Campaign.Evaluate(p, NoClasses, NoRaces, []), NoClasses, NoRaces, [], null);
        Assert.Null(lane.Classes.Single().Clash);
    }
}
