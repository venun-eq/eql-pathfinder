using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// History → Visits (docs/specs/visits-tab.md): the tab's name, one button per kind of visit with its count, the Time column
/// and a raid's Done. The page draws what the engine sent: each row's kind, whole time and four parts, and the raid's boss.
/// </summary>
public sealed class VisitsPages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;

    /// <summary>The fixture's first visit made into one of a given kind and clock: the numbers the engine would send for it.</summary>
    private static JsonObject Row(JsonObject like, string zone, string? kind, double whole, string start, double active, double idle, double after, double outside,
        string? zonedOut, string? boss = null, bool completed = false, bool tooShort = false, string state = "left")
    {
        var row = (JsonObject)JsonNode.Parse(like.ToJsonString())!;
        row["baseZone"] = zone; row["zone"] = zone; row["difficulty"] = 4; row["kind"] = kind; row["tooShort"] = tooShort; row["completed"] = completed;
        row["start"] = start; row["zonedOut"] = zonedOut; row["state"] = state; row["boss"] = boss;
        row["wholeMinutes"] = whole; row["activeMinutes"] = active; row["idleMinutes"] = idle; row["afterMinutes"] = after; row["outsideMinutes"] = outside;
        return row;
    }

    public async Task InitializeAsync()
    {
        var message = await PageMessage.FromFixture();
        var like = (JsonObject)message["history"]!.AsArray()[0]!;
        message["history"] = new JsonArray(
            Row(like, "Alpha Keep", "leveling", 17.3, "2026-11-18T16:46:00", 15.4, 0, 1.8, 0, "2026-11-18T17:03:00", completed: true),
            Row(like, "Beta Hollow", "farming", 67, "2026-11-18T12:00:00", 60, 0, 5, 2, "2026-11-18T13:07:00"),
            Row(like, "Gamma Pass", "farming", 14.4, "2026-11-18T11:00:00", 14.4, 0, 0, 0, "2026-11-18T11:14:00"),
            Row(like, "Delta Sanctum - Group", "raid", 30, "2026-11-18T10:00:00", 30, 0, 0, 0, "2026-11-18T10:30:00", boss: "Lord Dread"),
            Row(like, "Zeta Spire - Solo", "raid", 20, "2026-11-18T09:00:00", 20, 0, 0, 0, "2026-11-18T09:20:00"),
            Row(like, "Epsilon Ruins", null, 9, "2026-11-18T08:00:00", 9, 0, 0, 0, "2026-11-18T08:09:00"),
            Row(like, "Eta Cave", "leveling", 2, "2026-11-18T07:00:00", 2, 0, 0, 0, "2026-11-18T07:02:00", tooShort: true),
            Row(like, "Theta Vault", "farming", 12, "2026-11-18T06:00:00", 12, 0, 0, 0, null),
            Row(like, "Iota Mine", "farming", 20, "2026-11-18T05:00:00", 20, 0, 0, 0, "2026-11-18T05:20:00", state: "paused"));
        Message = message;
        var opened = await PageRuns.AtOnce(h => h.Run("crawls",
            PageStep.Send(message), PageStep.Read("all"),
            PageStep.Click("#visitKindSeg button[data-v=\"raid\"]"), PageStep.Read("raids"),
            PageStep.Click("#visitKindSeg button[data-v=\"none\"]"), PageStep.Read("not judged"),
            PageStep.Click("#visitKindSeg button[data-v=\"farming\"]"), PageStep.Type("#q", "Alpha"), PageStep.Read("farming, searched")));
        Run = opened.Runs[0];
    }

    internal JsonObject Message { get; private set; } = null!;
    public Task DisposeAsync() => Task.CompletedTask;
}

public class VisitsPageTests(VisitsPages pages) : IClassFixture<VisitsPages>
{
    private static string Flat(string text) => Regex.Replace(text, @"\s+", " ");

    [Fact]
    public void TheTabIsCalledVisits()
    {
        var text = pages.Run.At("all").Text("page-crawls");
        Assert.Contains("Visits", text);
        Assert.DoesNotContain("Crawls", text);
        Assert.Contains("Alpha Keep", text);
    }

