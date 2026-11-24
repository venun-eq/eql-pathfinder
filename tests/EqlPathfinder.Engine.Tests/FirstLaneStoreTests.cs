using System.Text.Json;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// What you want first's store (docs/specs/campaign-first-lane.md, branch A): the plan keeps ranked lists of classes and
/// races, and a page edit is read back as it was sent.
/// </summary>
public class FirstLaneStoreTests
{
    /// <summary>settings.json as the app reads and writes it: these fields, the rest carried as they are.</summary>
    private sealed class SettingsFile : IPlannerSettings
    {
        public Dictionary<string, CampaignPlan> Campaigns { get; set; } = new();
        public Dictionary<string, NewCampaignPlan> NewCampaigns { get; set; } = new();
        [JsonExtensionData] public Dictionary<string, JsonElement>? Rest { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static SettingsFile Saved(SettingsFile s) => JsonSerializer.Deserialize<SettingsFile>(JsonSerializer.Serialize(s, Json), Json)!;
    private static string Of(CampaignPlan p) => JsonSerializer.Serialize(p, Json);

    private const string Who = "Standin_Server";

    private static string Wants(IEnumerable<CampaignWant>? list) =>
        list == null ? "(none)" : string.Join(", ", list.Select(w => $"{w.Key} {w.Target}{(w.UnlockNow ? " now" : "")}"));

    private static Dictionary<string, int> Ranks(params (string Race, int Rank)[] ranks)
    {
        var r = ClassNames.Races.ToDictionary(x => x, _ => 0);
        foreach (var (race, rank) in ranks) r[race] = rank;
        return r;
    }

    /// <summary>An edit as today's page sends it: the race pips' goals and "Earn these first", as in 0.10.22.</summary>
    private static CampaignPlan PageEdit(Dictionary<string, int> races, params string[] first) => new()
    {
        Classes = new(StringComparer.OrdinalIgnoreCase) { ["ROG"] = 3, ["MAG"] = 2 },
        Races = new(races, StringComparer.OrdinalIgnoreCase),
        First = [.. first],
    };

    [Fact]
    public void TodaysPageEditsAreKeptAsRankedListsAndReadBackAsBefore()
    {
        var s = new SettingsFile();
        PlannerSwitch.Store(s, Who, PageEdit(new() { ["Iksar"] = 3, ["Troll"] = 2, ["Human"] = 1 }, "race:Troll", "class:ROG"));
        var p = PlannerSwitch.PlanFor(Saved(s), Who);

        Assert.Equal("Troll 2", Wants(p.RaceOrder));   // Earn these first's race is ranked; the other goals are only goals
        Assert.Equal("ROG 3", Wants(p.ClassOrder));
        Assert.Equal(["race:Troll", "class:ROG"], p.First);               // the page reads what it wrote
        Assert.Equal("Human 1, Iksar 3, Troll 2", string.Join(", ", p.Races.OrderBy(r => r.Key).Select(r => $"{r.Key} {r.Value}")));
    }

    [Fact]
    public void AnEarnTheseFirstEditIsPlannedFromTheNewListsNotTheOldOnes()
    {
        var s = new SettingsFile();
        var first = PageEdit(new());
        foreach (var c in new[] { "WAR", "CLR", "PAL", "RNG" }) first.Classes[c] = 3;
        PlannerSwitch.Store(s, Who, first);
        var edit = PlannerSwitch.PlanFor(Saved(s), Who);
        edit.ClassOrder!.Add(new() { Key = "ROG" });   // the page ranks ROG and sends a rebuild: the lists, never First
        PlannerSwitch.WithLists(s, Who, edit);
        Assert.Equal("ROG 3", Wants(edit.ClassOrder));
        Assert.Equal(["class:ROG"], edit.First);   // First follows the lists
        var runs = Campaign.Generate(edit, ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0), ClassNames.Races.ToDictionary(r => r, _ => 0));
        Assert.Contains("ROG", runs[0].Classes);
    }

