using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Campaign's page layout (docs/specs/campaign-page-layout.md): a header band, then Your runs, beside a side column of
/// Your goals, What you want first and Your combos. The widths themselves are CSS, proved by the shots; these read which
/// card is in which column, in headless Edge.
/// </summary>
public sealed class CampaignLayoutPages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;
    internal PageRun Click { get; private set; } = null!;
    /// <summary>A player outside the beta: the old planner, which gets the same layout.</summary>
    internal JsonObject Planned { get; private set; } = null!;
    internal JsonObject Dearer { get; private set; } = null!;
    internal JsonObject NoGoals { get; private set; } = null!;
    internal JsonObject NoPlan { get; private set; } = null!;

    /// <summary>The beta planner's page data: What you want first and Your combos draw only on it.</summary>
    private static JsonObject NewPlanner(JsonObject m)
    {
        var c = m["reinc"]!["campaign"]!.AsObject();
        c["newPlanner"] = System.Text.Json.JsonSerializer.SerializeToNode(NewPlannerView.Of(
            new(new(EqlPathfinder.Engine.Model.ClassRoles.DefaultCarries, true), false), new SurvivalCheck("off", null, 0), []), PageJson.Options);
        c["firstLane"] = new JsonObject { ["classes"] = new JsonArray(), ["races"] = new JsonArray(), ["everyRace"] = new JsonArray() };
        return m;
    }

    public async Task InitializeAsync()
    {
        Planned = NewPlanner(await PageMessage.WithCountsSet());
        // The plan costs one run more than the fewest that reaches the goals.
        Dearer = NewPlanner(await PageMessage.WithCountsSet()).With("reinc.campaign.floor.runs", Planned.Number("reinc.campaign.left") - 1);
        // No goals left to earn: the fewest runs that reach them is none.
        NoGoals = NewPlanner(await PageMessage.WithCountsSet()).With("reinc.campaign.floor.runs", 0);
        NoPlan = await PageMessage.WithCountsSet(plan: false);
        var opened = await PageRuns.AtOnce(h => h.Run("campaign",
            PageStep.Send(Planned), PageStep.Read("planned"),
            PageStep.Send(Dearer), PageStep.Read("dearer"),
            PageStep.Send(NoGoals), PageStep.Read("nogoals"),
            PageStep.Send(NoPlan), PageStep.Read("empty")),
            h => h.Run("campaign", PageStep.Send(Planned), PageStep.Click("[data-camp=\"goal-classes\"]")));
        Run = opened.Runs[0];
        Click = opened.Runs[1];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class CampaignLayoutTests(CampaignLayoutPages pages) : IClassFixture<CampaignLayoutPages>
{
    [Fact]
    public void TheGoalsAndTheTwoListsSitInTheSideColumnBesideTheRuns()
    {
        var side = pages.Run.At("planned").Text("campSide");
        var goals = side.IndexOf("Your goals", StringComparison.Ordinal);
        Assert.True(goals >= 0, side);
        Assert.True(goals < side.IndexOf("Classes you want first", StringComparison.Ordinal), side);
        Assert.True(side.IndexOf("Classes you want first", StringComparison.Ordinal) < side.IndexOf("Your combos", StringComparison.Ordinal), side);
        Assert.DoesNotContain("Your runs", side);
        Assert.Contains("Your runs", pages.Run.At("planned").Text("campMain"));
    }

    [Fact]
    public void TheHeaderIsOneBandWithTheTotalsAndNoCheapestPathCard()
    {
        var band = pages.Run.At("planned").Text("campBand");
        Assert.Contains("YOUR CAMPAIGN · EVERYTHING 3/3", band);
        Assert.Contains("Run 1 of 48", band);
        Assert.Contains("runs to go", band);
        Assert.Contains("Rebuild the rest", band);
        Assert.Contains("Start over", band);
        Assert.DoesNotContain("Class passives", band);
        var all = pages.Run.At("planned").Text("campBody");
        Assert.DoesNotContain("Cheapest path", all);
        Assert.DoesNotContain("the cheapest path", band);
    }

    [Fact]
    public void APlanDearerThanTheCheapestPathSaysSoInThePlainTotalsLine()
    {
        var band = pages.Run.At("dearer").Text("campBand");
        Assert.Contains("· 1 run more than the cheapest path", band);
    }


    [Fact]
    public void ASpareRunsPlanWithNoRealFixSaysSoOnlyInTheBand()
    {
        var all = pages.Run.At("dearer").Text("campBody");
        Assert.DoesNotContain("more than it needs", all);
        Assert.Equal(1, all.Split("Rebuild the rest").Length - 1);
    }
    [Fact]
    public void WithNoGoalsLeftTheTotalsLineSaysNoneOfTheRunsIsNeeded()
    {
        var band = pages.Run.At("nogoals").Text("campBand");
        Assert.Contains("No goals left to earn: none of these runs is needed", band);
        Assert.DoesNotContain("more than the cheapest path", band);
    }

    [Fact]
    public void TheEmptyPlanKeepsPlanEveryReincarnationBesideYourGoals()
    {
        var run = pages.Run.At("empty");
        Assert.Contains("Plan every reincarnation", run.Text("campMain"));
        Assert.Contains("Your runs show here once you pick a goal", run.Text("campMain"));
        var side = run.Text("campSide");
        Assert.Contains("Your goals", side);
        Assert.Contains("Build the plan", side);
        Assert.DoesNotContain("Plan every reincarnation", side);
        Assert.DoesNotContain("Your campaign", run.Text("campBody"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThreeReadyMadeGoalsSitUnderYourGoalsAndTheToolsRowIsGone()
    {
        var side = pages.Run.At("planned").Text("campSide");
        Assert.Contains("Everything 3/3", side);
        Assert.Contains("Every class 3/3", side);
        Assert.Contains("Clear goals", side);
        Assert.Contains("More options", side);
        Assert.DoesNotContain("Every race 3/3", side);
        Assert.DoesNotContain("Rank races", side);
        Assert.DoesNotContain("a gold ring marks", side);
        Assert.Contains("✓ Everything 3/3", side);   // the planned page's goals are every class and race at 3/3
        Assert.DoesNotContain("✓ Every class 3/3", side);
    }

    [Fact]
    public void TheEmptyPlanHasNoReadyMadeGoalsInYourGoals()
    {
        var side = pages.Run.At("empty").Text("campSide");
        Assert.DoesNotContain("Clear goals", side);
        Assert.DoesNotContain("Everything 3/3", side);
        Assert.Contains("ranks earned · no goal yet", side);
        Assert.Contains("you have it", side);
    }

    [Fact]
    public void EachTileSaysHowManyRanksYouWantAndALegendNamesTheMarks()
    {
        var side = pages.Run.At("planned").Text("campSide");
        Assert.Contains("ranks you want", side);
        Assert.Contains("this plan earns it", side);
        Assert.DoesNotContain("not planned", side);   // the legend names the marks drawn: a fully planned page has no pip left unplanned
        Assert.Contains("your goal", side);
        Assert.Contains("Click a pip for the rank you want. Click your goal's pip again to lower it.", side);
        Assert.DoesNotContain("0 of 0", side);
    }

    [Fact]
    public void AClickOnAnUnlitReadyMadeGoalSetsItAndRebuildsAroundTheRuns()
    {
        var sent = pages.Click.PostedAs("campaign").Last();
        var plan = (JsonObject)sent["value"]!;
        Assert.True((bool)sent["rebuild"]!);
        Assert.Equal(ClassNames.All.Count, plan["classes"]!.AsObject().Count);
        Assert.Empty(plan["races"]!.AsObject());                 // Every class 3/3 takes the race goals away
        Assert.Empty(plan["raceOrder"]!.AsArray());
        Assert.Equal(pages.Planned["reinc"]!["campaign"]!["plan"]!["loadouts"]!.AsArray().Count, plan["loadouts"]!.AsArray().Count);   // the runs stay for the rebuild to keep
    }

}
