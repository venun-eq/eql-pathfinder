using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The page data: the one message without a kind, which every page draws from. It moved from the window into the engine
/// (docs/specs/page-data-in-engine.md), and these hold it to the message the window sent before the move. They stay as
/// the list of what a page is sent: a new field is added here as well as to the builder.
///
/// The messages are built here from the fixture on every run and never written anywhere: they hold the character's
/// name and clock times (docs/quality.md).
/// </summary>
public class PageDataTests
{
    /// <summary>MainWindow.Json as it was on main (d451aba, 11-17), frozen: the "before" is written with this, not with the engine's own.</summary>
    private static readonly JsonSerializerOptions Main = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>MainWindow.Refresh's message as it was on main (d451aba), field for field and in its order.</summary>
    private static object Before(TrackerService s, LiveSnapshot? snap, CampaignPlan? campaign, JsonNode settings) => new
    {
        character = s.Logs.Name,
        server = s.Logs.Server,
        fragmentValue = s.FragmentValue,
        generated = DateTime.Now,
        snapshot = snap,
        history = s.History(5000),
        drops = s.NotableDrops(5000),
        ledger = s.LootLedger(),
        reinc = s.ReincarnationData(campaign),
        settings,
    };

    /// <summary>
    /// App.SettingsForPage's object as it was on main (d451aba): every name, in its order, as a page reads it from
    /// data.settings. The values are made up, and few are a default, so a field that's dropped or filled from its
    /// neighbour shows. fragmentRecommendedValue and fragmentChoices are the engine's own numbers (Potential).
    /// </summary>
    private static JsonNode SettingsOnMain()
    {
        var s = JsonNode.Parse("""
            {
              "fragmentValue": 2.5, "fragmentRecommended": false, "fragmentRecommendedValue": null, "sound": true,
              "fragmentChoices": null,
              "overlays": [
                { "id": "crawl", "name": "Crawl overlay", "about": "What the crawl overlay is for.", "visible": true, "locked": false,
                  "opacity": 0.92, "options": { "size": "Compact", "mode": "Auto" } },
                { "id": "meter", "name": "Fight meter", "about": "What the fight meter is for.", "visible": false, "locked": true,
                  "opacity": 0.5, "options": { "scope": "fight", "fightEnd": "8" } }
              ],
              "overlaysOnlyWithGame": true, "overlaysLocked": false, "arranging": true,
              "shareIntroSeen": true,
              "farmRate": 7.5, "farmRateMode": "manual", "goal": "motes", "farmDifficulties": [2, 4], "startWindow": "Tray",
              "version": "9.9.9", "pendingUpdate": null, "updatesEnabled": true,
              "logsDir": "C:\\Games\\Logs", "character": "Testchar_testserver", "gameRunning": true,
              "beta": true, "onBetaBuild": false, "shareEnabled": true,
              "usage": false, "noticesSeen": ["usage-counts"], "campaignAnswered": 3
            }
            """)!;
        s["fragmentRecommendedValue"] = Potential.RecommendedFragmentValue;
        s["fragmentChoices"] = JsonSerializer.SerializeToNode(Potential.FragmentValueChoices.Select(c => new { goal = c.Goal, value = c.Value }).ToList());
        return s;
    }

    /// <summary>
    /// The same settings as the window will hand them to the builder: read into the engine's type, so every name has to be one it knows.
    /// The page tests send these too (PageMessage).
    /// </summary>
    internal static PageSettings Settings() => JsonSerializer.Deserialize<PageSettings>(SettingsOnMain().ToJsonString(), PageJson.Options)!;

    [Fact]
    public async Task EveryPageGetsTheDataMessageItGotBefore()
    {
        var fixture = TestLog.BefallenUnrest;
        var (before, after) = await Messages(fixture, s =>
        {
            // The overlay's last snapshot and a whole campaign, as the window passes them in while you play.
            return (s.BuildSnapshot(), s.RebuildCampaign(Campaign.Preset("all"), new CampaignRoles([], false)));
        });
        using var was = JsonDocument.Parse(before);
        Assert.True(was.RootElement.GetProperty("history").GetArrayLength() > 0, "the fixture should give the pages some crawls");
        Assert.Equal(JsonValueKind.Object, was.RootElement.GetProperty("snapshot").ValueKind);
        AssertSameMessage(before, after);
    }

