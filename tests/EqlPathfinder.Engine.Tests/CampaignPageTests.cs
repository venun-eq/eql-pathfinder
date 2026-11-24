using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Campaign in headless Edge for every test here, in one run: the fixture's player with a whole campaign and before
/// any plan, each as the log has them and with a count set, then a click on Set counts.
/// </summary>
public sealed class CampaignPages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;
    internal JsonObject AsLogged { get; private set; } = null!;
    internal JsonObject NoPlan { get; private set; } = null!;
    internal JsonObject Leveling { get; private set; } = null!;
    internal JsonObject Finished { get; private set; } = null!;
    internal JsonObject LevelingBeforeThePlan { get; private set; } = null!;

    /// <summary>The plan with its first two runs reincarnated: leveling (32 and 1 of the cap) or, with <paramref name="levelingNow"/> false, back at the cap.</summary>
    private static async Task<JsonObject> TwoRunsReincarnated(bool levelingNow, double cap, bool beforeThePlan = false)
    {
        var m = (await PageMessage.FromFixture()).With("reinc.levelCap", cap);
        var c = m["reinc"]!["campaign"]!.AsObject();
        var runs = c["runs"]!.AsArray();
        for (int i = 0; i < 2; i++)
        {
            var r = runs[i]!.AsObject();
            r["state"] = levelingNow ? "leveling" : "done";
            if (levelingNow) { r["level"] = i == 0 ? 32 : 1; r["etaHours"] = i == 0 ? 6.2 : null; r["crawls"] = i == 0 ? 9 : 0; }
        }
        runs[2]!.AsObject()["state"] = "next";
        c["done"] = 2; c["left"] = runs.Count - 2; c["leveling"] = levelingNow ? 2 : 0;
        if (beforeThePlan)
        {
            // As the engine sends it: both runs were reincarnated before the plan began, so they have no loadout in it (-1) and
            // the six runs played before are the history above them.
            runs[0]!["loadout"] = -1; runs[1]!["loadout"] = -1;
            c["before"] = 6; c["unseen"] = 6;
        }
        return m;
    }

    public async Task InitializeAsync()
    {
        AsLogged = await PageMessage.FromFixture();
        NoPlan = await PageMessage.FromFixture(plan: false);
        Leveling = await TwoRunsReincarnated(true, 55);
        Finished = await TwoRunsReincarnated(false, 55);
        LevelingBeforeThePlan = await TwoRunsReincarnated(true, 55, beforeThePlan: true);
        var countsSet = await PageMessage.WithCountsSet();
        var noPlanCountsSet = await PageMessage.WithCountsSet(plan: false);
        var opened = await PageRuns.AtOnce(h => h.Run("campaign",
            PageStep.Send(AsLogged), PageStep.Read("plan asks"),
            PageStep.Send(countsSet), PageStep.Read("plan counts set"),
            PageStep.Send(NoPlan), PageStep.Read("start asks"),
            PageStep.Send(noPlanCountsSet), PageStep.Read("start counts set"),
            PageStep.Send(Leveling), PageStep.Read("two leveling"),
            PageStep.Send(Finished), PageStep.Read("two done"),
            PageStep.Send(LevelingBeforeThePlan), PageStep.Read("two leveling before the plan"),
            PageStep.Send(AsLogged), PageStep.Click("#page-campaign [data-set-counts]")));
        Run = opened.Runs[0];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// What Campaign does with its page data (docs/quality.md): the real page in headless Edge, read as a player reads it.
/// These were regular expressions over campaign.js, core.js and roster.js in AppPageTests.
/// </summary>
public class CampaignPageTests(CampaignPages pages) : IClassFixture<CampaignPages>
{
    /// <summary>
    /// Campaign planned "Run 1 of 48" at +10% for a player six runs in (11-17): a log turned on after years of play has
    /// no reincarnation in it. While the log has seen none and no count is set, the plan says it starts from no
    /// reincarnations and offers Set counts, and before any plan the ready-made goals count their runs the same way.
    /// </summary>
    [Fact]
    public void CampaignSaysItPlansFromNoReincarnationsAndAsksForCounts()
    {
        var plan = pages.Run.At("plan asks").Text("campBody");
        Assert.Contains("YOUR CAMPAIGN · EVERYTHING 3/3 · FROM NO REINCARNATIONS", plan);   // the label is drawn in capitals
        Assert.Contains("Run 1 of 48", plan);
        Assert.Contains(HomePageTests.NoReincarnationsSince(pages.AsLogged), plan);
        Assert.Contains(HomePageTests.SetYourCounts, plan);
        Assert.Contains("Set counts", plan);

        var start = pages.Run.At("start asks").Text("campBody");
        Assert.Contains("Plan every reincarnation", start);
        Assert.Contains($"{pages.NoPlan.Number("reinc.campaign.presets.0.runs"):0} runs from no reincarnations", start);
        Assert.Contains(HomePageTests.NoReincarnationsSince(pages.NoPlan), start);
        Assert.Contains("Set counts", start);
        Assert.DoesNotContain("from here", start);
    }

    /// <summary>Once a count is set the plan counts from the player's ranks: no banner, the ranks they set already count, and the goals' runs are "from here".</summary>
    [Fact]
    public void CampaignStopsAskingOnceACountIsSet()
    {
        var plan = pages.Run.At("plan counts set").Text("campBody");
        Assert.Contains("YOUR CAMPAIGN · EVERYTHING 3/3", plan);
        Assert.Contains($"{PageMessage.CountsSetTo} of 48 ranks you want", plan);
        Assert.DoesNotContain("FROM NO REINCARNATIONS", plan);
        Assert.DoesNotContain("Your log shows no reincarnations", plan);
        Assert.DoesNotContain("Set counts", plan);

        var start = pages.Run.At("start counts set").Text("campBody");
        Assert.Contains("Plan every reincarnation", start);
        Assert.Contains("runs from here", start);
        Assert.DoesNotContain("from no reincarnations", start);
        Assert.DoesNotContain("Your log shows no reincarnations", start);
        Assert.DoesNotContain("Set counts", start);
    }

    /// <summary>
    /// Feedback #6 (0.10.7): both reincarnated runs read "done" while the player was still leveling them, and the big
    /// number jumped to the next run. Reincarnated and below the cap, a run is leveling, with its level out of the cap
    /// (55 here: never a literal 50); the big number is the earliest leveling run and the next to reincarnate is named under it.
    /// </summary>
    [Fact]
    public void ARunReincarnatedBelowTheCapReadsLevelingWithItsLevel()
    {
        var t = pages.Run.At("two leveling").Text("campBody").Replace(' ', ' ');
        var runs = pages.Leveling.List("reinc.campaign.runs");
        string Name(int i) => $"{runs[i]!["race"]} · {string.Join(" / ", runs[i]!["classes"]!.AsArray().Select(x => (string)x!))}";
        Assert.Contains("Run 1 of 48", t);
        Assert.Contains("Leveling: " + Name(0) + " · 32 / 55 · ≈ 6.2 h to go", t);
        Assert.Contains("Also leveling: run 2 · " + Name(1) + " · 1 / 55 · not played yet", t);
        Assert.Contains("Next to reincarnate: run 3 · " + Name(2), t);
        Assert.DoesNotContain("Leveling now", t.Split("Your runs")[0]);   // the top card says "Leveling:", the run list below has the group
        Assert.DoesNotContain("/ 50", t);
        Assert.Contains("0 done · 2 leveling · 46 to go", t);
        Assert.Contains("32 / 55", t);
    }

    /// <summary>With every reincarnated run back at the cap the hero is what it was: the next run, and runs 1 and 2 green "done".</summary>
    [Fact]
    public void RunsBackAtTheCapReadDoneAndTheNextRunLeadsAgain()
    {
        var t = pages.Run.At("two done").Text("campBody");
        Assert.Contains("Run 3 of 48", t);
        Assert.Contains("Next: ", t);
        Assert.DoesNotContain("Leveling", t);
        Assert.DoesNotContain("Also leveling", t);
        Assert.DoesNotContain("Next to reincarnate", t);
        Assert.Contains("2 done · 46 to go", t);
    }

    /// <summary>
    /// The player's own state: Kerran and Barbarian were reincarnated before the plan began, so they sit in "played before" and the
    /// plan has no loadout for them. They still read leveling, with a row each, and the big number is run 7, not 9.
    /// </summary>
    [Fact]
    public void RunsReincarnatedBeforeThePlanAndStillLevelingAreCountedAndGetTheirOwnRows()
    {
        var t = pages.Run.At("two leveling before the plan").Text("campBody").Replace((char)0xA0, (char)32);
        var runs = pages.LevelingBeforeThePlan.List("reinc.campaign.runs");
        string Name(int i) => $"{runs[i]!["race"]} · {string.Join(" / ", runs[i]!["classes"]!.AsArray().Select(x => (string)x!))}";
        Assert.Contains("Run 7 of 54", t);   // 6 played before, then the runs: the plan's 48 now include these two
        Assert.Contains("Leveling: " + Name(0) + " · 32 / 55 · ≈ 6.2 h to go", t);
        Assert.Contains("Also leveling: run 8 · " + Name(1) + " · 1 / 55 · not played yet", t);
        Assert.Contains("Next to reincarnate: run 9 · " + Name(2), t);
        Assert.Contains("6 played before · 0 done · 2 leveling · 46 to go", t);
    }

    /// <summary>
    /// Set counts on Campaign's banner opens Roster with its count entry ready. Roster is the other tab of Campaign's
    /// own group (Plan), so this is also a tab reached from its neighbour (docs/quality.md, "A tab named like its group").
    /// </summary>
    [Fact]
    public void SetCountsOnCampaignOpensRostersCountEntry()
    {
        Assert.Equal("campaign", pages.Run.At("plan asks").Page);
        PageRunReads.IsRosterWithItsCountsOpen(pages.Run, from: "campaign");
    }
}

/// <summary>
/// Campaign's edits say no plan: there is one planner (docs/specs/planner-out-of-beta.md), so no edit carries the plan the page
/// showed, and the window has nothing to drop.
/// </summary>
public class CampaignEditSendsNoPlannerTests
{
    [Fact]
    public async Task NoCampaignEditNamesAPlanner()
    {
        var m = await PageMessage.FromFixture(plan: false);
        var opened = await PageRuns.AtOnce(h => h.Run("campaign", PageStep.Send(m), PageStep.Click("#page-campaign [data-preset=\"all\"]")));
        var sent = opened.Runs[0].PostedAs("campaign").Single();
        Assert.True(sent["value"] != null && sent["planner"] == null, sent.ToJsonString());
    }
}

/// <summary>
/// "Rebuild the rest" on a plan a rebuild leaves the same (report 14, docs/specs/campaign-run-gold.md): the page waited
/// for the saved plan to change, so every run row stayed blank (its gold 3/3s, dots and XP) under "updating…" until a
/// combo changed the plan. The window's answer to the edit (<c>settings.campaignAnswered</c>) now ends the wait.
/// </summary>
public class CampaignEditAnswerTests
{
    [Fact]
    public async Task RebuildingAPlanThatStaysTheSameShowsItsRunsAgainOnceTheWindowAnswers()
    {
        // The window has answered earlier edits (the page opened after them): the page's next edit goes past their number.
        var shown = await PageMessage.FromFixture();
        var earlier = (int)shown.Number("settings.campaignAnswered");
        Assert.True(earlier > 0, "the fixture's window has answered an edit before");
        var before = await PageMessage.FromFixture();                       // a refresh read before the rebuild was saved
        var answered = (await PageMessage.FromFixture()).With("settings.campaignAnswered", earlier + 1);
        var opened = await PageRuns.AtOnce(h => h.Run("campaign",
            PageStep.Send(shown), PageStep.Read("before the click"),
            PageStep.Click("#page-campaign [data-camp=\"rebuild\"]"), PageStep.Read("waiting"),
            PageStep.Send(before), PageStep.Read("a refresh from before the save"),
            PageStep.Send(answered), PageStep.Read("answered")));
        var run = opened.Runs[0];

        var was = run.At("before the click").Text("campBody");
        Assert.Contains("3/3", was);
        Assert.Contains("updating…", run.At("waiting").Text("campBody"));
        Assert.Contains("updating…", run.At("a refresh from before the save").Text("campBody"));
        var now = run.At("answered").Text("campBody");
        Assert.DoesNotContain("updating…", now);
        Assert.Equal(was, now);
        Assert.Equal(earlier + 1, (int?)run.PostedAs("campaign").Single()["n"]);   // the number the window answers with
    }
}

/// <summary>
/// What the card's clicks write (docs/specs/campaign-first-lane-page.md, "Decisions"): with the beta's new side on, each edit
/// posts the two lists and the goals, one value by construction, and never `first`. One run per click, all at once.
/// </summary>
public class CampaignFirstCardEditsTests
{
    private static readonly string[] Reversed = [.. ClassNames.Races.Reverse()];
    private const string RankedPlan = "{\"classes\":{\"ROG\":3},\"races\":{\"Troll\":3,\"Iksar\":2},\"locked\":[],\"first\":[],\"loadouts\":[],\"since\":null,\"prefer\":[\"carry\",\"heals\"],\"classOrder\":[{\"key\":\"ROG\",\"target\":3,\"unlockNow\":false}],\"raceOrder\":[{\"key\":\"Troll\",\"target\":3,\"unlockNow\":false},{\"key\":\"Iksar\",\"target\":2,\"unlockNow\":false}]}";
    private const string EmptyPlan = "{\"classes\":{},\"races\":{},\"locked\":[],\"first\":[],\"loadouts\":[],\"since\":null,\"prefer\":[\"carry\",\"heals\"],\"classOrder\":[],\"raceOrder\":[]}";

    /// <summary>The ranked plan with the fixture's runs: Clear goals sits with the ready-made goals, which only a plan with runs draws.</summary>
    private static JsonObject WithRuns(JsonObject m)
    {
        var runs = PageMessage.FromFixture(plan: true).Result["reinc"]!["campaign"]!["plan"]!["loadouts"]!.DeepClone();
        m["reinc"]!["campaign"]!["plan"]!["loadouts"] = runs;
        return m;
    }

    private static async Task<JsonObject> NewSide(bool plan, bool ranked = true)
    {
        var m = await PageMessage.FromFixture(plan: plan);
        var c = m["reinc"]!["campaign"]!.AsObject();
        c["newPlanner"] = System.Text.Json.JsonSerializer.SerializeToNode(NewPlannerView.Of(
            new(new(ClassRoles.DefaultCarries, true), false), new SurvivalCheck("off", null, 0), []), PageJson.Options);
        if (plan)
        {
            c["plan"] = JsonNode.Parse(ranked ? RankedPlan : EmptyPlan);
            c["firstLane"] = new JsonObject { ["classes"] = new JsonArray(), ["races"] = new JsonArray(), ["everyRace"] = new JsonArray([.. Reversed.Select(r => (JsonNode)r)]) };
        }
        return m;
    }

    private static JsonObject Last(PageRun run) => (JsonObject)run.PostedAs("campaign").Last()["value"]!;
    private static string[] Keys(JsonNode? list) => list!.AsArray().Select(w => (string)w!["key"]!).ToArray();

    [Fact]
    public async Task EachClickInTheCardPostsTheListsAndTheGoalsTogether()
    {
        var opened = await PageRuns.AtOnce(
            h => h.Run("campaign", PageStep.Send(NewSide(true, ranked: false).Result), PageStep.Click("#campFirst [data-first=\"rank\"][data-fk^=\"class:\"]")),
            // Leave it to Pathfinder: Spread out (already on) first, since a click scrolls the page to its button and a page scroll closes a menu.
            h => h.Run("campaign", PageStep.Send(NewSide(true).Result), PageStep.Click("[data-first=\"spread\"][data-fk=\"race:Troll\"]"), PageStep.Click("[data-first=\"menu\"][data-fk=\"race:Troll\"]"), PageStep.Click("[data-first=\"unrank\"][data-fk=\"race:Troll\"]")),
            h => h.Run("campaign", PageStep.Send(WithRuns(NewSide(true).Result)), PageStep.Click("[data-camp=\"no-goals\"]")),
            h => h.Run("campaign", PageStep.Send(NewSide(true).Result), PageStep.Click("[data-goal=\"race:Troll\"][data-k=\"2\"]")),
            h => h.Run("campaign", PageStep.Send(NewSide(false).Result), PageStep.Click("[data-preset=\"all\"]")));

        var ranked = Last(opened.Runs[0]);
        var cls = Keys(ranked["classOrder"]);
        Assert.Single(cls);
        Assert.Equal(3, (int)ranked["classes"]![cls[0]]!);   // a class with no goal gets 3/3 with its place

        var dontCare = Last(opened.Runs[1]);
        Assert.Equal(["Iksar"], Keys(dontCare["raceOrder"]));
        Assert.Equal(3, (int?)dontCare["races"]!["Troll"]);   // Leave it to Pathfinder keeps its goal (docs/specs/campaign-race-goals.md)
        Assert.Equal(2, (int)dontCare["races"]!["Iksar"]!);

        var cleared = Last(opened.Runs[2]);
        Assert.Empty(cleared["classOrder"]!.AsArray());
        Assert.Empty(cleared["raceOrder"]!.AsArray());
        Assert.Empty(cleared["classes"]!.AsObject());
        Assert.Empty(cleared["races"]!.AsObject());

        var every = Last(opened.Runs[3]);
        Assert.Equal(["Troll", "Iksar"], Keys(every["raceOrder"]));   // a race pip sets that rank and the list keeps its places
        Assert.Equal(2, (int)every["races"]!["Troll"]!);

        var start = Last(opened.Runs[4]);
        Assert.Empty(start["raceOrder"]!.AsArray());   // Everything 3/3 ranks no race
        Assert.All(ClassNames.Races, r => Assert.Equal(3, (int)start["races"]![r]!));
        Assert.All(opened.Runs, r => Assert.All(r.PostedAs("campaign"), p => Assert.Null(p["value"]!["first"]?.AsArray().FirstOrDefault())));   // never `first`
    }
}
