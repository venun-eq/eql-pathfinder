using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The words that name where the farm rate comes from, in the real page (docs/specs/farm-rate-at-cap.md).</summary>
public sealed class FarmRatePages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        JsonObject Msg(string kind, int crawls, int unrated, int notFarmed, int[] diffs, bool fallback = true)
        {
            var m = PageMessage.FromFixture().GetAwaiter().GetResult();
            m.With("reinc.farmSource.kind", kind).With("reinc.farmSource.crawls", crawls).With("reinc.farmSource.unrated", unrated)
             .With("reinc.farmSource.notFarmed", notFarmed).With("reinc.farmSource.fallback", fallback)
             .With("reinc.farmSource.difficulties", new JsonArray(diffs.Select(d => (JsonNode)d).ToArray()));
            return m;
        }
        JsonObject OneCrawl() { var m = Msg("default", 1, 0, 0, [3, 4]); m["reinc"]!["farm"] = new JsonObject { ["crawls"] = 1, ["usual"] = 57, ["difficulties"] = new JsonArray(3, 4) }; return m; }
        var opened = await PageRuns.AtOnce(h => h.Run("settings",
            PageStep.Send(Msg("default", 0, 0, 8, [3, 4])), PageStep.Read("none farmed"),
            PageStep.Send(Msg("default", 2, 0, 0, [3, 4])), PageStep.Read("only two"),
            PageStep.Send(Msg("default", 0, 0, 0, [])), PageStep.Read("none ticked"),
            PageStep.Send(Msg("default", 0, 0, 0, [3, 4])), PageStep.Read("nothing at 50"),
            PageStep.Send(Msg("default", 0, 0, 5, [2])), PageStep.Read("d2 only"),
            PageStep.Send(Msg("default", 0, 0, 1, [2])), PageStep.Read("d2 one"),
            PageStep.Send(Msg("set", 0, 0, 0, [2]).With("reinc.farm.crawls", 0).With("reinc.farmSource.needs", 3)), PageStep.Read("d2 set"),
            PageStep.Send(OneCrawl()), PageStep.Read("one crawl")));
        Run = opened.Runs[0];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class FarmRateAtCapPageTests(FarmRatePages pages) : IClassFixture<FarmRatePages>
{
    private string At(string reading) => pages.Run.At(reading).Text("farmRateSetting");

    [Fact]
    public void NoneAtAFarmedDifficultyNamesTheSettingAndNeverSaysNoFarming() =>
        Assert.Contains("none of your 8 crawls at 50 since the 11-10 patch were at D3 or D4, the difficulties you farm for motes (Settings → How you play)", At("none farmed"));

    [Fact]
    public void TooLittleNamesTheDifficulties() =>
        Assert.Contains("only 2 of your D3 and D4 crawls at 50 since the 11-10 patch have a rate, and it takes 3", At("only two"));

    [Fact]
    public void NoDifficultyTickedSaysSo() =>
        Assert.Contains("you farm no difficulty for motes (Settings → How you play)", At("none ticked"));

    [Fact]
    public void NothingAtFiftyIsSaidInPlainWords() =>
        Assert.Contains("your logs have no crawls at 50 since the 11-10 patch", At("nothing at 50"));
}

public class FarmRateD2OnlyPageTests(FarmRatePages pages) : IClassFixture<FarmRatePages>
{
    [Fact]
    public void D2OnlyNamesD2InTheNoneLine() =>
        Assert.Contains("none of your 5 crawls at 50 since the 11-10 patch were at D2, the difficulty you farm for motes (Settings → How you play)", pages.Run.At("d2 only").Text("farmRateSetting"));

    [Fact]
    public void D2OnlyOneCrawlSaysWasnt() =>
        Assert.Contains("your 1 crawl at 50 since the 11-10 patch wasn't at D2, the difficulty you farm for motes", pages.Run.At("d2 one").Text("farmRateSetting"));

