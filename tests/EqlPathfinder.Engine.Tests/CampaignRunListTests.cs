using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Your runs as a table by round (docs/specs/campaign-run-list.md): the groups in order with their headers, Fold, the key,
/// the played band, and no "Two pets" line. The columns and colours are CSS, proved by the shots; these read the words.
/// </summary>
public sealed class CampaignRunListPages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;
    internal JsonObject Planned { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Planned = await PageMessage.FromFixture();
        // Two runs played before the plan, and the plan's first run reincarnated and leveling.
        var played = await PageMessage.FromFixture();
        var c = played["reinc"]!["campaign"]!.AsObject();
        c["before"] = 2; c["unseen"] = 0;
        c["played"] = new JsonArray(
            new JsonObject { ["n"] = 1, ["at"] = "2026-11-01T12:00:00", ["race"] = "Human", ["classes"] = new JsonArray("PAL", "MNK", "SHM"), ["gains"] = new JsonArray(), ["offPlan"] = false },
            new JsonObject { ["n"] = 2, ["at"] = "2026-11-02T12:00:00", ["race"] = "Troll", ["classes"] = new JsonArray("SHD", "ROG", "BST"), ["gains"] = new JsonArray(), ["offPlan"] = false });
        var runs = c["runs"]!.AsArray();
        var first = runs[0]!.AsObject();
        first["state"] = "leveling"; first["level"] = 32; first["etaHours"] = 6.2; first["crawls"] = 9;
        runs[1]!.AsObject()["state"] = "next";
        c["done"] = 1; c["left"] = runs.Count - 1; c["leveling"] = 1;
        // The plan's own first run played and back at the cap: a done run with the date it reincarnated.
        var withDone = await PageMessage.FromFixture();
        var dc = withDone["reinc"]!["campaign"]!.AsObject();
        var druns = dc["runs"]!.AsArray();
        var d0 = druns[0]!.AsObject();
        d0["state"] = "done"; d0["at"] = "2026-11-03T12:00:00"; d0["rankAfter"] = 1;
        d0["gains"] = new JsonArray(new JsonObject { ["kind"] = "race", ["key"] = (string)d0["race"]!, ["rank"] = 1 });
        druns[1]!.AsObject()["state"] = "next";
        dc["done"] = 1; dc["left"] = druns.Count - 1;
        var opened = await PageRuns.AtOnce(h => h.Run("campaign",
            PageStep.Send(Planned), PageStep.Read("planned"),
            PageStep.Click("[data-camp=\"fold\"][data-g=\"r1\"]"), PageStep.Read("folded"),
            PageStep.Click("[data-camp=\"fold\"][data-g=\"r1\"]"), PageStep.Send(played), PageStep.Read("played folded"),
            PageStep.Click("[data-camp=\"played\"]"), PageStep.Read("played shown"),
            PageStep.Send(withDone), PageStep.Read("done shown")));
        Run = opened.Runs[0];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class CampaignRunListTests(CampaignRunListPages pages) : IClassFixture<CampaignRunListPages>
{
    private string Main(string at) => pages.Run.At(at).Text("campMain");

    [Fact]
    public void TheGroupsReadInOrderWithTheirHeadersWords()
    {
        var t = Main("planned");
        string[] words = ["Round 1 · classes to 1/3", "Round 2 · classes to 2/3", "Round 3 · classes to 3/3", "Races", "every class is 3/3 by now"];
        int[] at = words.Select(s => t.IndexOf(s, StringComparison.Ordinal)).ToArray();
        Assert.All(at, i => Assert.True(i >= 0, t));
        Assert.Equal(at.Order(), at);
        Assert.Contains("XP to level", t);
        Assert.Contains("Completionist 1 at run", t);
        Assert.Contains("Completionist 3 at run", t);
        Assert.DoesNotContain("Completionist 4", t);
        Assert.Equal(4, t.Split("Fold").Length - 1);   // every group to come has Fold
    }

    [Fact]
    public void TheKeyNamesTheMarksOnItsOwnLine()
    {
        var t = Main("planned");
        Assert.Contains("Pips show each rank once the line is played:", t);
        Assert.Contains("you have it", t);
        Assert.Contains("this plan earns it", t);
        Assert.Contains("not yet", t);
        Assert.Contains("reaches 3/3 here", t);
        Assert.Contains("3/3 already: more XP to level, no passive", t);
    }

    [Fact]
    public void NoLineWarnsAboutTwoPetsOrAnUnearnedRacePassive()
    {
        foreach (var at in new[] { "planned", "folded", "played shown" })
        {
            var t = Main(at);
            Assert.DoesNotContain("Two pets", t);
            Assert.DoesNotContain("no race passive", t);
            Assert.DoesNotContain("only one is up", t);
        }
    }

    [Fact]
    public void FoldKeepsTheHeaderWithItsLoadoutCountAndHidesTheLines()
    {
        var before = Main("planned");
        var after = Main("folded");
        Assert.Matches(@"Round 1 · classes to 1/3[^\n]*· \d+ loadouts", after.Replace("\n", " ").Replace("Round 2", "\nRound 2"));
        Assert.Contains("Show", after);
        Assert.Equal(3, after.Split("Fold").Length - 1);
        Assert.True(after.Length < before.Length, "the folded round's lines are gone");
    }

    [Fact]
    public void PlayedIsAFoldedBandOverTheRunsBeforeAndShowListsThem()
    {
        var folded = Main("played folded");
        Assert.Contains("Played · runs 1–2", folded);
        Assert.Contains("in your ranks already", folded);
        Assert.DoesNotContain("Troll", folded.Split("Leveling now")[0]);
        var shown = Main("played shown");
        Assert.Contains("Troll", shown);
        Assert.Contains("Hide", shown);
    }

    /// <summary>A plan run already finished sits in the Played band as a played line with its date, not as a loadout line to play.</summary>
    [Fact]
    public void ADoneRunIsAPlayedLineWithItsDate()
    {
        var folded = Main("done shown");
        Assert.Contains("Played · run", folded);
        Assert.Contains("Nov 3", folded);
        Assert.DoesNotContain("once this run is played", folded);
    }

    [Fact]
    public void ALevelingRunHasItsOwnGroupWithItsLevelAndHoursAndTheNextRunIsMarked()
    {
        var t = Main("played folded").Replace((char)0xA0, (char)32);
        Assert.Contains("Leveling now", t);
        Assert.Contains("1 run reincarnated, below", t);
        Assert.Contains("≈ 6.2 h to go", t);
        Assert.Contains("32 /", t);
        Assert.Contains("next", t, StringComparison.OrdinalIgnoreCase);
        Assert.True(t.IndexOf("Leveling now", StringComparison.Ordinal) < t.IndexOf("Round 1", StringComparison.Ordinal));
    }
}
