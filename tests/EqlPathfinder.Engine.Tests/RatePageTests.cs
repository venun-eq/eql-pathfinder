using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// History → Crawls under the one rule for a rate (docs/specs/one-rate.md, the user, 11-18): a visit with no rate (the engine
/// sends null) shows "—" with its hover words, "Hide visits too short to judge" hides exactly those rows, and the T5-eq tile
/// follows the same rule over the visits shown. The page draws what the engine sent and works nothing out.
/// </summary>
public sealed class RatePages : IAsyncLifetime
{
    internal PageRun Crawls { get; private set; } = null!;
    internal JsonObject Message { get; private set; } = null!;

    /// <summary>A finished crawl with every rate, and a short unfinished one in "Quiet Hollow" with none (as the engine sends it).</summary>
    private static JsonObject Row(JsonObject like, bool finished)
    {
        var row = (JsonObject)JsonNode.Parse(like.ToJsonString())!;
        row["tooShort"] = !finished; row["difficulty"] = 4; row["completed"] = finished; row["kills"] = finished ? 30 : 2; row["minutes"] = finished ? 9.0 : 2.0;
        row["baseZone"] = finished ? "Kedge Keep" : "Quiet Hollow";
        foreach (var k in new[] { "levelsPerHour", "t5PerHour", "killsPerMinute", "dps" }) row[k] = finished ? 7.5 : null;
        return row;
    }

    public async Task InitializeAsync()
    {
        Message = await PageMessage.FromFixture();
        var like = (JsonObject)Message["history"]!.AsArray()[0]!;
        var both = (JsonObject)JsonNode.Parse(Message.ToJsonString())!;
        both["history"] = new JsonArray(Row(like, true), Row(like, false));
        var shortOnly = (JsonObject)JsonNode.Parse(Message.ToJsonString())!;
        shortOnly["history"] = new JsonArray(Row(like, false));
        var atCap = (JsonObject)JsonNode.Parse(Message.ToJsonString())!;
        var capRow = Row(like, false); capRow["tooShort"] = false;   // no rate and not a raid: the page says too short whatever the level
        atCap["history"] = new JsonArray(capRow);
        var opened = await PageRuns.AtOnce(h => h.Run("crawls",
            PageStep.Send(both), PageStep.Read("hidden"),
            PageStep.Click("#hideShort"), PageStep.Read("all"),
            PageStep.Send(shortOnly), PageStep.Read("only short"),
            PageStep.Click("#hideShort"), PageStep.Read("only short, box on"),
            PageStep.Send(atCap), PageStep.Read("cap visit")));
        Crawls = opened.Runs[0];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class RatePageTests(RatePages pages) : IClassFixture<RatePages>
{
    private string Words => $"it starts at {pages.Message.Number("snapshot.session.rateFromKills"):0} kills, or when the crawl is finished.";

    [Fact]
    public void HideVisitsTooShortToJudgeHidesExactlyTheRowsWithNoRate()
    {
        var hidden = pages.Crawls.At("hidden");
        Assert.Contains("Kedge Keep", hidden.Text("crawlTable"));
        Assert.DoesNotContain("Quiet Hollow", hidden.Text("crawlTable"));
        Assert.Contains("1 of 2", hidden.Text("crawlCount"));
        var all = pages.Crawls.At("all");
        Assert.Contains("Quiet Hollow", all.Text("crawlTable"));
        Assert.Contains("2 of 2", all.Text("crawlCount"));
    }

    [Fact]
    public void ARowWithNoRateShowsADashAndTheWordsOnHover()
    {
        var all = pages.Crawls.At("all");
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(all.Text("crawlTable"), "—").Count);
        Assert.Contains($"No rate: under {pages.Message.Number("snapshot.session.rateFromKills"):0} kills and not finished.", all.Hover("crawlTable"));
        Assert.Contains("too short", all.Text("crawlTable"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(all.Text("crawlTable"), "too short"));
        Assert.Contains("Levels an active hour", all.Hover("crawlTable"));
        Assert.Contains("Damage a second of active time, idle taken out", all.Hover("crawlTable"));
        Assert.Contains("7.5", all.Text("crawlTable"));
    }

    [Fact]
    public void WhenTheBoxHidesEveryVisitTheEmptyRowSaysWhy()
    {
        var text = pages.Crawls.At("only short, box on").Text("crawlTable");
        Assert.Contains($"1 visit too short to judge is hidden: {Words}", text);
        Assert.DoesNotContain("No visits match.", text);
    }

    [Fact]
    public void TheT5TileIsADashWhenNoVisitShownHasARate()
    {
        var tile = pages.Crawls.At("only short").Text("crawlTiles");
        Assert.Contains("—", tile);
        Assert.Contains($"T5-eq per active hour. The rate: {Words}", tile);
        Assert.DoesNotContain("T5-eq per active hour\n", tile);
    }

    [Fact]
    public void ACapVisitWithNoRateReadsTooShortLikeAnyOther()
    {
        // kills-at-cap: kills at the cap count, so a visit there with no rate is too short like any other, with no "aren't counted yet".
        var cap = pages.Crawls.At("cap visit");
        Assert.Contains("Quiet Hollow", cap.Text("crawlTable"));
        Assert.Contains("too short", cap.Text("crawlTable"));
        Assert.DoesNotContain("aren't counted yet", cap.Hover("crawlTable"));
    }
}
