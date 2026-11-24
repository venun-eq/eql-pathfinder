using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Numbers the engine works out and the page only draws (docs/specs/page-math-to-engine.md). Each test sends the page a
/// number the page's own formula would not give from the rest of the message, and reads which one is drawn.
/// </summary>
public sealed class PageMathPages : IAsyncLifetime
{
    internal PageRun Versus { get; private set; } = null!;
    internal PageRun XpPenalty { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // The rows' raw numbers say 20% slower, kill speed only, and about the same overall (-5%). The engine's fields say
        // 20% slower for both causes, and 30% quicker overall: the page must show the engine's.
        var answer = await PageMessage.Answer(PageMessage.Level1To20, countsSet: true);
        answer["community"] = JsonNode.Parse("""{ "leftYouOut": false, "minPlayers": 3 }""");
        answer["route"]!["versus"] = JsonNode.Parse("""
            [ { "place": "Estate of Unrest D4", "from": 20, "to": 23, "levels": 3, "mine": 12, "theirs": 10, "crawls": 2, "players": 2,
                "killsMine": 2, "killsTheirs": 2.5, "gap": 0.2, "why": "both", "killPercent": 21, "perKillPercent": 13, "slower": true, "tone": "warn" },
              { "place": "Najena D4", "from": 20, "to": 23, "levels": 3, "mine": 6, "theirs": 8, "crawls": 2, "players": 2,
                "killsMine": 3, "killsTheirs": 3, "gap": -0.25, "why": "", "killPercent": 0, "perKillPercent": 0, "slower": false, "tone": "good" } ]
            """);
        answer["route"]!["versusGap"] = -0.3;
        answer["route"]!["versusSame"] = false;
        // Every rank needs 50% more XP, as nothing in the page's own formula would say.
        var data = await PageMessage.WithCountsSet();
        data.With("reinc.classes.0.rank", 2);
        var key = data.String("reinc.classes.0.key");
        data.With("reinc.xpByRank", new JsonArray(Enumerable.Range(0, 31).Select(_ => (JsonNode?)JsonValue.Create(1.5)).ToArray()));
        var plain = await PageMessage.WithCountsSet();

        var first = await PageMessage.Answer(PageMessage.LevelFromHere, countsSet: true);
        var second = await PageMessage.Answer(PageMessage.Level1To20, countsSet: true);
        var opened = await PageRuns.AtOnce(
            h => h.Run("ask", PageStep.Send(plain), PageStep.Send(first),
                PageStep.Click("#askBody [data-ask-range=\"1,20\"]"), PageStep.Send(answer), PageStep.Read("versus")),
            h => h.Run("roster", PageStep.Send(data), PageStep.Click($"#page-roster [data-pick=\"{key}\"]"), PageStep.Read("penalty")));
        (Versus, XpPenalty) = (opened.Runs[0], opened.Runs[1]);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class PageMathTests(PageMathPages pages) : IClassFixture<PageMathPages>
{
    [Fact]
    public void AsksVersusPanelDrawsTheEnginesGapAndItsSplit()
    {
        var ask = pages.Versus.At("versus").Text("askBody");
        Assert.Contains("You take 30% less time a level than players who share", ask);
        Assert.Contains("Slower at Estate of Unrest D4.", ask);
        Assert.Contains("Both: you kill 21% slower (2.0 kills a minute against their 2.5), and each kill fills 13% less of a level.", ask);
        Assert.Contains("25% less", ask);
    }

    [Fact]
    public void ThePagesReadTheXpPenaltyFromTheEngineNotFromAFormulaOfTheirOwn()
    {
        var ask = pages.XpPenalty.At("penalty").Text("page-roster");
        Assert.Contains("needs at least +50% XP to level", ask);
    }
}
