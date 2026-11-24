using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The Campaign card and the Class roles tab in headless Edge, drawn from the view the engine builds (docs/specs/campaign-class-roles.md):
/// the page only draws what <see cref="NewPlannerView"/> says (the card's lines, what each class kills with, whether the carries are
/// Pathfinder's list), it decides none of it.
/// </summary>
public class ClassRolesPageTests
{
    private static async Task<JsonObject> WithView(PlannerInput input)
    {
        var m = await PageMessage.FromFixture();
        var c = m["reinc"]!["campaign"]!.AsObject();
        c["newPlanner"] = JsonSerializer.SerializeToNode(NewPlannerView.Of(input, new SurvivalCheck("checked", 4, 1), [3, 4]), PageJson.Options);
        return m;
    }

    [Fact]
    public async Task TheCardAndTheTabDrawWhatTheEngineSays()
    {
        var pathfinders = await WithView(new(new(ClassRoles.DefaultCarries, true), true));
        var own = await WithView(new(new(["WIZ", "BST"], true), false));
        var run = new PageHarness().Run("campaign",
            PageStep.Send(pathfinders), PageStep.Read("card"),
            PageStep.Click("#page-campaign [data-camp=got]"), PageStep.Read("got it"),
            PageStep.Click("#page-campaign [data-go=classroles]"), PageStep.Read("tab"),
            PageStep.Send(own), PageStep.Read("own list"));
        // The card "Campaign has a new planner" shows when the engine says an old plan is stored, with the short version and Got it.
        var card = run.At("card").Text("campBody");
        Assert.Contains("Campaign has a new planner", card);
        Assert.Contains("THE SHORT VERSION", card, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Got it", card);
        // Got it tells the app the card was seen, and the card closes at once.
        var seen = Assert.Single(run.PostedAs("setting"), p => (string?)p["key"] == "plannerCard");
        Assert.Equal("seen", (string?)seen["value"]);
        Assert.DoesNotContain("Campaign has a new planner", run.At("got it").Text("campBody"));
        Assert.DoesNotContain("Campaign has a new planner", run.At("own list").Text("campBody"));
        // The tab: Pathfinder's list says so and has no way back; what each class kills with is the engine's.
        var tab = run.At("tab").Text("rolesBody");
        Assert.Contains("Pathfinder's list", tab);
        Assert.DoesNotContain("Back to Pathfinder's", tab);
        Assert.Matches(@"MAG\s+pet", tab);
        Assert.Matches(@"ENC\s+charm", tab);
        // Their own list: BST is there with its warder, and the way back shows.
        var after = run.At("own list").Text("rolesBody");
        Assert.Contains("Back to Pathfinder's", after);
        Assert.Matches(@"WIZ\s+nukes", after);
        Assert.Matches(@"BST\s+warder", after);
    }

    /// <summary>What each class kills with is the engine's table, one for every class.</summary>
    [Fact]
    public void TheViewCarriesWhatEveryClassKillsWithAndWhetherTheListIsPathfinders()
    {
        var view = NewPlannerView.Of(new(new(ClassRoles.DefaultCarries, true), false), new SurvivalCheck("off", null, 0), []);
        Assert.True(view.CarriesAreDefault);
        Assert.Equal(ClassNames.All.Count, view.Kills.Count);
        Assert.All(ClassNames.All, c => Assert.Equal(ClassRoles.Kills(c.Abbrev), view.Kills[c.Abbrev]));
        Assert.False(NewPlannerView.Of(new(new(["MAG"], true), false), new SurvivalCheck("off", null, 0), []).CarriesAreDefault);
        Assert.True(NewPlannerView.Of(new(new(ClassRoles.DefaultCarries, true), true), new SurvivalCheck("off", null, 0), []).OldPlans);
    }
}
