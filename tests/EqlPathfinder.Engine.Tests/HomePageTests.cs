using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Home in headless Edge for every test here, three starts of the browser at once: the fixture's player at the cap,
/// once minutes into a farming session and once with a whole crawl behind them, and the same player as the log has
/// them, at 23, through a run that ends with a click on Set counts.
/// </summary>
public sealed class HomePages : IAsyncLifetime
{
    /// <summary>At the cap, two minutes into the session's first crawl, and nothing at the cap that counts since the patch.</summary>
    internal PageRun JustStarted { get; private set; } = null!;
    internal JsonObject JustStartedMessage { get; private set; } = null!;

    /// <summary>At the cap after one whole crawl there: a session with a rate, and one crawl where a place needs two to rank.</summary>
    internal PageRun OneCrawl { get; private set; } = null!;
    internal JsonObject OneCrawlMessage { get; private set; } = null!;

    /// <summary>
    /// The page data as the engine built it from the log, which has seen no reincarnation (read as "asks"); then the
    /// same player with a count set (read as "counts set"); then the first again, and a click on the banner's Set
    /// counts. No value is set to choose what Home shows: these tests read the banner where Home puts it for this
    /// player, and say nothing about which of its states that is (docs/debt.md, home.js).
    /// </summary>
    internal PageRun Counts { get; private set; } = null!;
    internal JsonObject AsLogged { get; private set; } = null!;
    internal JsonObject CountsSet { get; private set; } = null!;

    /// <summary>The same player as the engine says Home shows: below the cap with the loadout not known (a swap), then with no level line, then at the cap.</summary>
    internal PageRun Engine { get; private set; } = null!;

    /// <summary>The fixture's player whose every visit has no rate yet (the engine's nulls), and whose stop has none either.</summary>
    internal PageRun NoRate { get; private set; } = null!;
    internal JsonObject NoRateMessage { get; private set; } = null!;

    /// <summary>What the runs left in the folder they were given, and the page's own files before and after them.</summary>
    internal IReadOnlyList<string> LeftBehind { get; private set; } = [];
    internal IReadOnlyList<string> WebBefore { get; private set; } = [];
    internal IReadOnlyList<string> WebAfter { get; private set; } = [];

