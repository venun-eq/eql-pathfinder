using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// share-without-derived (docs/specs/share-without-derived.md): a shared unit carries facts only, the community job works
/// every number out from them, and the app withdraws the units it no longer builds.
/// </summary>
public class ShareWithoutDerivedTests
{
    private sealed class Sink : ITrackerSink
    {
        public List<Visit> Closed { get; } = new();
        public List<KillRecord> Kills { get; } = new();
        public List<LevelRecord> Levels { get; } = new();
        public void OnVisitClosed(Visit v) => Closed.Add(v);
        public void OnKill(KillRecord k) => Kills.Add(k);
        public void OnLevel(LevelRecord l) => Levels.Add(l);
    }

    private static string L(string hms, string body) => $"[Wed Nov 11 {hms} 2026] {body}";

    /// <summary>A Befallen D4 crawl of two kills, then a hunt outside of one kill: a crawl, a stretch and a relevel to share.</summary>
    private static SharePayload Build()
    {
        var o = new TrackerOptions();
        var s = new Sink();
        var e = new PathfinderEngine("Standin", o, s);
        foreach (var l in new[]
        {
            L("12:00:00", "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            L("12:00:00", "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L("12:00:00", "You have gained the ability \"Class Reincarnation: Enchanter\" at a cost of 0 ability points."),
            L("12:00:01", "[1 PAL/MNK/ENC] Standin (Iksar) ZONE: Befallen"),
            L("12:00:10", "You have entered Befallen 4 (Refined)."),
            L("12:05:00", "You gain experience! (60.000%)"),
            L("12:05:00", "You have slain a skeleton!"),
            L("12:10:00", "You have gained a level! Welcome to level 2!"),
            L("12:10:00", "You gain party experience! (30.000%)"),
            L("12:10:00", "You have slain a skeleton!"),
            L("12:12:00", "You have entered Befallen."),
            L("12:13:00", "You gain experience! (12.000%)"),
            L("12:13:00", "You have slain a rat!"),
            L("12:40:00", "You have entered Befallen 4 (Refined)."),
        }) e.ProcessLine(l);
        e.Flush();
        var visits = new List<Visit>(s.Closed);
        var js = Journeys.Build(visits, e.State, o, FieldHunts.From(s.Kills, 50));
        return ShareBuilder.Build("0.10.35", "oggok", "Standin_oggok", new byte[16], visits, js, s.Kills, s.Levels, e.State, o);
    }

    private static JsonObject Facts(ShareUnit u) => JsonNode.Parse(ShareUnits.Unzip(u.Facts))!.AsObject();

    [Fact]
    public void WhatsSentCarriesTheFactsAndNoNumbersTheAppWorkedOut()
    {
        var p = Build();
        var units = ShareUnits.From(p);
        var crawl = units.Single(u => u.Kind == "crawl");
        var stretch = units.Single(u => u.Kind == "stretch");
        // The facts the job works from are there...
        Assert.True(Facts(crawl).ContainsKey("end"));
        Assert.True(Facts(crawl).ContainsKey("kills"));
        Assert.True(Facts(stretch).ContainsKey("kills"));
        // ...and nothing this copy of the app worked out from them: not in the facts, not in the indexed columns.
        Assert.False(Facts(crawl).ContainsKey("derived"), "a crawl's facts still carry derived");
        Assert.False(Facts(stretch).ContainsKey("derived"), "a stretch's facts still carry derived");
        Assert.Null(crawl.Derived);
        Assert.Null(stretch.Derived);
        // A new schema, so the server and the job can tell a unit that never carried them.
        Assert.True(p.Schema >= 5, $"schema is {p.Schema}");
    }

    [Fact]
    public void ACrawlFromBeforeSchemaFourIsLeftOutWhateverItWasSentWith()
    {
        var crawl = ShareUnits.From(Build()).Single(u => u.Kind == "crawl");
        var now = Facts(crawl);
        Assert.NotNull(CommunityBuilder.Visit(CommunityBuilder.ReadCrawl(now.ToJsonString())!));   // as sent today: kept

        // As an app before schema 4 sent it: no end and no pauses, and the numbers its own math worked out.
        var old = Facts(crawl);
        old.Remove("end");
        old.Remove("pauses");
        old["derived"] = new JsonObject
        {
            ["levels"] = 0.9,
            ["killLevels"] = 0.9,
            ["rewardLevels"] = 0,
            ["activeMinutes"] = 30,
            ["idleMinutes"] = 0,
        };
        var seg = CommunityBuilder.Visit(CommunityBuilder.ReadCrawl(old.ToJsonString())!, out var why);
        Assert.Null(seg);
        Assert.NotNull(why);
        Assert.Contains("schema 4", why);   // the job's log line says why, not "impossible time or levels"
    }

    [Fact]
    public void AHuntWithNoKillsIsLeftOutWhateverItWasSentWith()
    {
        var stretch = ShareUnits.From(Build()).Single(u => u.Kind == "stretch");
        var now = Facts(stretch);
        Assert.NotNull(CommunityBuilder.Hunt(CommunityBuilder.ReadStretch(now.ToJsonString())!, []));   // with its kill: kept

        var bare = Facts(stretch);
        bare["kills"] = new JsonArray();
        bare["derived"] = new JsonObject { ["minutes"] = 30, ["levels"] = 1 };
        Assert.Null(CommunityBuilder.Hunt(CommunityBuilder.ReadStretch(bare.ToJsonString())!, []));
    }

    [Fact]
    public void AUnitTheAppNoLongerBuildsIsWithdrawnAndNothingElse()
    {
        var built = ShareUnits.From(Build());
        // The ledger: everything built was sent, one of them before its facts changed (a new hash, the same id), and a
        // hunt whose id moved (a re-read moved its first kill) is still there under its old id.
        var sent = built.ToDictionary(u => u.Id, u => u.Hash);
        sent[built[0].Id] = "an-older-hash";
        const string moved = "00112233445566778899";
        sent[moved] = "its-hash";

        Assert.Equal([moved], ShareUnits.Withdrawn(sent, built));
    }

    [Fact]
    public void NothingIsWithdrawnWhenTheBuildHoldsTooLittleToTrust()
    {
        var built = ShareUnits.From(Build());
        var sent = built.ToDictionary(u => u.Id, u => u.Hash);
        sent["00112233445566778899"] = "its-hash";
        Assert.Single(ShareUnits.Withdrawn(sent, built));   // one of seven gone: withdrawn

        // Nothing built for the character (a store that didn't open, a read that hasn't run): nothing withdrawn.
        Assert.Empty(ShareUnits.Withdrawn(sent, []));
        // More than half of what was sent gone at once looks like a build gone wrong, not play that moved: nothing withdrawn.
        Assert.Empty(ShareUnits.Withdrawn(sent, built.Take(2).ToList()));
    }
}