    /// <summary>Nothing yet: the log was turned on today and the overlay hasn't drawn. Every field is still there, as before.</summary>
    [Fact]
    public async Task ANewPlayersPagesGetEveryFieldToo()
    {
        var (before, after) = await Messages(null, _ => (null, new CampaignPlan()));
        using var was = JsonDocument.Parse(before);
        Assert.Equal(0, was.RootElement.GetProperty("history").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, was.RootElement.GetProperty("snapshot").ValueKind);
        AssertSameMessage(before, after);
    }

    /// <summary>What the pages lean on in every message, whatever is in it (the window's own answers are written with the same options).</summary>
    [Fact]
    public void AMessageIsWrittenTheWayThePagesReadIt()
    {
        var json = JsonSerializer.Serialize(new
        {
            BestTier = MoteTier.Grand,                                  // an enum by its name, not its number
            PendingUpdate = (string?)null,                              // a missing value is null, not left out
            ByPlace = new Dictionary<string, int> { ["Befallen"] = 2 }, // a place's or a class's name keeps its capitals
        }, PageJson.Options);
        Assert.Equal("""{"bestTier":"Grand","pendingUpdate":null,"byPlace":{"Befallen":2}}""", json);
        // A rate over no time doesn't stop the whole message: it arrives as a word the page can test for.
        Assert.Equal("""{"rate":"NaN","peak":"Infinity"}""", JsonSerializer.Serialize(new { Rate = double.NaN, Peak = double.PositiveInfinity }, PageJson.Options));
    }

    private sealed record Posted(string Value, MoteTier Tier, double Rate);

    /// <summary>What the window reads from a page with the same options (a campaign from Plan → Campaign): names in any case, an enum by its name.</summary>
    [Fact]
    public void WhatAPagePostsIsReadWhateverTheCaseOfItsNames()
    {
        var read = JsonSerializer.Deserialize<Posted>("""{"VALUE":"x","tier":"Grand","rate":"NaN"}""", PageJson.Options);
        Assert.Equal(new Posted("x", MoteTier.Grand, double.NaN), read);
    }

