using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Upgrades;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// What the Upgrades page is told (docs/quality.md): the answer the engine builds, written with PageJson.Options as the
/// window writes it. It was put together in the window (MainWindow.UpgradeMessage), where no test could build it, so
/// a page test of Upgrades would have drawn from an answer written by hand. Every name upgrade.js reads is listed
/// here as the window sent it: a field renamed or dropped in the move fails this, not a player's Upgrades page.
/// </summary>
public class UpgradesAnswerTests
{
    /// <summary>A game folder of this test's own: its Logs folder, and beside it the export when <paramref name="export"/> is given.</summary>
    private static JsonObject Answer(string[]? export, MoteBank bank, object? request)
    {
        using var game = new TempFolder();
        var logs = Directory.CreateDirectory(Path.Combine(game.Path, "Logs")).FullName;
        if (export != null) File.WriteAllLines(InventoryExport.PathFor(logs, TestLog.Character, TestLog.Server), export);
        var answer = UpgradePage.Build(TestLog.Logs(Path.Combine(logs, TestLog.LiveName)), bank, [],
            request == null ? null : JsonSerializer.SerializeToElement(request));
        return JsonNode.Parse(JsonSerializer.Serialize(answer, PageJson.Options))!.AsObject();
    }

    private static string[] Names(JsonNode? o) => o!.AsObject().Select(p => p.Key).ToArray();

    [Fact]
    public void TheUpgradesAnswerHasYourGearYourBankAndThePlanYouAskedFor()
    {
        var bank = new MoteBank { EnteredAt = new DateTime(2026, 11, 14, 13, 0, 0), Vtps = 1 };
        bank.Motes[5] = 40;
        bank.Fragments[6] = 3;
        // What upgrade.js posts (requestUpgrade): the item's +N and goal, and its other copies to merge in.
        var a = Answer(PageMessage.InventoryExportLines, bank, new { target = "gear", tier = 5, progress = 0, goal = 7, vtps = 0, copies = new[] { new { tier = 5, progress = 0 } } });

        Assert.Equal(["kind", "inventory", "items", "bank", "plan"], Names(a));
        Assert.Equal("upgrade", (string?)a["kind"]);

        Assert.Equal(["file", "exists", "time", "error"], Names(a["inventory"]));
        Assert.Equal($"{TestLog.Character}_{TestLog.Server}-Inventory.txt", a.String("inventory.file"));
        Assert.True((bool)a["inventory"]!["exists"]!);
        Assert.Equal(JsonValueKind.String, a["inventory"]!["time"]!.GetValueKind());
        Assert.Null(a["inventory"]!["error"]);

        // The gear is the export's, as InventoryExport reads it: each name with its +N, where it is and how many.
        var gear = JsonSerializer.SerializeToNode(InventoryExport.ParseGear(PageMessage.InventoryExportLines), PageJson.Options);
        Assert.True(JsonNode.DeepEquals(gear, a["items"]), $"items: {a["items"]?.ToJsonString()}");
        Assert.Equal(["name", "tier", "place", "count"], Names(a.List("items")[0]));

        Assert.Equal(["entered", "enteredAt", "motes", "fragments", "vtps", "since"], Names(a["bank"]));
        Assert.Equal(["motes", "fragments", "vtps"], Names(a["bank"]!["entered"]));
        Assert.Equal(40, a.Number("bank.motes.5"));
        Assert.Equal(40, a.Number("bank.entered.motes.5"));
        Assert.Equal(3, a.Number("bank.fragments.6"));
        Assert.Equal(1, a.Number("bank.vtps"));
        Assert.Equal(11, a.List("bank.motes").Count);
        Assert.Contains("motesLooted", Names(a["bank"]!["since"]));

        // The plan is the planner's for that request: from +5 to +7, with the other copy merged in.
        Assert.Equal(5, a.Number("plan.fromTier"));
        Assert.Equal(7, a.Number("plan.goal"));
        Assert.Single(a.List("plan.merges"));
    }

    /// <summary>Nothing yet: no /outputfile inventory, an empty bank and nothing asked. The answer says there's no export, names the file to make, and has no gear and no plan.</summary>
    [Fact]
    public void WithNoInventoryExportTheUpgradesAnswerSaysSoAndHasNoGearOrPlan()
    {
        var a = Answer(null, new MoteBank(), null);
        Assert.Equal(["kind", "inventory", "items", "bank", "plan"], Names(a));
        Assert.False((bool)a["inventory"]!["exists"]!);
        Assert.Equal($"{TestLog.Character}_{TestLog.Server}-Inventory.txt", a.String("inventory.file"));
        Assert.Null(a["inventory"]!["time"]);
        Assert.Empty(a.List("items"));
        Assert.Null(a["bank"]!["enteredAt"]);
        Assert.Equal(0, a.List("bank.motes").Sum(n => n!.GetValue<int>()));
        Assert.Null(a["plan"]);
    }

    /// <summary>A spell has no copies: the plan is for the spell's tier, and copies in the request are left out, as the window did.</summary>
    [Fact]
    public void ASpellsPlanLeavesCopiesOut()
    {
        var a = Answer(null, new MoteBank(), new { target = "spell", tier = 2, progress = 1, goal = 4, vtps = 0, copies = new[] { new { tier = 2, progress = 0 } } });
        Assert.Equal(2, a.Number("plan.fromTier"));
        Assert.Equal(4, a.Number("plan.goal"));
        Assert.True(a["plan"]!["merges"] is null or JsonArray { Count: 0 }, $"merges: {a["plan"]!["merges"]?.ToJsonString()}");
    }
}
