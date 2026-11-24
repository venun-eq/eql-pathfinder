using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Home compares a relevel only with relevels played mostly at the same difficulty as Ask's route
/// (docs/specs/goal-on-home.md). The engine names that difficulty, so the page only compares words.
/// </summary>
public class LikeForLikeTests
{
    private static readonly DateTime T = new(2026, 11, 11, 12, 0, 0);

    private static JourneyLeg Crawl(int difficulty, double minutes) =>
        new("Befallen", difficulty, 1, 50, 3, minutes, 10, 20);

    private static JourneyLeg OpenWorld(double minutes) =>
        new("Befallen", null, 1, 50, 1, minutes, 1, 1, "open world");

    private static Journey Relevel(double activeMinutes, params JourneyLeg[] legs) =>
        new("RNG/DRU/BRD", "BRD/DRU/RNG", null, T, T, T.AddHours(10), 1, 50, true, activeMinutes / 60, activeMinutes / 60, 5, 100, 0, 100, 49, 1,
            legs, null, null, null, []);

    private static AskLeg Leg(string place, double minutes, bool open = false) => new(place, 1, 10, minutes, 1, 1, 10, null, false, open, null, 0);

    /// <summary>SHD/ROG/BER on the player's log: D0 71%, D1 19%, D2 8%, open world 2%. Its hours went mostly to D0.</summary>
    [Fact]
    public void ARelevelWithMoreThanHalfItsHoursAtOneDifficultyIsMostlyThatOne()
    {
        var j = Relevel(438, Crawl(0, 311), Crawl(1, 83), Crawl(2, 35), OpenWorld(9));
        Assert.Equal("D0", j.Mostly);
        Assert.Equal("D4", Relevel(600, Crawl(4, 396), Crawl(2, 120), Crawl(1, 60), Crawl(3, 24)).Mostly);   // SHD/SHM/WIZ: D4 66%
    }

    /// <summary>Open world and other instances count in the whole but are no difficulty: 48% at D4 is not "mostly D4".</summary>
    [Fact]
    public void OpenWorldCountsInTheWholeButIsNoDifficulty()
    {
        Assert.Equal("mixed", Relevel(600, Crawl(4, 288), OpenWorld(312)).Mostly);
    }

    /// <summary>"More than half": exactly half is mixed, and a relevel with no crawl at all has no difficulty to be mostly at.</summary>
    [Fact]
    public void ExactlyHalfIsMixedAndSoIsARelevelWithNoCrawls()
    {
        Assert.Equal("mixed", Relevel(600, Crawl(4, 300), Crawl(2, 300)).Mostly);
        Assert.Equal("mixed", Relevel(600, Crawl(4, 200), Crawl(2, 200), Crawl(1, 200)).Mostly);
        Assert.Equal("mixed", Relevel(60, OpenWorld(60)).Mostly);
        Assert.Equal("mixed", Relevel(0).Mostly);
    }

    /// <summary>Two stretches at one difficulty in different places add up: the difficulty is what counts, not the place.</summary>
    [Fact]
    public void DifferentPlacesAtOneDifficultyAddUp()
    {
        var other = new JourneyLeg("Kedge Keep", 4, 20, 30, 2, 200, 5, 9);
        Assert.Equal("D4", Relevel(600, Crawl(4, 200), other, Crawl(2, 200)).Mostly);
    }

    /// <summary>Report #5's motes route: Befallen D1 for a few levels, then D4 to 50. By each leg's minutes it's mostly D4.</summary>
    [Fact]
    public void AnAskRouteIsMostlyTheDifficultyMostOfItsMinutesAreAt()
    {
        Assert.Equal("D4", DifficultyMix.OfRoute([Leg("Befallen D1", 40), Leg("Plane of Hate D4", 460)]));
        Assert.Equal("D0", DifficultyMix.OfRoute([Leg("Befallen D0", 100)]));
    }

    /// <summary>An open-world leg carries no difficulty, even when its place name does; no legs is no route, which is not "mixed".</summary>
    [Fact]
    public void AnAskRouteCountsOpenWorldLegsInTheWholeAndNoRouteHasNoWord()
    {
        Assert.Equal("mixed", DifficultyMix.OfRoute([Leg("Plane of Hate D4", 200), Leg("Befallen D4 · open world", 300, open: true)]));
        Assert.Equal("mixed", DifficultyMix.OfRoute([Leg("Befallen D1", 250), Leg("Plane of Hate D4", 250)]));
        Assert.Null(DifficultyMix.OfRoute([]));
    }

    /// <summary>The page reads the word off the data message: a journey and Ask's route both carry <c>mostly</c>.</summary>
    [Fact]
    public void TheWordReachesThePageOnAJourneyAndOnAskRoute()
    {
        var j = JsonSerializer.SerializeToNode(Relevel(438, Crawl(0, 311), Crawl(1, 83), Crawl(2, 35), OpenWorld(9)), PageJson.Options)!.AsObject();
        Assert.Equal("D0", (string?)j["mostly"]);
    }

    /// <summary>The messages the engine builds carry the route's word: Home's route in the data message (null here: the fixture, at 23, has played no crawl above its level) and Ask's route when it has legs.</summary>
    [Fact]
    public async Task TheBuiltMessagesCarryTheRoutesWord()
    {
        var home = (await PageMessage.FromFixture())["reinc"]!["home"]!["route"]!.AsObject();
        Assert.True(home.ContainsKey("mostly"));
        var word = (string?)(await PageMessage.Answer(PageMessage.Level1To20))["route"]!["mostly"];
        Assert.True(word is "mixed" || (word is { Length: 2 } && word[0] == 'D'), $"route.mostly was '{word}'");
    }
}