    /// <summary>
    /// A setting the window forgets to fill would reach the pages as false or 0 and read as the player's choice: the
    /// preview's own copy lost six that way. So nothing in the settings can be left out: each is a required member or a
    /// constructor's parameter with no default, unless the engine works it out itself (no setter). This one reads the
    /// type, not a result, because the thing it guards is a build that fails.
    /// </summary>
    [Fact]
    public void ASettingTheWindowForgetsToFillDoesNotCompile()
    {
        var type = typeof(PageSettings);
        var mustPass = type.GetConstructors().SelectMany(c => c.GetParameters()).Where(p => !p.HasDefaultValue)
            .Select(p => p.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in SettingsOnMain().AsObject())
        {
            var member = type.GetProperties().FirstOrDefault(p => string.Equals(
                p.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false).Cast<JsonPropertyNameAttribute>().FirstOrDefault()?.Name ?? p.Name,
                name, StringComparison.OrdinalIgnoreCase));
            Assert.True(member != null, $"the settings have no {name}");
            if (member!.SetMethod == null) continue;
            Assert.True(mustPass.Contains(member.Name) || member.IsDefined(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), false),
                $"settings.{name} can be left out, and would arrive as its default");
        }
    }

    /// <summary>An overlay's row on Settings is filled by the window as well (its name, shown, locked, opacity): the same holds for each of its fields.</summary>
    [Fact]
    public void AnOverlaysRowCannotLeaveAFieldOutEither()
    {
        foreach (var (name, _) in SettingsOnMain()["overlays"]![0]!.AsObject())
        {
            var member = typeof(PageOverlay).GetProperties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            Assert.True(member != null, $"an overlay's row has no {name}");
            Assert.True(member!.IsDefined(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), false),
                $"settings.overlays[].{name} can be left out, and would arrive as its default");
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// The message as main built it and as the engine builds it now, from one service at one moment: a copy of the
    /// fixture, or an empty log for a new player. <paramref name="inputs"/> gives the snapshot and the campaign.
    /// </summary>
    private static async Task<(string Before, string After)> Messages(string? fixture, Func<TrackerService, (LiveSnapshot?, CampaignPlan)> inputs)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-pagedata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, TestLog.LiveName);
        if (fixture != null) File.Copy(fixture, log);
        else File.WriteAllText(log, "");
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, TestLog.Logs(log), new TrackerOptions());
            await svc.ImportHistoryAsync();
            // On the tracker's turn, as the window asks (MainWindow.Refresh).
            return await svc.Query(s =>
            {
                var (snap, campaign) = inputs(s);
                var before = JsonSerializer.Serialize(Before(s, snap, campaign, SettingsOnMain()), Main);
                var after = JsonSerializer.Serialize(PageData.Build(s, snap, campaign, Settings()), PageJson.Options);
                return (before, after);
            });
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// <summary>The same names in the same order, the same kinds and the same values all the way down. Only the time it was made may differ.</summary>
    private static void AssertSameMessage(string before, string after)
    {
        using var was = JsonDocument.Parse(before);
        using var now = JsonDocument.Parse(after);
        Assert.Equal(["character", "server", "fragmentValue", "generated", "snapshot", "history", "drops", "ledger", "reinc", "settings"],
            was.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        var difference = Difference(was.RootElement, now.RootElement, "data");
        Assert.True(difference == null, "The page data isn't what the window sent before. " + difference);
        // A message with a kind is an answer, and core.js never takes one for the page data.
        Assert.False(now.RootElement.TryGetProperty("kind", out _), "the page data has no kind");
        Assert.True(now.RootElement.GetProperty("generated").TryGetDateTime(out _), "generated is the time the message was made");
    }

    /// <summary>The first place two messages part, as a path a page would write (data.reinc.campaign.runs[3].xp), or null.</summary>
    private static string? Difference(JsonElement was, JsonElement now, string path)
    {
        static string Short(JsonElement e) => e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? e.ValueKind.ToString().ToLowerInvariant()
            : e.GetRawText() is { Length: > 60 } t ? t[..60] + "…" : e.GetRawText();
        if (was.ValueKind != now.ValueKind) return $"{path}: was {Short(was)}, now {Short(now)}";
        switch (was.ValueKind)
        {
            case JsonValueKind.Object:
                var w = was.EnumerateObject().ToList();
                var n = now.EnumerateObject().ToList();
                var wn = w.Select(p => p.Name).ToList();
                var nn = n.Select(p => p.Name).ToList();
                if (!wn.SequenceEqual(nn))
                {
                    var gone = wn.Except(nn).ToList();
                    var added = nn.Except(wn).ToList();
                    return gone.Count + added.Count > 0
                        ? $"{path}: no longer has [{string.Join(", ", gone)}], and now has [{string.Join(", ", added)}]"
                        : $"{path}: the same names in another order: was [{string.Join(", ", wn)}], now [{string.Join(", ", nn)}]";
                }
                for (int i = 0; i < w.Count; i++)
                {
                    if (path == "data" && w[i].Name == "generated") continue;
                    if (Difference(w[i].Value, n[i].Value, $"{path}.{w[i].Name}") is { } d) return d;
                }
                return null;
            case JsonValueKind.Array:
                if (was.GetArrayLength() != now.GetArrayLength()) return $"{path}: had {was.GetArrayLength()}, now has {now.GetArrayLength()}";
                for (int i = 0; i < was.GetArrayLength(); i++)
                    if (Difference(was[i], now[i], $"{path}[{i}]") is { } d) return d;
                return null;
            default:
                return was.GetRawText() == now.GetRawText() ? null : $"{path}: was {Short(was)}, now {Short(now)}";
        }
    }
}

/// <summary>campaign.firstLane (docs/specs/campaign-first-lane.md): in the data only for the new planner with a ranked plan.</summary>
public class PageDataFirstLaneTests
{
    [Fact]
    public async Task TheFirstLaneIsInTheCampaignDataOnlyForTheNewPlannerWithARankedPlan()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-lane-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, TestLog.LiveName);
        File.WriteAllText(log, "");
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, TestLog.Logs(log), new TrackerOptions());
            await svc.ImportHistoryAsync();
            var (ranked, unranked) = await svc.Query(s =>
            {
                CampaignPlan Plan(bool ranked)
                {
                    var p = Campaign.Preset("classes");
                    if (ranked) { p.RaceOrder = [new() { Key = "Iksar", Target = 3 }]; p.Races["Iksar"] = 3; }
                    return s.RebuildCampaign(p, new CampaignRoles([], false));
                }
                string Json(CampaignPlan p, CampaignRoles roles) => JsonSerializer.Serialize(s.CampaignData(p, s.Journeys(), roles), PageJson.Options);
                var r = Plan(true);
                return (Json(r, new CampaignRoles([], false)), Json(Plan(false), new CampaignRoles([], false)));
            });
            using var on = JsonDocument.Parse(ranked);
            Assert.True(on.RootElement.TryGetProperty("firstLane", out var lane));
            Assert.Equal(16, lane.GetProperty("everyRace").GetArrayLength());
            Assert.DoesNotContain("firstLane", unranked);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
