using System.Globalization;
using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Loot's tiles in headless Edge: the share from Reward Chests, with chest fragments at their worth.</summary>
public class LootTilesPageTests
{
    /// <summary>
    /// A fragment counts at the fragment value in the total, so the chest's share has to count it the same way.
    /// Six motes from kills and three chest fragments of the same tier: the chest's share is its fragments' worth
    /// over everything's, not three over the total.
    /// </summary>
    [Fact]
    public async Task LootsChestShareCountsChestFragmentsAtTheirWorth()
    {
        var message = (await PageMessage.FromFixture()).With("history", new JsonArray());
        var fv = message.Number("settings.fragmentValue");
        var rows = new JsonObject
        {
            ["kind"] = "loot",
            ["rows"] = new JsonArray(
                new JsonObject { ["t"] = "2026-11-01T10:00:00", ["k"] = 0, ["r"] = 5, ["n"] = 6, ["c"] = 0, ["s"] = "a", ["p"] = "Z", ["i"] = 0 },
                new JsonObject { ["t"] = "2026-11-01T10:01:00", ["k"] = 1, ["r"] = 5, ["n"] = 3, ["c"] = 1, ["s"] = "chest", ["p"] = "Z", ["i"] = 0 }),
        };
        var run = new PageHarness().Run("loot", PageStep.Send(message), PageStep.Send(rows));
        var expected = Math.Round(3 * fv / (6 + 3 * fv) * 100).ToString("0", CultureInfo.InvariantCulture) + "% from Reward Chests";
        // The number and its label are on separate lines in the page's text.
        Assert.Contains(expected.Replace("% ", "%\n"), run.Text("lootTiles"));
    }
}
