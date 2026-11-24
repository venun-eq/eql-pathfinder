using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;
using EqlPathfinder.Engine.Upgrades;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The messages a page test sends: the page data the engine builds from the fixture and the answers it gives, written
/// as the window writes them (docs/specs/page-data-in-engine.md), with the few values a test is about changed.
///
/// They're built on each run and never checked in: they hold the character's name and clock times (docs/quality.md,
/// rule 6). A test changes values only where the built message already has them, and only to the same kind of value,
/// so a field the engine renames or drops fails here and doesn't leave a page test drawing from a message no player
/// gets. What the engine puts in those fields is the engine tests' to prove; these tests are about what the page does
/// with them.
/// </summary>
internal static class PageMessage
{
    /// <summary>The class the player has set a count for in <see cref="WithCountsSet"/>, the count, and when they set it (after the log's last line).</summary>
    public const string CountsSetFor = "MAG";
    public const int CountsSetTo = 2;
    private static readonly DateTime CountsSetOn = new(2026, 11, 14, 15, 0, 0);

    /// <summary>Ask's first question on a page nobody has used: where to level from your level.</summary>
    public static readonly AskQuery LevelFromHere = new("level", null, null, null, null, null, null);
    /// <summary>The "1–20" button under it: levels the fixture has played, so the answer is a route.</summary>
    public static readonly AskQuery Level1To20 = new("level", 1, 20, null, null, null, null);
    /// <summary>The "Mote farm at my level" tab: at 23 the fixture has no crawl in the band.</summary>
    public static readonly AskQuery FarmAtMyLevel = new("farm", null, null, null, null, null, null);
    /// <summary>One level down with the stepper: one crawl in the band, where a place needs two to rank.</summary>
    public static readonly AskQuery FarmOneLevelDown = new("farm", null, null, null, 22, null, null);

    private static readonly AskQuery[] Asked = [LevelFromHere, Level1To20, FarmAtMyLevel, FarmOneLevelDown];

    private sealed record Built(string Data, string DataWithoutAPlan, IReadOnlyDictionary<AskQuery, string> Answers);

    private static readonly Lazy<Task<Built>> AsLogged = new(() => Build(countsSet: false)), CountsSet = new(() => Build(countsSet: true));

    /// <summary>
    /// The fixture's page data: Standin at level 23 after five crawls, with the overlay's snapshot and a whole campaign
    /// (or, with <paramref name="plan"/> false, the campaign of a player who hasn't made one). The log has seen no
    /// reincarnation and no count is set, so every rank is a guess.
    /// </summary>
    public static async Task<JsonObject> FromFixture(bool plan = true) => Parse(await AsLogged.Value, plan);

    /// <summary>
    /// The same player after setting a count on Roster (<see cref="CountsSetFor"/> at <see cref="CountsSetTo"/>): the
    /// engine built this message from the same log with that count entered, as the app does after Roster posts it.
    /// </summary>
    public static async Task<JsonObject> WithCountsSet(bool plan = true) => Parse(await CountsSet.Value, plan);

    /// <summary>
    /// Ask's answer to one of the questions above, as <c>TrackerService.AskPage</c> gives it for the fixture's player
    /// (or the same player with a count set). A question that isn't in the list is a slip in the test.
    /// </summary>
    public static async Task<JsonObject> Answer(AskQuery question, bool countsSet = false)
    {
        var answers = (await (countsSet ? CountsSet : AsLogged).Value).Answers;
        Assert.True(answers.ContainsKey(question), $"PageMessage builds no answer to {question}: add the question to PageMessage.Asked");
        return JsonNode.Parse(answers[question])!.AsObject();
    }

    /// <summary>The made-up inventory export behind <see cref="UpgradesAnswer"/>: a name twice (two copies), two robes, and a stack that isn't gear.</summary>
    public static readonly string[] InventoryExportLines =
    [
        "Location\tName\tID\tCount\tSlots",
        "Equipment\tCudgel of the Fool +5\t177757",
        "Equipment\tSpear of Harmony\t27725",
        "Any Slot\tPyrosilk Robe +9\t181073\t1\t11",
        "General1-Slot3\tCloak of Flames +7\t1\t1\t0",
        "General1-Slot4\tBone Chips\t2\t20\t0",
        "Equipment\tCudgel of the Fool +5\t177757",
        "General2-Slot1\tRobe of the Oracle +2\t3\t1\t0",
    ];