    [Fact]
    public void SetFallbackNamesD2() =>
        Assert.Contains("Also used until 3 of your D2 crawls at 50 since the patch have a rate.", pages.Run.At("d2 set").Text("settingsBody"));
}

/// <summary>Home's farming card at the cap: the note beside the typical row, and the row fading while a difficulty click waits.</summary>
public sealed class FarmRateHomePages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        async Task<JsonObject> Msg(int crawls, double? usual, int[] diffs)
        {
            var m = await PageMessage.FarmingAtTheCap();
            m["reinc"]!["farm"] = new JsonObject { ["crawls"] = crawls, ["usual"] = usual, ["difficulties"] = new JsonArray(diffs.Select(d => (JsonNode)d).ToArray()) };
            m.With("reinc.farmSource.difficulties", new JsonArray(diffs.Select(d => (JsonNode)d).ToArray()));
            return m;
        }
        var typical = await Msg(6, 80, [3, 4]); var unrated = await Msg(0, null, [3, 4]); unrated["reinc"]!["farm"]!["unrated"] = 2;
        var none = await Msg(0, null, [3, 4]); var unticked = await Msg(0, null, []); var d2 = await Msg(0, null, [2]);
        var opened = await PageRuns.AtOnce(
            h => h.Run("home",
                PageStep.Send(typical), PageStep.Read("typical"),
                PageStep.Send(none), PageStep.Read("none"), PageStep.Send(unrated), PageStep.Read("unrated"),
                PageStep.Send(unticked), PageStep.Read("none ticked"),
                PageStep.Send(d2), PageStep.Read("d2")),
            h => h.Run("settings",
                PageStep.Send(typical), PageStep.Click("[data-farm-diff=\"2\"]"), PageStep.Click("button[data-page=\"home\"]"), PageStep.Read("waiting"),
                PageStep.Send(typical), PageStep.Read("arrived")));
        Run = opened.Runs[0];
        Waiting = opened.Runs[1];
    }

    internal PageRun Waiting { get; private set; } = null!;
    public Task DisposeAsync() => Task.CompletedTask;
}

public class FarmRateHomeTests(FarmRateHomePages pages) : IClassFixture<FarmRateHomePages>
{
    private string Row(string reading) => pages.Run.At(reading).Text("homeTypicalFarm");

    [Fact] public void TypicalRowNamesTheCrawlsAndDifficulties() => Assert.Contains("6 crawls, D3 and D4", Row("typical"));
    [Fact] public void NoCrawlsAtAFarmedDifficultySaysWhich() => Assert.Contains("no D3 or D4 crawls yet", Row("none"));
    [Fact] public void CrawlsWithNoRateYetAreNotCalledNone() { Assert.Contains("2 crawls at D3 and D4, no rate yet", Row("unrated")); Assert.DoesNotContain("no D3 or D4 crawls", Row("unrated")); }
    [Fact] public void NoDifficultyTickedSaysSo() => Assert.Contains("no difficulty farmed", Row("none ticked"));
    [Fact] public void D2OnlyNamesD2() => Assert.Contains("no D2 crawls yet", Row("d2"));

    [Fact]
    public void TheTypicalRowFadesWhileADifficultyClickWaits()
    {
        Assert.True(pages.Waiting.At("waiting").Faded("homeTypicalFarm"));
        Assert.False(pages.Waiting.At("arrived").Faded("homeTypicalFarm"));
        Assert.False(pages.Run.At("typical").Faded("homeTypicalFarm"));
    }
}

public class FarmRateOneCrawlPageTests(FarmRatePages pages) : IClassFixture<FarmRatePages>
{
    [Fact]
    public void OneCrawlIsNotCalledTypical()
    {
        var t = pages.Run.At("one crawl").Text("farmRateSetting");
        Assert.Contains("57 from 1 crawl", t);
        Assert.DoesNotContain("typical 57", t);
    }
}