    [Fact]
    public void ARaceWhoseGoalIsTakenAwayLeavesTheListAndTheRestKeepTheirOrder()
    {
        var s = new SettingsFile();
        var ranked = PageEdit(new() { ["Iksar"] = 3, ["Troll"] = 2, ["Human"] = 1 }, "class:ROG");
        ranked.ClassOrder = [new() { Key = "ROG" }];
        ranked.RaceOrder = [new() { Key = "Troll" }, new() { Key = "Human" }, new() { Key = "Iksar" }];
        PlannerSwitch.Store(s, Who, ranked);
        var edit = PlannerSwitch.PlanFor(Saved(s), Who);   // the page edits the plan it was sent, lists and all
        edit.Races.Remove("Human");
        edit.Races["Erudite"] = 1;   // a goal set on the tile: not ranked
        edit.ClassOrder!.Clear();   // ROG leaves the list, keeping its goal
        PlannerSwitch.Store(s, Who, edit);
        var p = PlannerSwitch.PlanFor(Saved(s), Who);

        Assert.Equal("Troll 2, Iksar 3", Wants(p.RaceOrder));
        Assert.Equal(1, p.Races["Erudite"]);
        Assert.True(p.ClassOrder is null or { Count: 0 }, $"ROG is still ranked after Earn these first was cleared: {Wants(p.ClassOrder)}");
        Assert.Equal(["race:Troll", "race:Iksar"], p.First);   // First follows the lists
    }

    [Fact]
    public void TakingARaceOutOfTheListKeepsItsGoal()
    {
        var s = new SettingsFile();
        var ranked = PageEdit(new() { ["Iksar"] = 3, ["Troll"] = 2 });
        ranked.ClassOrder = [];
        ranked.RaceOrder = [new() { Key = "Troll" }, new() { Key = "Iksar" }];
        PlannerSwitch.Store(s, Who, ranked);
        var edit = PlannerSwitch.PlanFor(Saved(s), Who);
        edit.RaceOrder!.RemoveAll(w => w.Key == "Troll");   // Leave it to Pathfinder: the page sends the list without Troll, its goal kept
        PlannerSwitch.Store(s, Who, edit);
        var p = PlannerSwitch.PlanFor(Saved(s), Who);

        Assert.Equal("Iksar 3", Wants(p.RaceOrder));
        Assert.Equal(2, p.Races.GetValueOrDefault("Troll"));
    }

    // ------------------------------------------------------------------ a plan saved by 0.10.23

    private const string Emptied = "Your race list was every race at 3/3: that's your goal now, and Pathfinder picks the order. Rank a race to put it first.";

    /// <summary>The new plan as 0.10.23 saved it after Everything 3/3: every goal 3/3, ROG ranked, all 16 races ranked at 3/3 in the preset's order.</summary>
    private static CampaignPlan EveryRaceRanked(Action<List<CampaignWant>>? change = null)
    {
        var p = Campaign.Preset("all");
        p.ClassOrder = [new() { Key = "ROG", Target = 3 }];
        var list = Campaign.EveryRace(null, [], Ranks()).Select(r => new CampaignWant { Key = r, Target = 3 }).ToList();
        change?.Invoke(list);
        p.RaceOrder = list;
        foreach (var w in list) p.Races[w.Key] = w.Target;
        foreach (var r in ClassNames.Races.Where(r => !list.Any(w => w.Key == r))) p.Races.Remove(r);
        p.Loadouts = Campaign.Generate(p, ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0), Ranks());
        p.Since = new DateTime(2026, 11, 19);
        return p;
    }

    /// <summary>settings.json as 0.10.23 left it: the switch on, the card seen, and the plan with its lists.</summary>
    private static SettingsFile SavedBy0_10_23(CampaignPlan plan)
    {
        var s = new SettingsFile();
        s.NewCampaigns[Who] = new NewCampaignPlan { Plan = plan };
        return Saved(s);
    }

    private static NewPlannerView View(SettingsFile s) => NewPlannerView.Of(PlannerSwitch.PageInput(s, Who), new SurvivalCheck("off", null, 0), []);

