using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class ShareTests
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

    private static (PathfinderEngine e, Sink s) Run()
    {
        var s = new Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in new[]
        {
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Paladin\" at a cost of 0 ability points."),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Monk\" at a cost of 0 ability points."),
            L("11:59:39", "You have gained the ability \"Class Reincarnation: Enchanter\" at a cost of 0 ability points."),
            L("12:00:00", "[1 PAL/MNK/ENC] Venun (Iksar) ZONE: Befallen"),
            L("12:00:10", "You have entered Befallen 4 (Refined)."),
            L("12:05:00", "Venun tells the group, 'pulling the big one'"),
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
        return (e, s);
    }

    private static SharePayload Build(byte[] salt, DateTime? since = null)
    {
        var (e, s) = Run();
        var visits = new List<Visit>(s.Closed);
        var js = Journeys.Build(visits, e.State, new TrackerOptions(), FieldHunts.From(s.Kills, 50));
        return ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", salt, visits, js, s.Kills, s.Levels, e.State, new TrackerOptions(), since);
    }

    [Fact]
    public void ASlotsTrioIsSharedAsCarriedAndTheSlotIsNeverSent()
    {
        var (e, s) = Run();
        var v = Assert.Single(s.Closed);
        (v.TrioConfirmed, v.TrioFrom, v.Slot) = (false, "slot", 6);
        var p = ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", new byte[16], s.Closed, [], s.Kills, s.Levels, e.State, new TrackerOptions());
        var crawl = Assert.Single(p.Crawls);
        Assert.Equal(("PAL/MNK/ENC", false, "carried"), (crawl.Trio, crawl.TrioConfirmed, crawl.TrioSource));
        var json = JsonSerializer.Serialize(p);
        Assert.DoesNotContain("\"Slot\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TrioFrom", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharingOnlyNewPlayLeavesOutWhatCameBefore()
    {
        var p = Build(new byte[16], new DateTime(2026, 11, 11, 12, 11, 0));
        Assert.Empty(p.Crawls);
        Assert.Empty(p.Reincarnations);
        Assert.Single(p.Stretches);
        Assert.Equal("2026-11-11", p.Since);
    }

    [Fact]
    public void PlayDuringABonusXpEventGoesMarked()
    {
        // A crawl during the event is sent with "bonus": true, so the community job can leave it out; play after it, and
        // every unit without one, carries no such field (nothing else changes on the wire, so nothing is sent again).
        var (e, s) = Run();
        SharePayload Share() => ShareBuilder.Build("0.7.1", "oggok", "Venun_oggok", new byte[16], s.Closed, [], s.Kills, s.Levels, e.State, new TrackerOptions());
        var before = ShareUnits.From(Share());
        Assert.Null(Assert.Single(Share().Crawls).Bonus);
        e.State.NoteBonusXp(new DateTime(2026, 11, 11, 12, 5, 0));
        var p = Share();
        Assert.True(Assert.Single(p.Crawls).Bonus);
        Assert.Null(Assert.Single(p.Stretches).Bonus);   // the rat at 12:13, after it
        Assert.Contains("\"bonus\":true", JsonSerializer.Serialize(p.Crawls[0], ShareClient.Json));
        Assert.DoesNotContain("bonus", JsonSerializer.Serialize(p.Stretches[0], ShareClient.Json));
        // Only the marked unit changes, so only it is sent again.
        var after = ShareUnits.From(p);
        Assert.NotEqual(before.Single(u => u.Kind == "crawl").Hash, after.Single(u => u.Kind == "crawl").Hash);
        Assert.Equal(before.Where(u => u.Kind != "crawl").Select(u => u.Hash), after.Where(u => u.Kind != "crawl").Select(u => u.Hash));
    }

    [Fact]
    public void UnitsCarryTheWholeUnitAndAHashThatOnlyChangesWithIt()
    {
        var p = Build(new byte[16]);
        var units = ShareUnits.From(p);
        Assert.Equal(["reincarnation", "reincarnation", "reincarnation", "crawl", "stretch", "relevel"], units.Select(u => u.Kind));
        Assert.All(units, u => Assert.Matches("^[0-9a-f]{20}$", u.Id));
        Assert.Equal(units.Count, units.Select(u => u.Id).Distinct().Count());
        var crawl = units[3];
        Assert.Equal("Befallen", crawl.Zone);
        Assert.Equal(4, crawl.Difficulty);
        Assert.Contains("\"kills\":[[290,60,0,null,1],[590,30,1,null,2]]", ShareUnits.Unzip(crawl.Facts));
        Assert.Equal(units.Select(u => u.Hash), ShareUnits.From(Build(new byte[16])).Select(u => u.Hash));
        // Only the facts and the schema re-send a unit: not a newer app, a newer engine, or new derived numbers (the job
        // works those out itself), which re-sent whole histories until the server's daily limit stopped them (11-17).
        Assert.Equal(crawl.Hash, ShareUnits.From(p with { AppVersion = "9.9.9" })[3].Hash);
        Assert.Equal(crawl.Hash, ShareUnits.From(p with { Engine = p.Engine + 1 })[3].Hash);
        var c0 = p.Crawls[0];
        Assert.NotEqual(crawl.Hash, ShareUnits.From(p with { Crawls = [c0 with { Deaths = [1, 2] }] })[3].Hash);
        Assert.NotEqual(crawl.Hash, ShareUnits.From(p with { Schema = p.Schema + 1 })[3].Hash);
        // What's uploaded carries facts only: the job works every number out from them (share-without-derived).
        Assert.DoesNotContain("\"derived\":", ShareUnits.Unzip(crawl.Facts));
        var batch = Assert.Single(ShareUnits.Batches(p, units));
        Assert.Equal(p.Character, batch.Character);
        Assert.Equal(6, batch.Rows.Count);
    }

    [Fact]
    public void SignaturesVerifyAgainstTheInstallsPublicKeyInTheServersFormat()
    {
        using var key = ShareProtocol.NewKey();
        var spki = key.ExportSubjectPublicKeyInfo();
        var install = ShareProtocol.InstallId(spki);
        Assert.Matches("^[0-9a-f]{32}$", install);
        var body = "{\"a\":1}"u8.ToArray();
        var canonical = ShareProtocol.Canonical("POST", "/v1/summaries", 1794817044, install, body);
        Assert.Equal($"EQLP1\nPOST\n/v1/summaries\n1794817044\n{install}\n015abd7f5cc57a2dd94b7590f04ad8084273905ee33ec5cebeae62276a97f862", canonical);
        var sig = ShareProtocol.Sign(key, canonical);
        Assert.Equal(64, sig.Length);   // r||s, as WebCrypto verifies it
        using var pub = System.Security.Cryptography.ECDsa.Create();
        pub.ImportSubjectPublicKeyInfo(spki, out _);
        Assert.True(pub.VerifyData(System.Text.Encoding.UTF8.GetBytes(canonical), sig, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public void ThePreviewHoldsTheFactsButNoNameAndNoClockTime()
    {
        var salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        var p = Build(salt);
        Assert.Equal(5, p.Schema);
        var crawl = Assert.Single(p.Crawls);
        Assert.Equal("2026-11-11", crawl.Day);
        Assert.Equal("PAL/MNK/ENC", crawl.Trio);
        Assert.NotNull(crawl.TrioSource);
        Assert.NotNull(crawl.Casts);
        Assert.Equal(1, crawl.Rank);
        // Each kill: seconds into the crawl, XP %, party, kill time, level. The second gave party experience.
        Assert.Equal(["[290,60,0,null,1]", "[590,30,1,null,2]"], crawl.Kills.Select(k => k.Json));
        Assert.Equal(["[590,2]"], crawl.Dings.Select(d => d.Json));
        // Schema 4: the facts the job works active time out from agree with what the app sent.
        Assert.NotNull(crawl.End);
        Assert.NotNull(crawl.Pauses);
        var (levels, killLevels, active) = CommunityBuilder.Recompute(crawl);
        Assert.Equal(0.9, levels, 3);
        Assert.Equal(0.9, killLevels, 3);
        var (e2, s2) = Run();
        var built = Assert.Single(s2.Closed);
        Assert.True(active > 0);
        Assert.Equal(ActiveTime.Minutes(built, new TrackerOptions().IdleThresholdSeconds, e2.State.LastTime ?? DateTime.Now), active, 3);
        // Reincarnations come first in the order, then the crawl, then the rat hunted outside.
        Assert.Equal([1, 2, 3], p.Reincarnations.Select(r => r.Seq));
        Assert.Equal(4, crawl.Seq);
        var stretch = Assert.Single(p.Stretches);
        Assert.Equal(5, stretch.Seq);
        Assert.Equal(["[0,12,0,null,2]"], stretch.Kills.Select(k => k.Json));
        // The hunt carries who was hunting, as the app's own Ask has it: the crawl just before it (0.9.30).
        Assert.Equal((crawl.Trio, crawl.Rank), (stretch.Trio, stretch.Rank));
        Assert.NotNull(stretch.Trio);
        var unit = ShareUnits.From(p).Single(u => u.Kind == "stretch");
        Assert.Equal((stretch.Trio, crawl.Rank), (unit.Trio, unit.Rank));
        Assert.Null(p.Since);   // all play (decided 11-16), not only since the first reincarnation
        var json = JsonSerializer.Serialize(p);
        Assert.Contains("\"Kills\":[[290,60,0,null,1],[590,30,1,null,2]]", json);
        Assert.DoesNotContain("Venun", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pulling", json);
        Assert.DoesNotContain("skeleton", json);
        Assert.DoesNotContain("12:0", json);
    }

    [Fact]
    public void CodesAreStableForOneSaltAndDifferForAnother()
    {
        var a = Build(Enumerable.Repeat((byte)7, 16).ToArray());
        var b = Build(Enumerable.Repeat((byte)7, 16).ToArray());
        var c = Build(Enumerable.Repeat((byte)8, 16).ToArray());
        Assert.Equal(a.Character, b.Character);
        Assert.Equal(a.Crawls[0].Id, b.Crawls[0].Id);
        Assert.NotEqual(a.Character, c.Character);
        Assert.NotEqual(a.Crawls[0].Id, c.Crawls[0].Id);
    }

    [Fact]
    public void ACopyKeepsOnlyASharingKeyItMadeItself()
    {
        // A key made before 11-17 has no home (it's the real install's). The check threw on it, so a test copy given the
        // real settings.json dropped all of them and stopped at first-run setup.
        const string copy = @"C:\Temp\eqlp-copy";
        Assert.False(ShareKeyHome.MadeIn(null, copy));
        Assert.False(ShareKeyHome.MadeIn("", copy));
        Assert.False(ShareKeyHome.MadeIn(@"C:\Users\someone\AppData\Local\EQL Pathfinder", copy));
        Assert.True(ShareKeyHome.MadeIn(copy, copy));
        Assert.True(ShareKeyHome.MadeIn(@"c:\temp\EQLP-COPY\", copy));
    }
}