    [Fact]
    public void TheKindsAreCountedFromWhatTheSearchAndTheBoxLetThrough()
    {
        var all = Flat(pages.Run.At("all").Text("visitKindSeg"));
        Assert.Contains("All 8", all);     // the too-short visit is left out of All and of its kind while the box is ticked
        Assert.DoesNotContain("Eta Cave", pages.Run.At("all").Text("crawlTable"));
        Assert.Contains("Leveling 1", all);
        Assert.Contains("Farming 4", all);
        Assert.Contains("Raids 2", all);
        Assert.Contains("Not judged 1", all);
        Assert.DoesNotContain("Grouped", all);     // a kind with no visits is left out
        var searched = Flat(pages.Run.At("farming, searched").Text("visitKindSeg"));
        Assert.Contains("All 1", searched);
        Assert.Contains("Leveling 1", searched);
        Assert.DoesNotContain("Raids", searched);
    }

    [Fact]
    public void ChoosingAKindShowsOnlyItsVisits()
    {
        var raids = pages.Run.At("raids").Text("crawlTable");
        Assert.Contains("Delta Sanctum", raids);
        Assert.Contains("Zeta Spire", raids);
        Assert.DoesNotContain("Alpha Keep", raids);
        Assert.DoesNotContain("Epsilon Ruins", raids);
        Assert.Contains("2 of 9", pages.Run.At("raids").Text("crawlCount"));
        var none = pages.Run.At("not judged").Text("crawlTable");
        Assert.Contains("Epsilon Ruins", none);
        Assert.DoesNotContain("Delta Sanctum", none);
        Assert.Contains("In no pace or rate", pages.Run.At("not judged").Hover("visitKindSeg"));
    }

    [Fact]
    public void AChosenKindWithNoVisitsStaysAndTheTableSaysSo()
    {
        var run = pages.Run.At("farming, searched");
        Assert.Contains("Farming 0", Flat(run.Text("visitKindSeg")));
        Assert.Contains("No visits match.", run.Text("crawlTable"));
    }

    [Fact]
    public void TheTimeColumnReadsInMinutesAndHours()
    {
        var table = Flat(pages.Run.At("all").Text("crawlTable"));
        Assert.Contains("Time", table);
        Assert.Contains("17 min", table);
        Assert.Contains("14 min", table);
        Assert.Contains("1 h 07", table);
        Assert.Contains("From zoning in to zoning out", pages.Run.At("all").Hover("crawlTable"));
    }

    [Fact]
    public void TheTimeTooltipSaysWhereTheMinutesWent()
    {
        var hover = pages.Run.At("all").Hover("crawlTable");
        Assert.Contains("Zoned in 16:46, out 17:03: 17.3 min", hover);
        Assert.Contains("Active 15.4", hover);
        Assert.Contains("Idle 0.0", hover);
        Assert.Contains("After it completed (looting, walking out) 1.8", hover);
        Assert.Contains("Outside the instance (a death, an errand) 2.0", hover);
        // A raid never completes: its tooltip has no "After it completed" line.
        var raid = pages.Run.At("raids").Hover("crawlTable");
        Assert.Contains("Zoned in 10:00, out 10:30: 30.0 min", raid);
        Assert.DoesNotContain("After it completed", raid);
    }

    [Fact]
    public void ASoFarHeaderSaysWhetherTheVisitIsStillInsideOrNotBackYet()
    {
        var hover = pages.Run.At("all").Hover("crawlTable");
        Assert.Contains("Zoned in 06:00, still inside: 12.0 min so far", hover);
        Assert.Contains("Zoned in 05:00, out 05:20, not back yet: 20.0 min so far", hover);
    }

    [Fact]
    public void ARaidIsDoneWhenItsBossDied()
    {
        var raids = pages.Run.At("raids");
        Assert.Single(Regex.Matches(raids.Text("crawlTable"), "✓"));
        Assert.Contains("Its boss died: Lord Dread", raids.Hover("crawlTable"));
        Assert.Contains("A raid never says so", raids.Hover("crawlTable"));
        Assert.Contains("No boss kill", raids.Text("crawlTable"));
    }
}