    /// <summary>
    /// The Upgrades page's answer as the engine builds it (<see cref="UpgradePage.Build"/>) for the fixture's character
    /// with <see cref="InventoryExportLines"/> as their /outputfile inventory, an empty bank and no plan asked for yet.
    /// </summary>
    public static JsonObject UpgradesAnswer()
    {
        var game = Path.Combine(Path.GetTempPath(), "pf-pagemessage-" + Guid.NewGuid().ToString("N"));
        var logs = Directory.CreateDirectory(Path.Combine(game, "Logs")).FullName;
        try
        {
            File.WriteAllLines(InventoryExport.PathFor(logs, TestLog.Character, TestLog.Server), InventoryExportLines);
            var answer = UpgradePage.Build(TestLog.Logs(Path.Combine(logs, TestLog.LiveName)), new MoteBank(), [], null);
            return JsonNode.Parse(JsonSerializer.Serialize(answer, PageJson.Options))!.AsObject();
        }
        finally
        {
            try { Directory.Delete(game, true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// The same player at the level cap, the last crawl one at the cap that ended minutes ago: Home draws its farming
    /// card. The fixture stops at 23, and no checked-in log reaches the cap yet.
    /// </summary>
    public static async Task<JsonObject> FarmingAtTheCap()
    {
        var m = await FromFixture();
        var cap = m.Number("reinc.levelCap");
        return m.With("reinc.homeNow.mode", "farming").With("reinc.homeNow.level", cap).With("reinc.currentLevel", cap).With("snapshot.level", cap).With("history.0.levelStart", cap).With("history.0.levelEnd", cap);
    }

    /// <summary>Sets the value at a dotted path ("history.0.levelStart") that the message already has, to a value of the same kind.</summary>
    public static JsonObject With(this JsonObject message, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        JsonNode at = message;
        foreach (var part in parts[..^1]) at = Child(at, part, path);
        var was = Child(at, parts[^1], path);
        Assert.True(Kind(was) == Kind(value), $"the page data's {path} is {Kind(was)} ({was.ToJsonString()}), and the test sets {Kind(value)}");
        if (at is JsonArray list) list[int.Parse(parts[^1], CultureInfo.InvariantCulture)] = value;
        else at[parts[^1]] = value;
        return message;
    }

    /// <summary>The number at a dotted path.</summary>
    public static double Number(this JsonObject message, string path) => At(message, path).GetValue<double>();

    /// <summary>The text at a dotted path.</summary>
    public static string String(this JsonObject message, string path) => At(message, path).GetValue<string>();

    /// <summary>The list at a dotted path.</summary>
    public static JsonArray List(this JsonObject message, string path) => At(message, path).AsArray();

    private static JsonNode At(JsonObject message, string path)
    {
        JsonNode at = message;
        foreach (var part in path.Split('.')) at = Child(at, part, path);
        return at;
    }

    private static JsonNode Child(JsonNode at, string part, string path)
    {
        var child = at switch
        {
            JsonObject o => o[part],
            JsonArray a when int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < a.Count => a[i],
            _ => null,
        };
        Assert.True(child != null, $"the message built from the fixture has no {path} (nothing at \"{part}\"): a page test only changes values the engine wrote");
        return child!;
    }

    private static string Kind(JsonNode? n) => n?.GetValueKind() switch
    {
        null => "null",
        JsonValueKind.True or JsonValueKind.False => "true or false",
        var k => k.ToString()!.ToLowerInvariant(),
    };

    private static JsonObject Parse(Built built, bool plan) => JsonNode.Parse(plan ? built.Data : built.DataWithoutAPlan)!.AsObject();

    /// <summary>
    /// As the window asks (MainWindow.Refresh, SendAsk): on the tracker's turn, written with the engine's options. One
    /// read of the log gives the page data, with a campaign and without, and every answer the page tests send.
    /// </summary>
    private static async Task<Built> Build(bool countsSet)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-pagemessage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, TestLog.LiveName);
        File.Copy(TestLog.BefallenUnrest, log);
        try
        {
            var options = new TrackerOptions();
            if (countsSet) options.EnteredCounts[Roster.Key(false, CountsSetFor)] = new EnteredCount(CountsSetTo, CountsSetOn);
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, TestLog.Logs(log), options);
            await svc.ImportHistoryAsync();
            static string Written(object message) => JsonSerializer.Serialize(message, PageJson.Options);
            return await svc.Query(s => new Built(
                Written(PageData.Build(s, s.BuildSnapshot(), s.RebuildCampaign(Campaign.Preset("all"), new CampaignRoles([], false)), PageDataTests.Settings())),
                Written(PageData.Build(s, s.BuildSnapshot(), null, PageDataTests.Settings())),
                Asked.ToDictionary(q => q, q => Written(s.AskPage(q)))));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
