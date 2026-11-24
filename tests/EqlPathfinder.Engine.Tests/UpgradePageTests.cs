using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Upgrades in headless Edge for every test here, in one run: the engine's answer for a made-up inventory export
/// (PageMessage.InventoryExportLines), then a player clicking into the item box, typing, and picking from the list.
/// </summary>
public sealed class UpgradePages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;
    internal JsonObject Answer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var data = await PageMessage.FromFixture();
        Answer = PageMessage.UpgradesAnswer();
        var opened = await PageRuns.AtOnce(h => h.Run("upgrade",
            PageStep.Send(data), PageStep.Send(Answer),
            PageStep.Click("#upItem"), PageStep.Read("opened"),
            PageStep.Type("#upItem", "robe"), PageStep.Read("robe"),
            PageStep.Type("#upItem", "fool cud"), PageStep.Read("fool cud"),
            PageStep.Type("#upItem", "zzz"), PageStep.Read("no match"),
            PageStep.Type("#upItem", "cud"), PageStep.Click("#upItems .opt")));
        Run = opened.Runs[0];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// What the Upgrades item box does (docs/quality.md): the real page in headless Edge, read as a player reads it.
/// WebView2's own popup for a datalist listed your gear and didn't filter as you typed (a report in 0.9.34), so the
/// page draws its own list. AppPageTests still reads that no page leans on a datalist and that the box's markup names
/// its list; that the list filters was proven by nothing until this.
/// </summary>
public class UpgradePageTests(UpgradePages pages) : IClassFixture<UpgradePages>
{
    private static readonly string[] Gear = ["Cloak of Flames", "Cudgel of the Fool", "Pyrosilk Robe", "Robe of the Oracle", "Spear of Harmony"];

    /// <summary>The names the engine read from the export are the ones these tests are about: a change to the made-up export shows here.</summary>
    private void TheAnswerHasThatGear() =>
        Assert.Equal(Gear, pages.Answer.List("items").Select(i => (string?)i!["name"]).Order(StringComparer.Ordinal));

    /// <summary>Clicking into the box lists every piece of gear in the export, each name once with its copies and where they are, and nothing that isn't gear.</summary>
    [Fact]
    public void TheUpgradesItemBoxListsYourGearWhenYouClickIntoIt()
    {
        TheAnswerHasThatGear();
        var opened = pages.Run.At("opened");
        Assert.Contains("Gear from " + pages.Answer.String("inventory.file"), opened.Text("upInvNote"));
        var list = opened.Text("upItems");
        foreach (var name in Gear) Assert.Contains(name, list);
        Assert.Contains("Cudgel of the Fool 2 copies\n+5 Equipment · +5 Equipment", list);
        Assert.Contains("Pyrosilk Robe\n+9 Worn", list);
        Assert.DoesNotContain("Bone Chips", list);
    }

    /// <summary>
    /// The list keeps only the gear with every typed word in its name, in any order, names that start with what was
    /// typed first; when nothing matches it says so and where to go on.
    /// </summary>
    [Fact]
    public void TheUpgradesItemBoxFiltersYourGearAsYouType()
    {
        TheAnswerHasThatGear();
        var robe = pages.Run.At("robe").Text("upItems");
        Assert.Contains("Robe of the Oracle", robe);
        Assert.Contains("Pyrosilk Robe", robe);
        Assert.True(robe.IndexOf("Robe of the Oracle", StringComparison.Ordinal) < robe.IndexOf("Pyrosilk Robe", StringComparison.Ordinal), "the name that starts with what was typed comes first:\n" + robe);
        foreach (var other in new[] { "Cloak of Flames", "Cudgel of the Fool", "Spear of Harmony" }) Assert.DoesNotContain(other, robe);

        var words = pages.Run.At("fool cud").Text("upItems");
        Assert.Contains("Cudgel of the Fool", words);
        foreach (var other in Gear.Where(g => g != "Cudgel of the Fool")) Assert.DoesNotContain(other, words);

        var none = pages.Run.At("no match").Text("upItems");
        Assert.Contains("None of your gear matches. Set its +N below.", none);
        foreach (var name in Gear) Assert.DoesNotContain(name, none);
    }

    /// <summary>
    /// Picking from the list closes it, lists the item's copies from the export under the box, and asks the window for
    /// the plan: the item's +N with the other copy to merge in. The page posts that 120 ms after the last change.
    /// </summary>
    [Fact]
    public void PickingGearFromTheListShowsItsCopiesAndAsksTheWindowForThePlan()
    {
        TheAnswerHasThatGear();
        Assert.Contains("Cudgel of the Fool", pages.Run.At("opened").Text("upItems"));   // the list was open before the pick
        Assert.Equal("", pages.Run.Text("upItems"));
        var copies = pages.Run.Text("upCopies");
        Assert.Contains("Your copies", copies);
        Assert.Contains("Kept · the others merge into it", copies);
        Assert.Contains("Merges in", copies);
        Assert.Contains("Found in your inventory export.", copies);

        var asked = pages.Run.PostedAs("upgrade");
        Assert.NotEmpty(asked);
        var plan = asked[^1]["value"]!.AsObject();
        Assert.Equal("gear", (string?)plan["target"]);
        Assert.Equal(5, (int?)plan["tier"]);
        Assert.Equal(10, (int?)plan["goal"]);
        Assert.Equal("""[{"tier":5,"progress":0}]""", plan["copies"]!.ToJsonString());
    }
}
