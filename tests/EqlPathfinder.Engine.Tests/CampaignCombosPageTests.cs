using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Your combos on Campaign in headless Edge (docs/specs/campaign-your-combos.md): only with the new planner on, between the
/// campaign card and Your runs, and on the "no plan yet" page under the ready-made goals. With it off, Campaign is today's.
/// </summary>
public class CampaignCombosPageTests
{
    private static async Task<JsonObject> NewPlanner(bool plan)
    {
        var m = await PageMessage.FromFixture(plan: plan);
        var c = m["reinc"]!["campaign"]!.AsObject();
        c["newPlanner"] = JsonSerializer.SerializeToNode(NewPlannerView.Of(
            new(new(ClassRoles.DefaultCarries, true), false), new SurvivalCheck("off", null, 0), []), PageJson.Options);
        return m;
    }

    [Fact]
    public async Task TheCardShowsBetweenTheCampaignCardAndYourRunsAndSaysWhatACombosIsWhenThereAreNone()
    {
        var run = new PageHarness().Run("campaign",
            PageStep.Send(await NewPlanner(plan: true)), PageStep.Read("new"),
            PageStep.Send(await NewPlanner(plan: false)), PageStep.Read("new, no plan"));

        var now = run.At("new").Text("campBody");
        Assert.Contains("Your runs", now);
        Assert.Contains("Your combos", now);
        Assert.Contains("Try one", now, StringComparison.OrdinalIgnoreCase);   // small caps on screen
        Assert.Contains("Add a combo", now);
        Assert.True(now.IndexOf("Your combos", StringComparison.Ordinal) < now.IndexOf("Your runs", StringComparison.Ordinal), "Your combos isn't above Your runs");

        var start = run.At("new, no plan").Text("campBody");
        Assert.Contains("Your combos", start);
        Assert.Contains("Add a combo", start);
    }

    private static readonly string[] Order = ["WAR", "CLR", "PAL", "RNG", "SHD", "DRU", "MNK", "BRD", "ROG", "SHM", "NEC", "WIZ", "MAG", "ENC", "BST", "BER"];

    /// <summary>Played runs before the plan, each a trio, as the engine's CampaignPlayed reads in the page data.</summary>
    private static JsonArray PlayedRuns(params string[][] trios) => new(trios.Select((t, i) => (JsonNode)new JsonObject
    {
        ["n"] = i + 1,
        ["at"] = new DateTime(2026, 11, 1).AddDays(i).ToString("o"),
        ["race"] = "Iksar",
        ["classes"] = new JsonArray(t.Select(c => (JsonNode)c).ToArray()),
        ["gains"] = new JsonArray(),
        ["offPlan"] = false,
    }).ToArray());

