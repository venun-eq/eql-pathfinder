using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Home sees relevels through the kind of relevel it is (docs/specs/goal-on-home.md, then judge-by-play: read from the play,
/// never asked): <c>reinc.homeKind</c> names the difficulty and whether it counts motes, and picks which relevels Home compares with. The player's real relevels, made up in their numbers: one mostly D0 at rank 2,
/// two mostly D4 at rank 1. Built as the engine builds them (a journey's route legs, its curve, <c>mostly</c>) and put
/// into the fixture's page data, which has no reincarnation to make relevels of.
/// </summary>
public sealed class HomeGoalPages : IAsyncLifetime
{
    internal static JsonObject Journey(string trio, int rank, double hours, double t5, string mostly, string end, bool done = true, double levelNow = 50)
    {
        var d = mostly == "mixed" ? 4 : int.Parse(mostly[1..]);
        var legs = new JsonArray();
        void Leg(int diff, double share) => legs.Add(new JsonObject
        {
            ["kind"] = "crawl",
            ["baseZone"] = "Befallen",
            ["difficulty"] = diff,
            ["levelFrom"] = 1,
            ["levelTo"] = 50,
            ["crawls"] = 3,
            ["activeMinutes"] = hours * 60 * share,
            ["levels"] = 49 * share,
            ["t5"] = t5 * share
        });
        if (mostly == "mixed") { Leg(1, 0.5); Leg(4, 0.5); } else Leg(d, 1);
        var curve = new JsonArray(Point(0, 1), Point(hours * 0.4, 30), Point(hours, levelNow));
        var j = new JsonObject
        {
            ["trio"] = trio,
            ["trioKey"] = trio,
            ["race"] = "Human",
            ["reincarnated"] = "2026-11-01T12:00:00",
            ["start"] = "2026-11-01T13:00:00",
            ["end"] = end,
            ["levelFrom"] = 1,
            ["levelNow"] = levelNow,
            ["done"] = done,
            ["activeHours"] = hours,
            ["wallHours"] = hours + 2,
            ["crawls"] = 3,
            ["kills"] = 500,
            ["deaths"] = 0,
            ["t5"] = t5,
            ["levels"] = 49,
            ["rank"] = rank,
            ["route"] = legs,
            ["etaHours"] = done ? null : (JsonNode)8,
            ["pace"] = null,
            ["etaNote"] = null,
            ["curve"] = curve,
            ["fieldHours"] = 0,
            ["fieldLevels"] = 0,
            ["instanceHours"] = 0,
            ["instanceLevels"] = 0,
            ["mostly"] = mostly,
        };
        if (!done) j["projection"] = new JsonArray(Point(hours, levelNow), Point(hours + 5, 50));
        return j;
    }

    private static JsonObject Point(double hours, double level) => new() { ["hours"] = hours, ["level"] = level };

    /// <summary>Ask's route as the engine gives it for a goal: the strip's legs, hours and T5-eq, and the word for its difficulty.</summary>
    internal static JsonObject Route(string goal, string mostly, int from = 1) => new()
    {
        ["from"] = from,
        ["to"] = 50,
        ["goal"] = goal,
        ["minutes"] = 500.0,
        ["t5"] = 452.0,
        ["gaps"] = new JsonArray(),
        ["mostly"] = mostly,
        ["legs"] = new JsonArray(
            new JsonObject { ["place"] = "Befallen D1", ["from"] = from, ["to"] = 10, ["minutes"] = 40.0, ["openWorld"] = false },
            new JsonObject { ["place"] = "Plane of Hate D4", ["from"] = 10, ["to"] = 50, ["minutes"] = 460.0, ["openWorld"] = false }),
    };

    /// <summary>The kind the engine reads from the relevel on now or the last one (<c>homeKind</c>): its difficulty, and whether it is a farmed one (counting motes).</summary>
    internal static JsonObject Kind(string mostly, bool farmed, string source = "last") => new()
    { ["source"] = source, ["trio"] = "RNG/DRU/BRD", ["trioKey"] = "RNG/DRU/BRD", ["mostly"] = mostly, ["farmed"] = farmed };

    internal static JsonObject Put(JsonObject message, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        var at = message;
        foreach (var p in parts[..^1]) at = at[p]!.AsObject();
        at[parts[^1]] = value;
        return message;
    }

    private static JsonArray Six() => new(
        Journey("SHD/ROG/BER", 2, 7.3, 39, "D0", "2026-11-03T12:00:00"),
        Journey("RNG/DRU/BRD", 1, 9.3, 461, "D4", "2026-11-04T12:00:00"),
        Journey("PAL/MNK/ENC", 1, 9.7, 629, "D4", "2026-11-05T12:00:00"));

    /// <summary>The fixture's player at the cap with three finished relevels, on a goal and the route that goal picks.</summary>
    internal static async Task<JsonObject> AtTheCap(string goal, string mostly)
    {
        var m = await PageMessage.FarmingAtTheCap();
        Put(m, "reinc.journeys", Six());
        Put(m, "reinc.home.route", Route(goal, mostly));
        Put(m, "reinc.homeKind", Kind(mostly, goal == "motes"));
        return WithRaces(m);
    }

    /// <summary>Leveling now at 30 on the player's trio, 3 h in, with the same three finished relevels.</summary>
    internal static async Task<JsonObject> Leveling(string goal, string mostly)
    {
        var m = await PageMessage.FromFixture();
        var journeys = Six();
        journeys.Add(Journey("SHD/SHM/WIZ", 1, 3.0, 80, "D4", "2026-11-18T12:00:00", done: false, levelNow: 30));
        Put(m, "reinc.journeys", journeys);
        Put(m, "reinc.homeKind", Kind(mostly, goal == "motes", "now"));
        Put(m, "reinc.home.route", Route(goal, mostly, from: 30));
        Put(m, "reinc.homeNow.mode", "relevel");
        Put(m, "reinc.homeNow.now", new JsonObject { ["trio"] = "SHD/SHM/WIZ", ["trioKey"] = "SHD/SHM/WIZ", ["race"] = "Human", ["reincarnated"] = "2026-11-01T12:00:00", ["number"] = 7, ["level"] = 30, ["journeyIndex"] = 3 });
        return WithRaces(m);
    }

    /// <summary>
    /// The races the engine works out from a message's journeys (<c>reinc.journeyRaces</c>), as TrackerService builds them: the fixture's
    /// have no relevels of their own, so after a test puts its own in, the engine's race is made from them and the journeys are written
    /// back as the engine writes them. Reads the open relevel (<c>homeNow</c>), the kind (<c>homeKind</c>), the campaign's next run and Ask's route.
    /// </summary>
    internal static JsonObject WithRaces(JsonObject m)
    {
        var reinc = m["reinc"]!.AsObject();
        var journeys = reinc["journeys"].Deserialize<List<Journey>>(PageJson.Options)!;
        var cap = reinc["levelCap"]!.GetValue<int>();
        var active = reinc["homeNow"]?["now"]?["journeyIndex"]?.GetValue<int>();
        var kind = reinc["homeKind"]?["mostly"]?.GetValue<string>();
        var trio = reinc["currentTrio"]?.GetValue<string>();
        var rank = (trio?.Split('/') ?? []).Select(k => reinc["classes"]!.AsArray().FirstOrDefault(c => c!["key"]!.GetValue<string>() == k)?["rank"]?.GetValue<int>() ?? 0).DefaultIfEmpty(0).Max();
        var next = reinc["campaign"]?["runs"]?.AsArray().FirstOrDefault(r => r!["state"]!.GetValue<string>() == "next")?["xp"]?.GetValue<double>();
        var route = reinc["home"]?["route"];
        var legs = route?["legs"]?.AsArray().Select(l => l!["minutes"]!.GetValue<double>()).ToList() ?? [];
        var races = JourneyRace.For(journeys, cap, active, kind, rank, next, legs.Count > 0 ? route!["minutes"]!.GetValue<double>() : null,
            legs.Count > 0 ? route!["t5"]!.GetValue<double>() : null, legs);
        reinc["journeys"] = JsonSerializer.SerializeToNode(journeys, PageJson.Options);
        reinc["journeyRaces"] = JsonSerializer.SerializeToNode(races, PageJson.Options);
        return m;
    }

    internal PageRun Motes { get; private set; } = null!;
    internal PageRun Farming { get; private set; } = null!;
    internal PageRun Level { get; private set; } = null!;
    internal PageRun LevelFast { get; private set; } = null!;
    internal PageRun Many { get; private set; } = null!;
    /// <summary>Leveling at 30 with the latest crawl finished (played at 22, with a place verdict), and then the same with a crawl going now.</summary>
    internal PageRun FinishedPlace { get; private set; } = null!;
    internal PageRun LivePlace { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var motes = await AtTheCap("motes", "D4");
        var fast = await AtTheCap("fast", "D0");
        var d2 = await AtTheCap("motes", "D2");
        var mixed = await AtTheCap("motes", "mixed");
        var noRoute = await AtTheCap("motes", "D4");
        Put(noRoute, "reinc.home.route", null);
        var noRelevel = await AtTheCap("motes", "D4");
        Put(noRelevel, "reinc.journeys", new JsonArray());
        Put(noRelevel, "reinc.homeKind", null);
        var lv = await Leveling("motes", "D4");
        var lvNone = await Leveling("fast", "D2");
        var many = await AtTheCap("motes", "D2");
        var finishedPlace = await Leveling("motes", "D4");
        foreach (var row in finishedPlace["history"]!.AsArray().OfType<JsonObject>()) { row["start"] = "2026-11-18T13:00:00"; row["difficulty"] = 4; row["kind"] = "leveling"; row["levelStart"] = 22; row["placeVerdict"] = null; row["betterPlace"] = null; }
        var firstRow = (JsonObject)finishedPlace["history"]!.AsArray()[0]!;
        firstRow["placeVerdict"] = "slower"; firstRow["betterPlace"] = "Befallen D1"; firstRow["baseZone"] = "Nagafen's Lair";
        finishedPlace["snapshot"] = new JsonObject { ["current"] = null, ["pace"] = null };
        var livePlace = (JsonObject)JsonNode.Parse(finishedPlace.ToJsonString())!;
        livePlace["snapshot"] = new JsonObject
        {
            ["current"] = new JsonObject { ["state"] = "crawling", ["baseZone"] = "Nagafen's Lair", ["difficulty"] = 4 },
            ["pace"] = new JsonObject { ["run"] = new JsonObject { ["xp"] = "normal" }, ["place"] = new JsonObject { ["kind"] = "slower", ["better"] = "Befallen D1", ["levelsPerHour"] = 1.0, ["betterLevelsPerHour"] = 1.6 }, ["kind"] = "leveling" },
        };
        var seven = Six();
        for (var i = 0; i < 4; i++) seven.Add(Journey($"TR{i}/ABC/DEF", 1, 10 + i, 300, "D4", $"2026-11-0{6 + i}T12:00:00"));
        Put(many, "reinc.journeys", seven);
        const string between = "[data-home-mode=between]";
        var opened = await PageRuns.AtOnce(
            h => h.Run("home",
                PageStep.Send(WithRaces(motes)), PageStep.Read("farming"), PageStep.Click(between), PageStep.Read("motes"),
                PageStep.Send(WithRaces(fast)), PageStep.Read("fast"),
                PageStep.Send(WithRaces(d2)), PageStep.Read("none"),
                PageStep.Send(WithRaces(mixed)), PageStep.Read("mixed"),
                PageStep.Send(WithRaces(noRoute)), PageStep.Read("no route"),
                PageStep.Send(WithRaces(noRelevel)), PageStep.Read("no relevel")),
            h => h.Run("home", PageStep.Send(WithRaces(motes)), PageStep.Read("farming")),
            h => h.Run("home", PageStep.Send(WithRaces(lv)), PageStep.Read("leveling")),
            h => h.Run("home", PageStep.Send(WithRaces(lvNone)), PageStep.Read("nothing to race")),
            h => h.Run("home", PageStep.Send(WithRaces(many)), PageStep.Click(between), PageStep.Read("many")),
            h => h.Run("home", PageStep.Send(WithRaces(finishedPlace)), PageStep.Read("finished place")),
            h => h.Run("home", PageStep.Send(WithRaces(livePlace)), PageStep.Read("live place")));
        (Motes, Farming, Level, LevelFast, Many) = (opened.Runs[0], opened.Runs[1], opened.Runs[2], opened.Runs[3], opened.Runs[4]);
        (FinishedPlace, LivePlace) = (opened.Runs[5], opened.Runs[6]);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class HomeGoalPageTests(HomeGoalPages pages) : IClassFixture<HomeGoalPages>
{
    /// <summary>Report #5: Home said "To beat: 7.3 h" from a relevel played at D0 on a motes route that goes to D4. Counting motes picks the D4 route, and the D4 best.</summary>
    [Fact]
    public void NextRelevelOnAMotesGoalComparesWithTheBestRelevelAtTheRoutesDifficulty()
    {
        var hero = pages.Motes.At("motes").Text("homeHero");
        Assert.DoesNotContain("Your goal", hero);
        Assert.Contains("Counting motes: your last relevel, RNG/DRU/BRD, was mostly D4, a difficulty you farm.", hero);
        Assert.Matches(@"≈ \d+\.\d h · \d+ T5-eq", hero);
        Assert.Contains("mostly D4", hero);
        Assert.Contains("To beat: 9.3 h · 461 T5-eq (RNG/DRU/BRD, mostly D4)", hero);
        Assert.DoesNotContain("7.3 h", hero);
    }

    /// <summary>The header names the kind, in hours as played; each row says where its hours went, and only the comparable best has the star.</summary>
    [Fact]
    public void YourRelevelsNamesTheKindItComparesAndMarksEachRowsDifficulty()
    {
        var card = pages.Motes.At("motes").Text("homeMain");
        Assert.Contains("Mostly D4, like Ask's route: best 9.3 h · typical 9.5 h", card);
        Assert.Contains("mostly D0", card);
        Assert.Contains("39 T5-eq", card);
        Assert.Contains("461 T5-eq", card);
        Assert.Contains("★ RNG/DRU/BRD", card);
        Assert.DoesNotContain("★ SHD/ROG/BER", card);
        Assert.Contains("Faded: mostly at another difficulty, so not compared.", card);
    }

    /// <summary>Fastest: the route is the fast one at its own difficulty, and To beat is from relevels of that difficulty only.</summary>
    [Fact]
    public void FastestComparesWithRelevelsAtTheFastRoutesDifficultyOnly()
    {
        var hero = pages.Motes.At("fast").Text("homeHero");
        Assert.Contains("Fastest: your last relevel, RNG/DRU/BRD, was mostly D0.", hero);
        Assert.DoesNotContain("Counting motes", hero);
        Assert.Contains("To beat: 7.3 h · 39 T5-eq (SHD/ROG/BER, mostly D0)", hero);
        Assert.Contains("Mostly D0, like Ask's route: best 7.3 h", pages.Motes.At("fast").Text("homeMain"));
    }

    /// <summary>With none of that kind yet, Home says so and never falls back to another kind; there is no star.</summary>
    [Fact]
    public void WithNoRelevelOfThatKindHomeSaysSoAndNamesNoBest()
    {
        var run = pages.Motes.At("none");
        Assert.Contains("Nothing to beat at D2 yet: none of your 3 relevels was mostly D2.", run.Text("homeHero"));
        Assert.DoesNotContain("To beat:", run.Text("homeHero"));
        Assert.Contains("None mostly D2 like Ask's route yet", run.Text("homeMain"));
        Assert.DoesNotContain("★", run.Text("homeMain"));
        var mixed = pages.Motes.At("mixed");
        Assert.Contains("Nothing to beat on a mixed relevel yet: none of your 3 relevels was mixed.", mixed.Text("homeHero"));
        Assert.Contains("None mixed like Ask's route yet", mixed.Text("homeMain"));
    }

    /// <summary>With nothing comparable and no star, the sub line can't promise "your best"; and one comparable relevel has no "typical" apart from the best.</summary>
    [Fact]
    public void TheSubLineAndTheHeaderOnlyPromiseWhatIsThere()
    {
        var none = pages.Many.At("many").Text("homeMain");
        Assert.Contains("Your latest five.", none);
        Assert.DoesNotContain("Your best and the latest five", none);
        var fast = pages.Motes.At("fast").Text("homeMain");
        Assert.Contains("best 7.3 h", fast);
        Assert.DoesNotContain("typical", fast);
    }

    /// <summary>No route (no legs): the kind line stays, Ask has nothing to say, and there is no To beat line.</summary>
    [Fact]
    public void WithNoRouteTheKindStaysAndThereIsNothingToBeat()
    {
        var hero = pages.Motes.At("no route").Text("homeHero");
        Assert.Contains("Counting motes", hero);
        Assert.Contains("Ask has no route to 50 yet.", hero);
        Assert.DoesNotContain("To beat:", hero);
        Assert.DoesNotContain("Nothing to beat", hero);
    }

    /// <summary>No relevel yet: nothing to go by, and Home says so rather than guess a goal.</summary>
    [Fact]
    public void WithNoRelevelYetHomeSaysThereIsNothingToGoBy()
    {
        var hero = pages.Motes.At("no relevel").Text("homeHero");
        Assert.Contains("Ask's route is the fastest: no relevel of yours to go by yet.", hero);
        Assert.DoesNotContain("Counting motes", hero);
        Assert.DoesNotContain("To beat:", hero);
    }

    /// <summary>The goal switch is gone: Home asks for nothing, so no click posts a goal, and "Ask it another way" opens Ask.</summary>
    [Fact]
    public void HomeHasNoGoalSwitchAndLinksToAsk()
    {
        var run = pages.Motes.At("motes");
        Assert.Contains("Ask it another way →", run.Text("homeHero"));
        Assert.DoesNotContain(pages.Motes.Posted, p => (string?)p["type"] == "setting" && (string?)p["key"] == "goal");
    }

    /// <summary>Farming's "When you're done" names the kind its best is of.</summary>
    [Fact]
    public void FarmingsWhenYoureDoneSaysBestAtTheRoutesDifficulty()
    {
        Assert.Contains("Relevels: best at D4 9.3 h, 3 done", pages.Farming.At("farming").Text("homeSide"));
    }

    /// <summary>
    /// A finished crawl's place words name the level it was judged at (22), not today's (30), and say they come from the crawls
    /// before it; the crawl going now is judged now, with neither (docs/specs/verdicts-as-of-visit.md).
    /// </summary>
    [Fact]
    public void ThePlaceTileOfAFinishedCrawlNamesItsOwnLevelAndTheCrawlsBeforeIt()
    {
        var done = pages.FinishedPlace.At("finished place").Text("homeHero");
        Assert.Contains("Befallen D1 was clearly faster at the same difficulty at 22, by your crawls before it.", done);
        Assert.DoesNotContain("at 30", done);
        var live = pages.LivePlace.At("live place").Text("homeHero");
        Assert.Contains("Befallen D1 is clearly faster at the same difficulty at 30: 1.6 levels/h against 1.0 here.", live);
        Assert.DoesNotContain("by your crawls before it", live);
    }

    /// <summary>Leveling now: the pace line, the sentence, Splits and the race all name the difficulty and the D4 best.</summary>
    [Fact]
    public void LevelingNowRacesTheBestRelevelAtTheRoutesDifficulty()
    {
        var run = pages.Level.At("leveling");
        var hero = run.Text("homeHero");
        Assert.Contains("ahead of your best at D4", hero);
        Assert.Contains("Your best at D4, RNG/DRU/BRD, reached 30 after 3.7 h", hero);
        Assert.Contains("Counting motes: this relevel is mostly D4, a difficulty you farm.", hero);
        Assert.Contains("Splits against your best at D4", run.Text("homeSide"));
        Assert.Contains("RNG/DRU/BRD (9.3\u00a0h as played · 461\u00a0T5-eq)", run.Text("homeSide"));
        var race = run.Text("homeMain");
        Assert.Contains("The race at D4", race);
        Assert.Contains("Your best at D4: RNG/DRU/BRD 9.3 h", race);
        Assert.Contains("Middle half of your 2 relevels at D4", race);
    }

    /// <summary>On a route with no relevel of its kind, the delta goes and the sentence says what is missing; Splits has nothing to split against.</summary>
    [Fact]
    public void LevelingNowWithNothingToRaceSaysSoAndDrawsNoSplits()
    {
        var run = pages.LevelFast.At("nothing to race");
        Assert.Contains("Nothing to race at D2 yet: none of your 3 relevels was mostly D2.", run.Text("homeHero"));
        Assert.DoesNotContain("of your best", run.Text("homeHero"));
        Assert.DoesNotContain("Splits against", run.Text("homeSide"));
        Assert.Contains("No relevel of yours mostly at D2 to race yet", run.Text("homeMain"));
        Assert.Contains("This relevel, place by place", run.Text("homeSide"));
    }
}
