using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The words of the Kunark cap (docs/specs/kunark-cap.md, "What the player sees"): every number and name comes from the page data
/// (<c>reinc.levelCap</c>, <c>reinc.newLevels</c>), so these messages are the fixture's player on 12-09 with the cap at 55.
/// </summary>
public sealed class KunarkPages : IAsyncLifetime
{
    internal static readonly JsonObject NewLevels = new() { ["expansion"] = "Kunark", ["day"] = "2026-12-08", ["from"] = 50, ["to"] = 55, ["rank"] = 0 };

    internal static JsonObject After(JsonObject m)
    {
        m["reinc"]!["levelCap"] = 55;
        m["reinc"]!["newLevels"] = NewLevels.DeepClone();
        return m;
    }

    static void Ranked(JsonObject m, int rank) { foreach (var c in m["reinc"]!["classes"]!.AsArray()) c!["rank"] = rank; }

    internal PageRun Home50 { get; private set; } = null!;
    internal PageRun Relevel { get; private set; } = null!;
    internal PageRun Camp { get; private set; } = null!;
    internal PageRun NoRoute { get; private set; } = null!;
    internal PageRun OneTo55 { get; private set; } = null!;
    internal PageRun Legs52 { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var home = After(await PageMessage.FarmingAtTheCap());
        home.With("reinc.levelCap", 55).With("reinc.currentLevel", 50).With("reinc.homeNow.level", 50).With("reinc.homeNow.mode", "leveling");
        Ranked(home, 2);
        HomeGoalPages.Put(home, "reinc.home.route.legs", new JsonArray());

        var relevel = After(await HomeGoalPages.Leveling("motes", "D4"));
        var journeys = relevel["reinc"]!["journeys"]!.AsArray();
        var open = journeys[3]!.AsObject();
        open["rank"] = 3; open["etaTo"] = 50; open["levelNow"] = 32;
        relevel["reinc"]!["xpByRank"] = new JsonArray(1.0, 1.1, 1.2, 1.3, 1.4, 1.5);
        relevel["reinc"]!["homeNow"]!["now"]!["level"] = 32;

        var camp = After(await PageMessage.FromFixture());
        var runs = camp["reinc"]!["campaign"]!["runs"]!.AsArray();
        for (int i = 0; i < 2; i++) { var r = runs[i]!.AsObject(); r["state"] = "leveling"; r["level"] = i == 0 ? 32 : 1; r["etaHours"] = i == 0 ? 6.2 : null; r["etaTo"] = i == 0 ? 50 : null; r["crawls"] = i == 0 ? 9 : 0; }
        runs[2]!["state"] = "next";

        var data = After(await PageMessage.FromFixture());
        var none = (await PageMessage.Answer(PageMessage.Level1To20)).With("route.from", 50).With("route.to", 55).With("route.legs", new JsonArray()).With("route.gaps", new JsonArray(50, 51, 52, 53, 54)).With("levelCap", 55);
        var whole = (await PageMessage.Answer(PageMessage.Level1To20)).With("route.to", 55).With("route.gaps", new JsonArray(50, 51, 52, 53, 54)).With("levelCap", 55).With("rank", 3).With("xpExtra", 30);

        var legs52 = (await PageMessage.Answer(PageMessage.Level1To20)).With("route.to", 55).With("route.gaps", new JsonArray(52, 53, 54)).With("levelCap", 55).With("rank", 3).With("xpExtra", 30);
        var lastLeg = legs52["route"]!["legs"]!.AsArray().Last()!.AsObject(); lastLeg["to"] = 52;
        legs52["route"]!["solo"] = new JsonObject { ["minutes"] = 96, ["covered"] = 49, ["places"] = new JsonArray("Befallen D4") };
        foreach (var l in legs52["route"]!["legs"]!.AsArray()) if (l!["to"]!.GetValue<int>() > 52) l["to"] = 52;
        var opened = await PageRuns.AtOnce(
            h => h.Run("home", PageStep.Send(home), PageStep.Read("home")),
            h => h.Run("home", PageStep.Send(relevel), PageStep.Read("relevel")),
            h => h.Run("campaign", PageStep.Send(camp), PageStep.Read("campaign")),
            h => h.Run("ask", PageStep.Send(data), PageStep.Send(none), PageStep.Read("none")),
            h => h.Run("ask", PageStep.Send(data), PageStep.Send(whole), PageStep.Read("whole")),
            h => h.Run("ask", PageStep.Send(data), PageStep.Send(legs52), PageStep.Read("legs52")));
        (Home50, Relevel, Camp, NoRoute, OneTo55, Legs52) = (opened.Runs[0], opened.Runs[1], opened.Runs[2], opened.Runs[3], opened.Runs[4], opened.Runs[5]);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class KunarkCapPageTests(KunarkPages pages) : IClassFixture<KunarkPages>
{
    [Fact]
    public void HomeAt50AfterLaunchIsLevelingTowards55WithNoRouteYetAndNoExtraXp()
    {
        var hero = pages.Home50.At("home").Text("homeHero");
        Assert.Contains("LEVELING", hero.ToUpperInvariant());
        Assert.Contains("50 / 55", hero);
        Assert.Contains("No route past 50 yet: Kunark opened levels 51 to 55 on 12-08", hero);
        Assert.Contains("No extra XP to level past 50", hero);
        Assert.DoesNotContain("Farming at", hero);
        Assert.DoesNotContain("Ask fills in a route once", hero);
        Assert.DoesNotContain("Ask's route is the fastest", hero);   // no route, so nothing to say about how it counts
    }

    [Fact]
    public void HomesRelevelCountsHoursToTheHighestLevelPlayedAndTheChipReadsTheLevelNow()
    {
        var run = pages.Relevel.At("relevel");
        var hero = run.Text("homeHero");
        Assert.Contains("/ 55", hero);
        Assert.Contains("to 50", hero);
        Assert.DoesNotContain("h to go", hero);
        Assert.Contains("+30% XP to level", hero);
    }

    [Fact]
    public void CampaignGivesHoursToTheLevelWithNumbersAndSaysBelow55()
    {
        var t = pages.Camp.At("campaign").Text("campBody").Replace(' ', ' ');
        Assert.Contains("32 / 55 · ≈ 6.2 h to 50", t);
        Assert.Contains("below 55", t);
    }

    [Fact]
    public void CampaignsXpColumnTitleGivesTheNewLevelsTheirUsualXp()
    {
        Assert.Contains("levels 51 to 55, new with Kunark: +0%", pages.Camp.At("campaign").Hover("campBody"));
    }

    [Fact]
    public void AskFrom50To55HasNoRouteYetAndTheStepperAndPresetsReach55()
    {
        var run = pages.NoRoute.At("none");
        var t = run.Text("askBody");
        Assert.Contains("No route past 50 yet", t);
        Assert.Contains("Kunark opened levels 51 to 55 on 12-08", t);
        Assert.Contains("1–55", t);
        Assert.Contains("50–55", t);
        Assert.DoesNotContain("46–50", t);
        Assert.DoesNotContain("No crawls between", t);
    }

    [Fact]
    public void AskFrom1To55GivesHoursToLevel50InGreyAndTheTwoPartPenaltyLine()
    {
        var t = pages.OneTo55.At("whole").Text("askBody");
        Assert.Contains("to 50", t);
        Assert.Contains("No route past 50 yet: Kunark opened", t);
        Assert.DoesNotContain("No numbers from level 50 to 55", t);
        Assert.Contains("+30% to level 50 for 3 reincarnations", t);
        Assert.Contains("its usual XP past 50", t);
    }

    [Fact]
    public void AskWithCrawlsToLevel52SaysNoRoutePast52AndGivesHoursToThere()
    {
        var t = pages.Legs52.At("legs52").Text("askBody");
        Assert.Contains("No route past 52 yet", t);
        Assert.Contains("crawls past 52 yet", t, StringComparison.Ordinal);
        Assert.Contains("to 52", t);
        Assert.DoesNotContain("No route past 50", t);
        Assert.Contains("≈ 1.6 h to 50", t);
        Assert.DoesNotContain("49 of 54", t);
    }
}