    public async Task InitializeAsync()
    {
        JustStartedMessage = (await PageMessage.FarmingAtTheCap())
            .With("snapshot.session.early", true).With("snapshot.session.crawlMinutes", 2).With("snapshot.session.wallMinutes", 2);
        // The fixture's session is one crawl with nothing after it, so its two rates are the same number, and a page that
        // drew either in the other's place would read the same. Half an hour in all, with the crawl in it, tells them apart.
        OneCrawlMessage = (await PageMessage.FarmingAtTheCap()).With("reinc.home.farm.crawls", 1);
        OneCrawlMessage.With("snapshot.session.wallMinutes", 30).With("snapshot.session.t5PerHourWall", OneCrawlMessage.Number("snapshot.session.t5") / 30 * 60);
        NoRateMessage = await HomeGoalPages.Leveling("motes", "D4");   // a relevel on now (Home draws its place card), played at the fixture's visits
        foreach (var row in NoRateMessage["history"]!.AsArray().OfType<JsonObject>())
        {
            row["start"] = "2026-11-18T13:00:00"; row["difficulty"] = 4;
            foreach (var k in new[] { "levelsPerHour", "t5PerHour", "killsPerMinute", "dps" }) row[k] = null;
            row["kind"] = "farming"; row["farmVerdict"] = "early"; row["farmUsual"] = 40.0; row["farmBasis"] = "crawls you finished, solo";
            row["stopFarm"] = new JsonObject { ["verdict"] = "early", ["t5PerHour"] = null, ["pct"] = null, ["usual"] = 40.0, ["runs"] = 3, ["basis"] = "crawls you finished, solo" };
        }
        AsLogged = await PageMessage.FromFixture();
        CountsSet = await PageMessage.WithCountsSet();

        var swapped = (await PageMessage.FromFixture()).With("reinc.homeNow.mode", "swapped");
        var noLevel = (await PageMessage.FromFixture()).With("reinc.homeNow.mode", "nolevel");
        var opened = await PageRuns.AtOnce(
            h => h.Open("home", JustStartedMessage.ToJsonString()),
            h => h.Open("home", OneCrawlMessage.ToJsonString()),
            h => h.Open("home", NoRateMessage.ToJsonString()),
            h => h.Run("home",
                PageStep.Send(AsLogged), PageStep.Read("asks"),
                PageStep.Send(CountsSet), PageStep.Read("counts set"),
                PageStep.Send(AsLogged), PageStep.Click("#page-home .banner [data-set-counts]")),
            h => h.Run("home",
                PageStep.Send(swapped), PageStep.Read("swapped"),
                PageStep.Send(noLevel), PageStep.Read("no level"),
                PageStep.Send(JustStartedMessage), PageStep.Read("at the cap")));
        (JustStarted, OneCrawl, NoRate, Counts, Engine) = (opened.Runs[0], opened.Runs[1], opened.Runs[2], opened.Runs[3], opened.Runs[4]);
        (LeftBehind, WebBefore, WebAfter) = (opened.LeftBehind, opened.WebBefore, opened.WebAfter);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// What Home does with its page data (docs/quality.md): the real page in headless Edge, read as a player reads it.
/// These two facts were regular expressions over home.js until the harness could draw the page.
/// </summary>
public class HomePageTests(HomePages pages) : IClassFixture<HomePages>
{
    private static string Shown(JsonObject message, string path, string format) => message.Number(path).ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// One rule for a rate (docs/specs/one-rate.md): the last visit with no rate reads "Too short for a rate yet" with the
    /// words (the number of kills from the data), and a stop with no rate says so and shows no T5-eq/h or levels/h of its own.
    /// </summary>
    [Fact]
    public void HomeSaysTooShortForARateWhereTheEngineSendsNone()
    {
        var n = pages.NoRateMessage.Number("snapshot.session.rateFromKills").ToString("0", CultureInfo.InvariantCulture);
        var all = pages.NoRate.Text("page-home");
        Assert.Contains($"Too short for a rate yet: the rate starts at {n} kills, or when the crawl is finished.", all);
        var side = pages.NoRate.Text("homeSide");
        Assert.Contains("Too short for a rate yet", side);
        Assert.DoesNotMatch(@"d T5-eq/h", side);
        Assert.DoesNotMatch(@"d levels/h", side);
    }

    /// <summary>The engine picks Home's state (reinc.homeNow) and the page draws it: the releveling state is never the guess (feedback #7, 0.10.7).</summary>
    [Fact]
    public void Home_draws_the_state_the_engine_sends_and_guesses_none()
    {
        var swapped = pages.Engine.At("swapped").Text("homeHero");
        Assert.Contains("Swapped loadouts", swapped, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Type /who in game", swapped);
        Assert.Contains("Your level changed to", swapped);
        Assert.Contains("loadout not known yet", swapped);
        Assert.DoesNotContain("Leveling now", swapped);
        var none = pages.Engine.At("no level").Text("homeHero");
        Assert.Contains("Your character", none, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pathfinder hasn't seen your level in this log yet", none);
        var atCap = pages.Engine.At("at the cap").Text("homeHero");
        Assert.Contains("Auto: you're at", atCap);
        Assert.DoesNotContain("a crawl at", atCap);
    }

    /// <summary>
    /// Home's farming card read a two-minute crawl as 124.6 T5-eq an hour (a report in 0.10.3). Until the session has
    /// a finished crawl or Rates.MinKills kills (the message's <c>early</c>), it shows the T5-eq so far and says when the
    /// rate comes, and the rate itself is nowhere on Home. PageHarnessTests runs this on a copy that forgets to wait.
    /// </summary>
    internal static void WaitsForTheSessionRate(PageRun home, JsonObject message)
    {
        var hero = home.Text("homeHero");
        Assert.Contains("T5-eq so far this session", hero);
        Assert.Contains(Shown(message, "snapshot.session.t5", "0") + "\n", hero);
        Assert.Contains($"2 min in crawls. The rate starts at {Shown(message, "snapshot.session.rateFromKills", "0")} kills, or when a crawl is finished.", hero);
        Assert.Contains("1 crawl, too soon to say", hero);
        Assert.DoesNotContain("T5-eq an hour this session", hero);
        Assert.DoesNotContain("counting time between crawls", hero);
        var rate = Shown(message, "snapshot.session.t5PerHourCrawl", "0.0");
        Assert.NotEqual("0.0", rate);
        var all = home.Text("page-home");
        Assert.Contains("T5-eq so far this session", all);
        Assert.DoesNotContain(rate, all);
    }

    [Fact]
    public void HomeShowsT5SoFarUntilTheSessionHasAFairRate() => WaitsForTheSessionRate(pages.JustStarted, pages.JustStartedMessage);

    /// <summary>
    /// Once the session has a fair rate, the big number is the rate in crawls, the wall-clock rate is the small line
    /// under it and nowhere else, and "This session" in the comparison is the rate in crawls again. The message has two
    /// different rates, so each is read where it belongs: drawn in each other's place, the page fails here.
    /// </summary>
    internal static void ShowsTheSessionRate(PageRun home, JsonObject message)
    {
        var hero = home.Text("homeHero");
        var rate = Shown(message, "snapshot.session.t5PerHourCrawl", "0.0");
        var wall = Shown(message, "snapshot.session.t5PerHourWall", "0.0");
        Assert.NotEqual(rate, wall);
        Assert.Contains($"{rate}\nT5-eq an hour this session, in crawls", hero);
        Assert.Contains($"{wall} counting time between crawls", hero);
        Assert.Contains($"This session\n{rate} 1 crawl", hero);
        Assert.Single(Regex.Matches(hero, Regex.Escape(wall)));
        Assert.DoesNotContain("so far this session", hero);
        Assert.DoesNotContain("The rate starts", hero);
        Assert.DoesNotContain("too soon to say", hero);
    }

    [Fact]
    public void HomeShowsTheRateOnceTheSessionHasOne() => ShowsTheSessionRate(pages.OneCrawl, pages.OneCrawlMessage);

    /// <summary>
    /// Home said "You haven't farmed at 50 since the patch" beside "Your usual at 50 since the patch: 8.0, 1 crawl": a
    /// place needs Ask.MinCrawls crawls to rank. With crawls but none ranked, Home says there's too little yet, and
    /// what a place needs. (Ask's half of this is AskPageTests'.)
    /// </summary>
    [Fact]
    public void AFarmTooSmallToRankReadsAsTooLittleOnHome()
    {
        var side = pages.OneCrawl.Text("homeSide");
        Assert.Contains("Too little at 50 since the 11-10 patch to rank a place yet.", side);
        Assert.Contains($"A place ranks at {Ask.MinCrawls} crawls and {Ask.MinHours * 60:0} min.", side);
        Assert.DoesNotContain("You haven't farmed", side);
    }

    [Fact]
    public void NoFarmingAtTheCapReadsAsNotFarmedOnHome()
    {
        Assert.Equal(0, pages.JustStartedMessage.Number("reinc.home.farm.crawls"));
        var side = pages.JustStarted.Text("homeSide");
        Assert.Contains("You haven't farmed at 50 since the 11-10 patch.", side);
        Assert.DoesNotContain("Too little", side);
    }

    /// <summary>What the page posts to the window comes back to the test: opening a page is counted once (usage), as in the app.</summary>
    [Fact]
    public void OpeningHomeTellsTheWindowWhichPageWasSeen()
    {
        foreach (var run in new[] { pages.JustStarted, pages.OneCrawl })
        {
            Assert.Equal("home", run.Page);
            Assert.Single(run.Posted, p => (string?)p["type"] == "seen" && (string?)p["page"] == "home");
        }
    }

    /// <summary>
    /// Farming hides Up next and the roster until you're done: a card that isn't drawn reads as nothing, so a test can
    /// say a player doesn't see it. An id the page doesn't have is a slip in the test, and says so.
    /// </summary>
    [Fact]
    public void AHiddenCardReadsAsNothingAndAnIdThePageLacksFailsByName()
    {
        Assert.Contains("FARMING AT 50", pages.JustStarted.Text("homeHero"));   // the label is drawn in capitals, and read as drawn
        Assert.Equal("", pages.JustStarted.Text("homeUpNextCard"));
        var e = Assert.Throws<PageFailed>(() => pages.JustStarted.Text("homeHeroo"));
        Assert.Contains("homeHeroo", e.Message);
    }

    /// <summary>The banner's first line, as core.js draws it from the log's first day: "Your log shows no reincarnations since Nov 14".</summary>
    internal static string NoReincarnationsSince(JsonObject message) =>
        "Your log shows no reincarnations since " + DateTime.Parse(message.String("reinc.logStart"), CultureInfo.InvariantCulture).ToString("MMM d", CultureInfo.InvariantCulture);

    internal const string SetYourCounts = "Reincarnated while logging was off? Set your counts so the plan and relevel times are right.";

    /// <summary>
    /// A player who turned on /log after years of play has no reincarnation in it, so every rank reads as none: Home
    /// planned "Run 1 of 48" at +10% for a player six runs in (11-17). While the log has seen none and no count is
    /// set, Home says its ranks are a guess and offers Set counts. This was a regular expression over home.js.
    /// </summary>
    [Fact]
    public void HomeAsksForCountsWhileTheLogHasSeenNoReincarnation()
    {
        var home = pages.Counts.At("asks").Text("page-home");
        Assert.Contains(NoReincarnationsSince(pages.AsLogged), home);
        Assert.Contains(SetYourCounts, home);
        Assert.Contains("Set counts", home);
        Assert.Contains("16 classes left for Completionist if you haven't reincarnated before", home);
        Assert.Contains("none in your log yet", home);
    }

    /// <summary>Once a count is set the ranks are the player's own word: no banner, and the roster counts what they set.</summary>
    [Fact]
    public void HomeStopsAskingOnceACountIsSet()
    {
        Assert.Equal(PageMessage.CountsSetTo, pages.CountsSet.List("reinc.classes").Single(c => (string?)c!["key"] == PageMessage.CountsSetFor)!["entered"]!.GetValue<int>());
        var home = pages.Counts.At("counts set").Text("page-home");
        Assert.Contains("1 / 16 reincarnated", home);
        Assert.Contains("15 classes left for Completionist", home);
        Assert.DoesNotContain("if you haven't reincarnated before", home);
        Assert.DoesNotContain("Your log shows no reincarnations", home);
        Assert.DoesNotContain("Set counts", home);
        Assert.DoesNotContain("Set your counts", home);
        Assert.DoesNotContain("none in your log yet", home);
    }

    /// <summary>Set counts on Home's banner opens Roster with its count entry ready. This was a regular expression over roster.js's listener.</summary>
    [Fact]
    public void SetCountsOnHomeOpensRostersCountEntry()
    {
        Assert.Equal("home", pages.Counts.At("asks").Page);
        PageRunReads.IsRosterWithItsCountsOpen(pages.Counts, from: "home");
    }

    /// <summary>
    /// The gate runs these in the checkout it will merge and in a clean copy: a run keeps its copy of the page and the
    /// browser's profile in the folder it's given, and both are gone when it returns. A profile folder that can be
    /// removed is also the proof that no browser is left: Edge holds files in it for as long as any part of it runs.
    /// </summary>
    [Fact]
    public void APageTestLeavesNothingBehindAndWritesNothingBesideThePage()
    {
        Assert.NotEqual("", pages.JustStarted.Text("homeHero"));   // the page really was drawn
        Assert.NotEqual("", pages.OneCrawl.Text("homeHero"));
        Assert.NotEqual("", pages.Counts.Text("page-" + pages.Counts.Page));
        Assert.Empty(pages.LeftBehind);
        Assert.NotEmpty(pages.WebBefore);
        Assert.Equal(pages.WebBefore, pages.WebAfter);
    }
}
