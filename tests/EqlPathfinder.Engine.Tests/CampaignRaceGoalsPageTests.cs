using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// A race can be a goal without a rank, on the page (docs/specs/campaign-race-goals.md): the race tile sets goals as the class
/// tile does, and Races you want first is the class list's twin. The real page in headless Edge, with the beta's new planner on.
/// A run that clicks a button the page doesn't draw yet fails as "a click on nothing the page shows": that button is the feature.
/// </summary>
public class CampaignRaceGoalsPageTests
{
    private const string Emptied = "Your race list was every race at 3/3: that's your goal now, and Pathfinder picks the order. Rank a race to put it first.";
    private const string Hint = "Click a pip for the rank you want. Click your goal's pip again to lower it.";

    private static JsonObject Goals(IEnumerable<(string Key, int Goal)> goals)
    {
        var o = new JsonObject();
        foreach (var (k, g) in goals) o[k] = g;
        return o;
    }

    /// <summary>The new plan as the window sends it, before any run is planned: ROG 3/3, these race goals, and these races ranked.</summary>
    private static JsonObject Plan(IEnumerable<(string Key, int Goal)> races, string[] ranked, params string[] locked) => new()
    {
        ["classes"] = Goals([("ROG", 3)]),
        ["races"] = Goals(races),
        ["locked"] = new JsonArray([.. locked.Select(x => (JsonNode)x)]),
        ["first"] = new JsonArray(),
        ["loadouts"] = new JsonArray(),
        ["since"] = null,
        ["prefer"] = new JsonArray("carry", "heals"),
        ["classOrder"] = new JsonArray(new JsonObject { ["key"] = "ROG", ["target"] = 3, ["unlockNow"] = false }),
        ["raceOrder"] = new JsonArray([.. ranked.Select(k => (JsonNode)new JsonObject { ["key"] = k, ["target"] = races.First(r => r.Key == k).Goal, ["unlockNow"] = false })]),
    };

    private static readonly (string, int)[] TrollAndIksar = [("Troll", 3), ("Iksar", 1)];

    private static JsonObject IksarUnlockNow(JsonObject plan)
    {
        plan["raceOrder"]!.AsArray().First(w => (string)w!["key"]! == "Iksar")!["unlockNow"] = true;
        return plan;
    }
    private static IEnumerable<(string, int)> EveryRace => ClassNames.Races.Select(r => (r, 3));

    /// <summary>The page data with the new planner on and <paramref name="plan"/> (null: no plan yet), and the one-time line when given.</summary>
    private static JsonObject NewSide(JsonObject? plan, string? emptied = null, Action<JsonObject>? reinc = null)
    {
        var m = PageMessage.FromFixture(plan: plan != null).Result;
        var c = m["reinc"]!["campaign"]!.AsObject();
        var view = JsonSerializer.SerializeToNode(NewPlannerView.Of(
            new(new(ClassRoles.DefaultCarries, true), false), new SurvivalCheck("off", null, 0), []), PageJson.Options)!.AsObject();
        if (emptied != null) view["raceListEmptied"] = emptied;
        c["newPlanner"] = view;
        if (plan != null)
        {
            c["plan"] = plan;
            c["firstLane"] = new JsonObject { ["classes"] = new JsonArray(), ["races"] = new JsonArray(), ["everyRace"] = new JsonArray([.. ClassNames.Races.Select(r => (JsonNode)r)]) };
        }
        reinc?.Invoke(m["reinc"]!.AsObject());
        return m;
    }

    private static JsonObject Last(PageRun run) => (JsonObject)run.PostedAs("campaign").Last()["value"]!;
    private static string[] Keys(JsonNode? list) => list!.AsArray().Select(w => (string)w!["key"]!).ToArray();
    private static PageStep Pip(string race, int k) => PageStep.Click($"[data-goal=\"race:{race}\"][data-k=\"{k}\"]");