    // docs/specs/campaign-combos-example.md: an empty card offers one combo to try, a trio played twice or more.
    [Fact]
    public async Task AnEmptyCardOffersTheTrioPlayedMostAndAddThisOpensTheFormFilledIn()
    {
        var m = await NewPlanner(plan: true);
        var c = m["reinc"]!["campaign"]!.AsObject();
        // WAR / CLR / RNG and SHD / ROG / SHM 3 times each, the second played later; PAL / MNK / BRD twice, played last of all.
        c["played"] = PlayedRuns(["WAR", "CLR", "RNG"], ["WAR", "CLR", "RNG"], ["ROG", "SHD", "SHM"], ["WAR", "CLR", "RNG"], ["SHM", "ROG", "SHD"],
            ["SHD", "SHM", "ROG"], ["PAL", "MNK", "BRD"], ["BRD", "PAL", "MNK"]);
        foreach (var r in c["runs"]!.AsArray()) if ((string?)r!["state"] == "done") r["state"] = "next";   // the fixture's own played runs count nothing here
        var run = new PageHarness().Run("campaign",
            PageStep.Send(m), PageStep.Read("empty"),
            PageStep.Click("[data-combo-act=\"try\"]"), PageStep.Read("open"),
            PageStep.Click("[data-combo-act=\"cancel\"]"), PageStep.Read("closed"));

        var empty = run.At("empty").Text("campBody");
        Assert.Contains("Try one", empty, StringComparison.OrdinalIgnoreCase);
        // Played most wins over played last; between two played as often, the one played last.
        Assert.Contains("Play SHD / ROG / SHM", empty);
        Assert.Contains("You've played this trio 3 times (History → Relevels).", empty);
        Assert.DoesNotContain("Nothing here yet", empty);
        Assert.DoesNotContain("Make this a combo", empty);

        var open = run.At("open").Text("campBody");
        Assert.Contains("Add a combo", open);
        Assert.Contains("SHD / ROG / SHM:", open);
        Assert.DoesNotContain("Try one", open, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Try one", run.At("closed").Text("campBody"), StringComparison.OrdinalIgnoreCase);
    }

    // A plan's run you played counts as played; one still leveling doesn't yet.
    [Fact]
    public async Task APlansDoneRunCountsAsPlayedAndALevelingRunDoesNot()
    {
        var m = await NewPlanner(plan: true);
        var c = m["reinc"]!["campaign"]!.AsObject();
        c["played"] = PlayedRuns(["SHD", "ROG", "SHM"]);
        var runs = c["runs"]!.AsArray();
        foreach (var r in runs) if ((string?)r!["state"] == "done") r["state"] = "next";
        runs[0]!["state"] = "done"; runs[0]!["classes"] = new JsonArray("SHM", "SHD", "ROG");
        runs[1]!["state"] = "leveling"; runs[1]!["classes"] = new JsonArray("ROG", "SHM", "SHD");
        var run = new PageHarness().Run("campaign", PageStep.Send(m), PageStep.Read("page"));

        var page = run.At("page").Text("campBody");
        Assert.Contains("Play SHD / ROG / SHM", page);
        Assert.Contains("You've played this trio twice (History → Relevels).", page);
    }

    [Fact]
    public async Task WithNoTrioPlayedTwiceTheCardOffersAPairFromTheFirstRunToComeAndWithNoPlanItExplains()
    {
        var m = await NewPlanner(plan: true);
        var c = m["reinc"]!["campaign"]!.AsObject();
        c["played"] = PlayedRuns(["WAR", "CLR", "RNG"], ["ROG", "SHD", "SHM"]);
        var runs = c["runs"]!.AsArray();
        foreach (var r in runs) if ((string?)r!["state"] == "done") r["state"] = "next";
        runs[0]!["state"] = "done"; runs[0]!["classes"] = new JsonArray("BER", "BST", "MAG");   // played: the pair comes from the next run
        var first = runs[1]!;
        var pair = Order.Where(k => first["classes"]!.AsArray().Any(x => (string?)x == k)).Take(2).ToArray();
        var runNo = (int)first["n"]! + ((int?)c["before"] ?? 0);
        var withCombo = await NewPlanner(plan: true);
        withCombo["reinc"]!["campaign"]!["plan"]!["combos"] = new JsonArray(new JsonObject { ["classes"] = new JsonArray("PAL", "MNK"), ["everyRun"] = true, ["times"] = 3 });
        var none = await NewPlanner(plan: false);
        none["reinc"]!["campaign"]!["played"] = new JsonArray();
        // A relevel on now, with no goal yet: the no-plan page lists no runs, so it offers no pair from one.
        none["reinc"]!["campaign"]!["runs"] = new JsonArray(new JsonObject
        {
            ["n"] = 1,
            ["loadout"] = 0,
            ["race"] = "Iksar",
            ["classes"] = new JsonArray("WAR", "CLR", "RNG"),
            ["state"] = "leveling",
            ["rankAfter"] = 0,
            ["xp"] = 0,
            ["gains"] = new JsonArray(),
            ["past"] = new JsonArray(),
            ["level"] = 30,
        });
        var run = new PageHarness().Run("campaign",
            PageStep.Send(m), PageStep.Read("pair"),
            PageStep.Send(withCombo), PageStep.Read("with a combo"),
            PageStep.Send(none), PageStep.Read("none"));

        var withPair = run.At("pair").Text("campBody");
        Assert.Contains($"{pair[0]} with {pair[1]}", withPair);
        Assert.Contains("at least once", withPair);
        Assert.Contains($"Together in run {runNo} already.", withPair);

        var has = run.At("with a combo").Text("campBody");
        Assert.DoesNotContain("Try one", has, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Make this a combo", has);

        var empty = run.At("none").Text("campBody");
        Assert.DoesNotContain("Try one", empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nothing here yet. A combo can be a trio you want to play, say 3 times, or two classes you never want in one run.", empty);
        Assert.DoesNotContain("Make this a combo", empty);
    }
}
