using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Roster in headless Edge for every test here, in one run: the fixture's player as the log has them, with a count
/// set, and with one reincarnation in the log, then a click on the banner's Set counts.
/// </summary>
public sealed class RosterPages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;
    internal JsonObject AsLogged { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        AsLogged = await PageMessage.FromFixture();
        var countsSet = await PageMessage.WithCountsSet();
        // The fixture's log has no reincarnation, and no checked-in log has one yet: the race the character plays is
        // given one the log saw, at the two values the engine writes for it.
        var logSawOne = await PageMessage.FromFixture();
        var race = logSawOne.List("reinc.races").Select((r, i) => (Key: (string?)r!["key"], i)).Single(r => r.Key == logSawOne.String("snapshot.race")).i;
        logSawOne.With($"reinc.races.{race}.logged", 1).With($"reinc.races.{race}.rank", 1);

        var opened = await PageRuns.AtOnce(h => h.Run("roster",
            PageStep.Send(AsLogged), PageStep.Read("asks"),
            PageStep.Send(countsSet), PageStep.Read("counts set"),
            PageStep.Send(logSawOne), PageStep.Read("log saw one"),
            PageStep.Send(AsLogged), PageStep.Click("#countsBanner [data-set-counts]")));
        Run = opened.Runs[0];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// What Roster does with its page data (docs/quality.md): the real page in headless Edge, read as a player reads it.
/// These were regular expressions over core.js and roster.js in AppPageTests.
/// </summary>
public class RosterPageTests(RosterPages pages) : IClassFixture<RosterPages>
{
    /// <summary>
    /// A player who turned on /log after years of play has no reincarnation in it, so every rank reads as none. While
    /// the log has seen none and no count is set, Roster says so above its tiles and offers Set counts.
    /// </summary>
    [Fact]
    public void RosterAsksForCountsWhileTheLogHasSeenNoReincarnation()
    {
        var roster = pages.Run.At("asks");
        var banner = roster.Text("countsBanner");
        Assert.Contains(HomePageTests.NoReincarnationsSince(pages.AsLogged), banner);
        Assert.Contains(HomePageTests.SetYourCounts, banner);
        Assert.Contains("Set counts", banner);
        Assert.Contains("0 of 16 reincarnated", roster.Text("classCount"));
        Assert.Contains("0 of 16 reincarnated", roster.Text("raceCount"));
    }

    /// <summary>
    /// The ranks stop being a guess either way: the player set a count, or the log saw a reincarnation. Each is read
    /// from a message that says so in what Roster counts, so the banner is gone from a page that drew that message.
    /// </summary>
    [Fact]
    public void RosterStopsAskingOnceACountIsSetOrTheLogSeesAReincarnation()
    {
        var set = pages.Run.At("counts set");
        Assert.Contains("1 of 16 reincarnated", set.Text("classCount"));
        Assert.Contains("1 of 16 reincarnated", set.Text("page-roster"));
        Assert.DoesNotContain("Your log shows no reincarnations", set.Text("page-roster"));
        Assert.DoesNotContain(HomePageTests.SetYourCounts, set.Text("page-roster"));

        var logged = pages.Run.At("log saw one");
        Assert.Contains("0 of 16 reincarnated", logged.Text("classCount"));
        Assert.Contains("1 of 16 reincarnated", logged.Text("raceCount"));
        Assert.Contains("1 of 16 reincarnated", logged.Text("page-roster"));
        Assert.DoesNotContain("Your log shows no reincarnations", logged.Text("page-roster"));
        Assert.DoesNotContain(HomePageTests.SetYourCounts, logged.Text("page-roster"));
    }

    /// <summary>Set counts on Roster's own banner opens its count entry: the banner becomes the entry's, and the "Set counts…" link beside the tiles steps aside.</summary>
    [Fact]
    public void SetCountsOnRosterOpensItsCountEntry()
    {
        Assert.Contains("Set counts", pages.Run.At("asks").Text("editCounts"));
        PageRunReads.IsRosterWithItsCountsOpen(pages.Run, from: "roster");
        Assert.Equal("", pages.Run.Text("editCounts"));
        Assert.Single(pages.Run.PostedAs("seen"));
    }

    /// <summary>A reading no step of the run took is a slip in the test, and says which name: it never reads as the end of the run.</summary>
    [Fact]
    public void AReadingTheRunNeverTookFailsByName()
    {
        var e = Assert.Throws<PageFailed>(() => pages.Run.At("counts sett"));
        Assert.StartsWith("The test:", e.Message);
        Assert.Contains("counts sett", e.Message);
    }
}
