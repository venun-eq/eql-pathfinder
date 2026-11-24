using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Campaign's two notices that go away by themselves (docs/specs/combo-flash-scroll.md): "Removed …" under Your combos
/// after 6 seconds, and Start over's "Click again to clear the plan" after 4. Each goes away where it is, and the page
/// stays where the player scrolled it (#24: the page was drawn anew and scrolled back to the button with the focus).
/// </summary>
public class CampaignTimersPageTests
{
    private static async Task<JsonObject> WithTwoCombos()
    {
        var m = await PageMessage.FromFixture(plan: true);
        var c = m["reinc"]!["campaign"]!.AsObject();
        c["newPlanner"] = JsonSerializer.SerializeToNode(NewPlannerView.Of(
            new(new(ClassRoles.DefaultCarries, true), false), new SurvivalCheck("off", null, 0), []), PageJson.Options);
        c["plan"]!["combos"] = new JsonArray(
            new JsonObject { ["classes"] = new JsonArray("PAL", "MNK"), ["everyRun"] = true, ["times"] = 3 },
            new JsonObject { ["classes"] = new JsonArray("WAR", "CLR"), ["everyRun"] = false, ["times"] = 3 });
        return m;
    }

    /// <summary>
    /// This many seconds on the page's clock: each step is 200 ms of it (PageHarness's SettleMs), and a scroll that moves
    /// nothing is a step that does nothing else.
    /// </summary>
    private static PageStep[] Seconds(double s) => Enumerable.Repeat(PageStep.Scroll(), (int)Math.Ceiling(s * 5)).ToArray();

    private static double ScrollTop(PageRun run, string at) => double.Parse(run.At(at).Text("#main scrollTop"), CultureInfo.InvariantCulture);

    [Fact]
    public async Task TheRemovedNoticeGoesAwayWithoutScrollingThePageBackToAddACombo()
    {
        var run = new PageHarness().Run("campaign",
        [
            PageStep.Send(await WithTwoCombos()),
            PageStep.Click("[data-ci=\"0\"][data-combo-act=\"menu\"]"),
            PageStep.Click("[data-ci=\"0\"][data-combo-act=\"remove\"]"), PageStep.Read("removed"),
            PageStep.Scroll(-100_000), PageStep.Read("top"),   // the player scrolls back up to the top
            .. Seconds(7), PageStep.Read("later"),
        ]);

        Assert.Contains("Removed \"PAL with MNK\".", run.At("removed").Text("campBody"));
        Assert.True(ScrollTop(run, "removed") > 0, "Add a combo was on screen with the page at the top, so this proves nothing");
        Assert.Equal(0, ScrollTop(run, "top"));

        Assert.Equal(0, ScrollTop(run, "later"));
        Assert.DoesNotContain("Removed \"", run.At("later").Text("campBody"));
        Assert.Contains("Your combos", run.At("later").Text("campBody"));
    }

    [Fact]
    public async Task StartOversClickAgainGoesBackWithoutScrollingThePageBackUp()
    {
        var run = new PageHarness().Run("campaign",
        [
            PageStep.Send(await WithTwoCombos()),
            PageStep.Click("[data-camp=\"clear\"]"), PageStep.Read("asked"),
            PageStep.Scroll(600), PageStep.Read("down"),   // the player scrolls down to look at the runs first
            .. Seconds(5), PageStep.Read("later"),
        ]);

        Assert.Contains("Click again to clear the plan", run.At("asked").Text("campBody"));
        var down = ScrollTop(run, "down");
        Assert.True(down > 0, "the page did not scroll, so this proves nothing");

        Assert.Equal(down, ScrollTop(run, "later"));
        Assert.DoesNotContain("Click again to clear the plan", run.At("later").Text("campBody"));
        Assert.Contains("Start over", run.At("later").Text("campBody"));
        Assert.Empty(run.PostedAs("campaign"));   // one click asks; it never clears
    }
}
