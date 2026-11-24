using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Ask in headless Edge for every test here, two runs at once. Each answer is the engine's to the question the page
/// had just posted, sent after it as the window sends it: the tests hold the questions the page posted to the ones
/// the answers were built for.
/// </summary>
public sealed class AskPages : IAsyncLifetime
{
    /// <summary>
    /// "Level from A to B": the "1–20" button, answered for the player with a count set (read as "counts set") and then,
    /// after page data with none, for the player as the log has them (read as "asks"); then a click on Set counts.
    /// </summary>
    internal PageRun Level { get; private set; } = null!;
    internal JsonObject AsLogged { get; private set; } = null!;
    internal JsonObject RouteAsLogged { get; private set; } = null!;
    internal JsonObject RouteCountsSet { get; private set; } = null!;

    /// <summary>
    /// "Mote farm at my level": at the fixture's level, where the log has no crawl in the band (read as "none"), then
    /// one level down with the stepper, where it has one crawl and a place needs two to rank (read as "too little").
    /// </summary>
    internal PageRun Farm { get; private set; } = null!;
    internal JsonObject NoFarm { get; private set; } = null!;
    internal JsonObject TooLittle { get; private set; } = null!;
    /// <summary>The same level with crawls played and none with a rate (sample-gates): too little, not none.</summary>
    internal JsonObject AllUnrated { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        AsLogged = await PageMessage.FromFixture();
        var countsSet = await PageMessage.WithCountsSet();
        var firstCountsSet = await PageMessage.Answer(PageMessage.LevelFromHere, countsSet: true);
        var first = await PageMessage.Answer(PageMessage.LevelFromHere);
        RouteAsLogged = await PageMessage.Answer(PageMessage.Level1To20);
        RouteCountsSet = await PageMessage.Answer(PageMessage.Level1To20, countsSet: true);
        NoFarm = await PageMessage.Answer(PageMessage.FarmAtMyLevel);
        TooLittle = await PageMessage.Answer(PageMessage.FarmOneLevelDown);
        AllUnrated = ((JsonObject)TooLittle.DeepClone()).With("farm.crawls", 0).With("farm.unrated", 3).With("noRateLeftOut", 3);

        var opened = await PageRuns.AtOnce(
            h => h.Run("ask",
                PageStep.Send(countsSet), PageStep.Send(firstCountsSet),
                PageStep.Click("#askBody [data-ask-range=\"1,20\"]"), PageStep.Send(RouteCountsSet), PageStep.Read("counts set"),
                PageStep.Send(AsLogged), PageStep.Send(RouteAsLogged), PageStep.Read("asks"),
                PageStep.Click("#askBody [data-set-counts]")),
            h => h.Run("ask",
                PageStep.Send(AsLogged), PageStep.Send(first),
                PageStep.Click("#askBody [data-ask-q=\"farm\"]"), PageStep.Send(NoFarm), PageStep.Read("none"),
                PageStep.Click("#askBody [data-ask-step=\"at\"][data-d=\"-1\"]"), PageStep.Send(TooLittle), PageStep.Read("too little"), PageStep.Send(AllUnrated), PageStep.Read("unrated")));
        (Level, Farm) = (opened.Runs[0], opened.Runs[1]);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// What Ask does with its answers (docs/quality.md): the real page in headless Edge, read as a player reads it.
/// These were regular expressions over ask.js, core.js and roster.js in AppPageTests.
/// </summary>
public class AskPageTests(AskPages pages) : IClassFixture<AskPages>
{
    /// <summary>The questions the page posted, read as the window reads them (MainWindow.SendAsk).</summary>
    private static List<AskQuery?> Asked(PageRun run) => run.PostedAs("ask").Select(p => p.Deserialize<AskQuery>(PageJson.Query)).ToList();

    /// <summary>"levels 21–23", as ask.js writes a band of levels (an en dash), or "50" at the cap.</summary>
    private static string Where(JsonObject answer) => answer.String("farm.band") is "50" ? "50" : "levels " + answer.String("farm.band").Replace("-", "–");

    /// <summary>
    /// Opening Ask asks the window its first question, and each thing the player picks asks again with that added: the
    /// answers these tests send are the engine's to exactly these questions, in this order.
    /// </summary>
    [Fact]
    public void AskSendsTheWindowEachQuestionThePlayerPicks()
    {
        Assert.Equal(new AskQuery?[] { PageMessage.LevelFromHere, PageMessage.FarmAtMyLevel, PageMessage.FarmOneLevelDown }, Asked(pages.Farm));
        Assert.Equal(new AskQuery?[] { PageMessage.LevelFromHere, PageMessage.Level1To20 }, Asked(pages.Level.At("counts set")));
        // New page data asks the open question again, as in the app (the answer may have changed with it).
        Assert.Equal(new AskQuery?[] { PageMessage.LevelFromHere, PageMessage.Level1To20, PageMessage.Level1To20 }, Asked(pages.Level.At("asks")));
    }

    /// <summary>
    /// A route's times lean on the trio's rank, and a log turned on after years of play has no reincarnation in it.
    /// While the log has seen none and no count is set, the answer says its times assume none (the XP to level is not
    /// raised), under the same banner as Home, Campaign and Roster, with Set counts.
    /// </summary>
    [Fact]
    public void AskSaysItsTimesAssumeNoReincarnationsAndAsksForCounts()
    {
        Assert.Equal(0, pages.RouteAsLogged.Number("rank"));
        var ask = pages.Level.At("asks").Text("askBody");
        Assert.Contains(string.Join(" → ", pages.RouteAsLogged.List("route.legs").Select(l => (string?)l!["place"])), ask);   // a route was drawn
        Assert.Contains(HomePageTests.NoReincarnationsSince(pages.AsLogged), ask);
        Assert.Contains($"Times assume {pages.RouteAsLogged.String("trio")} has never reincarnated: no extra XP to level. {HomePageTests.SetYourCounts}", ask);
        Assert.Contains("Set counts", ask);
        Assert.DoesNotContain("Times are for the XP", ask);
    }

    /// <summary>Once a count is set, the same route says whose XP its times are for, and there's no banner.</summary>
    [Fact]
    public void AskSaysWhatXpItsTimesAreForOnceACountIsSet()
    {
        var (rank, extra) = (pages.RouteCountsSet.Number("rank"), pages.RouteCountsSet.Number("xpExtra"));
        Assert.Equal(PageMessage.CountsSetTo, rank);
        var ask = pages.Level.At("counts set").Text("askBody");
        Assert.Contains(string.Join(" → ", pages.RouteCountsSet.List("route.legs").Select(l => (string?)l!["place"])), ask);
        Assert.Contains($"Times are for the XP {pages.RouteCountsSet.String("trio")} needs now: +{extra:0}% for {rank:0} reincarnations of its most-reincarnated class.", ask);
        Assert.DoesNotContain("Times assume", ask);
        Assert.DoesNotContain("Your log shows no reincarnations", ask);
        Assert.DoesNotContain("Set counts", ask);
    }

    /// <summary>Set counts in Ask's answer opens Roster with its count entry ready.</summary>
    [Fact]
    public void SetCountsOnAskOpensRostersCountEntry()
    {
        Assert.Equal("ask", pages.Level.At("asks").Page);
        PageRunReads.IsRosterWithItsCountsOpen(pages.Level, from: "ask");
    }

    /// <summary>With no crawl at the level since the patch, Ask says there's no farming there, and nothing about ranking.</summary>
    [Fact]
    public void NoFarmingAtALevelReadsAsNoFarmingOnAsk()
    {
        Assert.Equal(0, pages.NoFarm.Number("farm.crawls"));
        Assert.Empty(pages.NoFarm.List("farm.rows"));
        var ask = pages.Farm.At("none").Text("askBody");
        Assert.Contains($"No farming at {Where(pages.NoFarm)} since the patch in your logs\n", ask);
        Assert.DoesNotContain("Too little farming", ask);
        Assert.DoesNotContain("to rank a place", ask);
        Assert.DoesNotContain("a place ranks once", ask);
    }

    /// <summary>
    /// A place needs Ask.MinCrawls crawls to rank. With crawls at the level but none ranked, Ask says there's too little
    /// farming yet, not none, how many crawls the log has there and what a place needs. Home's half of this is
    /// HomePageTests'. This was a regular expression over ask.js.
    /// </summary>
    [Fact]
    public void AFarmTooSmallToRankReadsAsTooLittleOnAsk()
    {
        var crawls = (int)pages.TooLittle.Number("farm.crawls");
        Assert.InRange(crawls, 1, Ask.MinCrawls - 1);
        Assert.Empty(pages.TooLittle.List("farm.rows"));
        var ask = pages.Farm.At("too little").Text("askBody");
        Assert.Contains($"Too little farming at {Where(pages.TooLittle)} since the patch in your logs to rank a place\n", ask);
        Assert.Contains($"Your logs have {crawls} crawl{(crawls == 1 ? "" : "s")} with a rate there, but no place has {Ask.MinCrawls} of them and {Ask.MinHours * 60:0} minutes yet; a place ranks once it does.", ask);
        Assert.DoesNotContain("No farming", ask);
    }

    /// <summary>Crawls played but none with a rate is too little farming, never "No farming", and the Left out line names them (sample-gates).</summary>
    [Fact]
    public void FarmingWhoseCrawlsAllLackARateReadsAsTooLittleNotNone()
    {
        var ask = pages.Farm.At("unrated").Text("askBody");
        Assert.Contains($"Too little farming at {Where(pages.AllUnrated)} since the patch in your logs to rank a place", ask);
        Assert.Contains("Your logs have 3 crawls there, none with a rate yet: a rate starts at 8 kills, or when the crawl is finished.", ask);
        Assert.Contains("Left out: 3 crawls at", ask);
        Assert.DoesNotContain("No farming", ask);
    }
}