    [Fact]
    public async Task TheRaceTilesPipsSetGoalsAndRankNothing()
    {
        var opened = await PageRuns.AtOnce(
            h => h.Run("campaign", PageStep.Send(NewSide(Plan(TrollAndIksar, ["Troll", "Iksar"]))), Pip("Human", 2)),
            h => h.Run("campaign", PageStep.Send(NewSide(Plan(TrollAndIksar, ["Troll", "Iksar"]))), Pip("Iksar", 1)));

        var set = Last(opened.Runs[0]);
        Assert.Equal(2, (int?)set["races"]!["Human"]);
        Assert.Equal(["Troll", "Iksar"], Keys(set["raceOrder"]));   // a goal set on the tile isn't ranked

        var lowered = Last(opened.Runs[1]);
        Assert.Null(lowered["races"]!["Iksar"]);
        Assert.Equal(["Troll"], Keys(lowered["raceOrder"]));   // no goal, no rank
    }

    [Fact]
    public async Task WithNoRaceGoalTheTileSaysTheRanksEarnedAndHowToSetOne()
    {
        var m = NewSide(Plan([], []));
        int earned = m["reinc"]!["races"]!.AsArray().Sum(x => (int)x!["rank"]!);
        var page = (await PageRuns.AtOnce(h => h.Run("campaign", PageStep.Send(m)))).Runs[0].Text("campBody");

        Assert.Contains($"{earned} of {ClassNames.Races.Count * 3} ranks earned · no goal yet", page);   // the race tile's foot: the class tile has ROG's goal
        Assert.Contains(Hint, page);
        Assert.DoesNotContain("Races are ranked below.", page);
        Assert.DoesNotContain("No race is a goal", page);
    }

    [Fact]
    public async Task TheReadyMadeGoalsSetRaceGoalsAndRankNone()
    {
        var opened = await PageRuns.AtOnce(
            h => h.Run("campaign", PageStep.Send(NewSide(null)), PageStep.Click("[data-preset=\"all\"]"), PageStep.Read("all")),
            h => h.Run("campaign", PageStep.Send(NewSide(Plan(TrollAndIksar, ["Troll", "Iksar"]))), PageStep.Click("[data-preset=\"all\"]"), PageStep.Read("on")),
            h => h.Run("campaign", PageStep.Send(NewSide(IksarUnlockNow(Plan(TrollAndIksar, ["Troll", "Iksar"])))), PageStep.Click("[data-preset=\"all\"]"), PageStep.Read("kept")));

        var all = Last(opened.Runs[0]);
        Assert.All(ClassNames.Races, r => Assert.Equal(3, (int)all["races"]![r]!));
        Assert.Empty(all["raceOrder"]!.AsArray());

        // Everything 3/3 keeps the races you ranked, at 3/3 and with their Unlock now, and ranks no other: the plan Campaign.Presets prices
        var kept = Last(opened.Runs[2]);
        Assert.All(ClassNames.Races, r => Assert.Equal(3, (int)kept["races"]![r]!));
        Assert.Equal(["Troll", "Iksar"], Keys(kept["raceOrder"]));
        Assert.All(kept["raceOrder"]!.AsArray(), w => Assert.Equal(3, (int)w!["target"]!));
        Assert.True((bool)kept["raceOrder"]![1]!["unlockNow"]!);
        Assert.False((bool)kept["raceOrder"]![0]!["unlockNow"]!);

        var on = Last(opened.Runs[1]);
        Assert.All(ClassNames.Races, r => Assert.Equal(3, (int)on["races"]![r]!));
        Assert.Equal(["Troll", "Iksar"], Keys(on["raceOrder"]));   // the list as it was

        foreach (var (run, at) in new[] { (opened.Runs[0], "all"), (opened.Runs[1], "on") })
        {
            var card = run.At(at).Text("campFirst");
            Assert.Contains("Classes you want first", card);
            Assert.DoesNotContain("Every race ranked", card);
            Assert.DoesNotContain("don't care", card);
        }
    }