    [Fact]
    public void APlanSavedWithEveryRaceRankedAtThreeOfThreeReadsWithItsListEmptyOnce()
    {
        var saved = EveryRaceRanked();
        var s = SavedBy0_10_23(saved);
        var p = PlannerSwitch.PlanFor(s, Who);

        Assert.Equal("", Wants(p.RaceOrder));
        Assert.All(ClassNames.Races, r => Assert.Equal(3, p.Races.GetValueOrDefault(r)));
        Assert.Equal("ROG 3", Wants(p.ClassOrder));
        Assert.Equal(saved.Loadouts.Count, p.Loadouts.Count);
        Assert.Equal("", Wants(PlannerSwitch.PlanFor(s, Who).RaceOrder));   // a second read too

        // The player ranks all 16 at 3/3 by hand afterwards: that list stays.
        p.RaceOrder = [.. ClassNames.Races.Select(r => new CampaignWant { Key = r, Target = 3 })];
        PlannerSwitch.Store(s, Who, p);
        s = Saved(s);
        Assert.Equal(16, PlannerSwitch.PlanFor(s, Who).RaceOrder!.Count);
        Assert.Null(View(s).RaceListEmptied);
    }

    [Fact]
    public void AnyOtherSavedRaceListStaysAsStored()
    {
        foreach (var (why, change) in new (string, Action<List<CampaignWant>>)[]
        {
            ("15 ranked", l => l.RemoveAt(l.Count - 1)),
            ("one Unlock now", l => l[3].UnlockNow = true),
            ("one at 2/3", l => l[5].Target = 2),
        })
        {
            var saved = EveryRaceRanked(change);
            var s = SavedBy0_10_23(saved);
            Assert.True(Wants(saved.RaceOrder) == Wants(PlannerSwitch.PlanFor(s, Who).RaceOrder), $"{why}: the list changed to \"{Wants(PlannerSwitch.PlanFor(s, Who).RaceOrder)}\"");
            Assert.Null(View(s).RaceListEmptied);
        }
    }

    [Fact]
    public void TheEmptiedListSaysSoUntilThePlanChangesOrCampaignIsLeft()
    {
        var s = SavedBy0_10_23(EveryRaceRanked());
        Assert.Equal(Emptied, View(s).RaceListEmptied);
        Assert.Equal(Emptied, View(Saved(s)).RaceListEmptied);   // until the player has seen it, every read says it
        var edit = PlannerSwitch.PlanFor(s, Who);
        edit.Races["Iksar"] = 2;   // a change to the plan
        PlannerSwitch.Store(s, Who, edit);
        s = Saved(s);
        Assert.Null(View(s).RaceListEmptied);
        Assert.Equal("", Wants(PlannerSwitch.PlanFor(s, Who).RaceOrder));

        var left = SavedBy0_10_23(EveryRaceRanked());
        Assert.Equal(Emptied, View(left).RaceListEmptied);
        PlannerSwitch.RaceListEmptiedSeen(left, Who);   // the player left Campaign
        left = Saved(left);
        Assert.Null(View(left).RaceListEmptied);
        Assert.Equal("", Wants(PlannerSwitch.PlanFor(left, Who).RaceOrder));   // and the list stays empty
    }

    [Fact]
    public void FiveRankedClassesStayWhenAnEditCarriesADifferentFirst()
    {
        var s = new SettingsFile();
        PlannerSwitch.Store(s, Who, PageEdit(new() { ["Iksar"] = 3 }, "race:Iksar"));
        var edit = PlannerSwitch.PlanFor(Saved(s), Who);
        edit.ClassOrder = [.. new[] { "WAR", "CLR", "PAL", "RNG", "ROG" }.Select(c => new CampaignWant { Key = c })];
        foreach (var w in edit.ClassOrder) edit.Classes[w.Key] = 3;
        edit.First = ["race:Iksar"];   // differs from the stored First, which the old cut-down would have obeyed
        PlannerSwitch.Store(s, Who, edit);
        var p = PlannerSwitch.PlanFor(Saved(s), Who);

        Assert.Equal("WAR 3, CLR 3, PAL 3, RNG 3, ROG 3", Wants(p.ClassOrder));
        Assert.Equal(["class:WAR", "class:CLR", "class:PAL"], p.First);   // the projection: classes in rank order, the first three
    }

}