    [Fact]
    public async Task TheEmptyRaceListReadsAsTheClassList()
    {
        var m = NewSide(Plan(EveryRace, [], "Froglok"), reinc: r => r["races"]!.AsArray().First(x => (string)x!["key"]! == "High Elf")!["rank"] = 3);
        var list = (await PageRuns.AtOnce(h => h.Run("campaign", PageStep.Send(m)))).Runs[0].Text("campFirstRaces");

        Assert.Contains("Races you want first", list);
        Assert.Contains("The rest are Pathfinder's to place", list);
        Assert.Contains("Nothing ranked: Pathfinder places every race the cheapest way. Click one below to earn it first, or to unlock it now.", list);
        Assert.Contains("Pathfinder places (15)", list, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HEF", list);
        Assert.Contains("FRG · locked", list);
        Assert.Contains("3/3 already: HIE", list);
        Assert.DoesNotContain("Half Elf", list);
        Assert.DoesNotContain("Don't care", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Only the races you rank are goals.", list);
    }

    [Fact]
    public async Task ARankedRaceIsARowAndAChipRanksIt()
    {
        JsonObject plan() => Plan([("Troll", 3), ("Iksar", 2), ("Human", 2)], ["Troll", "Iksar"]);   // one per run: a node can have one parent
        var opened = await PageRuns.AtOnce(
            h => h.Run("campaign", PageStep.Send(NewSide(plan())), PageStep.Read("rows"), PageStep.Click("[data-first=\"rank\"][data-fk=\"race:Human\"]")),
            h => h.Run("campaign", PageStep.Send(NewSide(plan())), PageStep.Click("[data-first=\"rank\"][data-fk=\"race:Ogre\"]")));

        var rows = opened.Runs[0].At("rows").Text("campFirstRaces");
        Assert.Contains("Drag a row's number, or use its ⋯, to reorder.", rows);
        Assert.Contains("The rest are Pathfinder's to place", rows);
        Assert.DoesNotContain("Click a race to rank it.", rows);

        var human = Last(opened.Runs[0]);
        Assert.Equal(["Troll", "Iksar", "Human"], Keys(human["raceOrder"]));
        Assert.Equal(2, (int?)human["races"]!["Human"]);   // ranked at the goal it had
        var ogre = Last(opened.Runs[1]);
        Assert.Equal(["Troll", "Iksar", "Ogre"], Keys(ogre["raceOrder"]));
        Assert.Equal(3, (int?)ogre["races"]!["Ogre"]);   // no goal yet: 3/3
    }

    [Fact]
    public async Task LeaveItToPathfinderUnranksARaceAndKeepsItsGoal()
    {
        // Spread out (already on) first, as a real player would have.
        var run = (await PageRuns.AtOnce(h => h.Run("campaign", PageStep.Send(NewSide(Plan(TrollAndIksar, ["Troll", "Iksar"]))),
            PageStep.Click("[data-first=\"spread\"][data-fk=\"race:Troll\"]"), PageStep.Click("[data-first=\"menu\"][data-fk=\"race:Troll\"]"), PageStep.Read("menu"),
            PageStep.Click("[data-first=\"unrank\"][data-fk=\"race:Troll\"]"), PageStep.Read("after")))).Runs[0];

        var menu = run.At("menu").Text("campPop");
        Assert.Contains("Leave it to Pathfinder", menu);
        Assert.Contains("Unranks Troll. Its goal stays 3/3.", menu);
        Assert.DoesNotContain("Don't care", menu);

        var after = Last(run);
        Assert.Equal(["Iksar"], Keys(after["raceOrder"]));
        Assert.Equal(3, (int?)after["races"]!["Troll"]);
        Assert.Contains("Troll is Pathfinder's to place again.", run.At("after").Text("campFirst"));
    }

    [Fact]
    public async Task ARowMenuStaysOpenForAScrollThatMovedNothing()
    {
        // The browser delivers the scroll of a focus a frame late; the button is where it was, so the menu stays.
        var run = (await PageRuns.AtOnce(h => h.Run("campaign", PageStep.Send(NewSide(Plan(TrollAndIksar, ["Troll", "Iksar"]))),
            PageStep.Click("[data-first=\"menu\"][data-fk=\"race:Troll\"]"), PageStep.Scroll(), PageStep.Read("menu")))).Runs[0];

        Assert.Contains("Leave it to Pathfinder", run.At("menu").Text("campPop"));
    }

    [Fact]
    public async Task ARowMenuClosesWhenARealScrollMovesItsButton()
    {
        // Every race is on the list, so the page is taller than the window and #main really scrolls.
        var run = (await PageRuns.AtOnce(h => h.Run("campaign", PageStep.Send(NewSide(Plan(EveryRace, ["Troll", "Iksar"]))),
            PageStep.Click("[data-first=\"menu\"][data-fk=\"race:Troll\"]"), PageStep.Read("open"), PageStep.Scroll(40), PageStep.Read("menu")))).Runs[0];

        Assert.Contains("Leave it to Pathfinder", run.At("open").Text("campPop"));
        Assert.True(double.Parse(run.At("menu").Text("#main scrollTop")) > 0, "the page did not scroll, so this proves nothing");
        Assert.DoesNotContain("Leave it to Pathfinder", run.At("menu").Text("campPop"));
    }

    [Fact]
    public async Task TheOneTimeLineShowsUntilThePlanChangesOrCampaignIsLeft()
    {
        var m = NewSide(Plan(EveryRace, []), emptied: Emptied);
        var opened = await PageRuns.AtOnce(
            h => h.Run("campaign", PageStep.Send(m), PageStep.Read("open"), Pip("Human", 1), PageStep.Read("changed")),
            h => h.Run("campaign", PageStep.Send(m), PageStep.Read("open"),
                PageStep.Click(".rail button[data-page=\"home\"]"), PageStep.Click(".rail button[data-page=\"planning\"]"), PageStep.Send(m), PageStep.Read("back")));

        var open = opened.Runs[0].At("open").Text("campFirstRaces");
        Assert.Contains(Emptied, open);
        Assert.DoesNotContain("Nothing ranked", open);   // in its place

        var changed = opened.Runs[0].At("changed").Text("campFirstRaces");
        Assert.Contains("Races you want", changed);
        Assert.DoesNotContain(Emptied, changed);

        Assert.Contains(Emptied, opened.Runs[1].At("open").Text("campFirstRaces"));
        var back = opened.Runs[1].At("back").Text("campFirstRaces");
        Assert.Contains("Races you want", back);
        Assert.DoesNotContain(Emptied, back);   // even when the window sends it again before it has heard
    }

    [Fact]
    public async Task LeavingCampaignAfterTheLineShowedTellsTheWindowOnceAndOnlyThen()
    {
        var shown = NewSide(Plan(EveryRace, []), emptied: Emptied);
        var opened = await PageRuns.AtOnce(
            h => h.Run("campaign", PageStep.Send(shown), PageStep.Click(".rail button[data-page=\"home\"]")),
            h => h.Run("campaign", PageStep.Send(shown), PageStep.Click(".rail button[data-page=\"home\"]"), PageStep.Click(".rail button[data-page=\"planning\"]"), PageStep.Click(".rail button[data-page=\"home\"]")),
            h => h.Run("campaign", PageStep.Send(NewSide(Plan(EveryRace, []))), PageStep.Click(".rail button[data-page=\"home\"]")));

        static IReadOnlyList<JsonObject> Seen(PageRun run) => [.. run.PostedAs("setting").Where(p => (string?)p["key"] == "raceListSeen")];
        Assert.Single(Seen(opened.Runs[0]));   // the window hears it, so the line is over
        Assert.Single(Seen(opened.Runs[1]));   // and once
        Assert.Empty(Seen(opened.Runs[2]));    // no line shown, nothing to tell
    }
}
