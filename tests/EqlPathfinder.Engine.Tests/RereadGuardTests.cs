using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EqlPathfinder.Engine.Combat;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Parsing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// What's read from a log is decided once, when the line is read, and stored. A change to those rules reaches the
/// history players already have only when the reader's version is raised, so the app reads every log again
/// (docs/engineering.md, Modeling 6). These tests hold each reader's version beside a hash of what it makes of the
/// fixture: change what's read without raising the version and they fail.
/// The hash covers only what the fixture holds (levels 2 to 23, crawls at D1 and D4): a change to lines it doesn't
/// have needs those lines added to what's hashed, or it gets past (docs/quality.md).
/// </summary>
public class RereadGuardTests
{
    // Raise the version and set its hash in the same commit, with the release note's `reread: true`.
    private const int TrackerVersion = 16;
    private const string TrackerHash = "d756d787f4c96062";
    private const int MeterVersion = 6;
    private const string MeterHash = "1c1bb6d1130c17e6";

    [Fact]
    public void WhatTheTrackerReadsChangesOnlyWithItsVersion() =>
        Hold("the crawl tracker", "EngineState.CurrentVersion", EngineState.CurrentVersion, Hash(TrackerRecords()),
            TrackerVersion, TrackerHash, "TrackerVersion", "TrackerHash");

    [Fact]
    public void WhatTheFightMeterReadsChangesOnlyWithItsVersion() =>
        Hold("the fight meter", "FightMeterService.CurrentVersion", FightMeterService.CurrentVersion, Hash(MeterRecords()),
            MeterVersion, MeterHash, "MeterVersion", "MeterHash");

    [Fact]
    public void TheHashIsTheSameOnEveryRead()
    {
        Assert.Equal(Hash(TrackerRecords()), Hash(TrackerRecords()));
        Assert.Equal(Hash(MeterRecords()), Hash(MeterRecords()));
    }

    [Fact]
    public void ANumberWorkedOutOnLoadIsNotPartOfTheHash()
    {
        // RewardPercent, IsCrawlish and the like are worked out from the facts each time a visit is loaded, so a change
        // to them needs no re-read. Facts are what has a setter: what the store reads back.
        var v = new Visit { Id = "a", Kills = 3, Rewards = [new VisitReward { Id = "r", Share = 40 }] };
        var text = JsonSerializer.Serialize(v, Facts);
        Assert.Contains("\"Kills\":3", text);
        Assert.Contains("\"Share\":40", text);
        Assert.DoesNotContain("RewardPercent", text);
        Assert.DoesNotContain("IsCrawlish", text);
        Assert.DoesNotContain("MoteCount", text);
    }

    [Fact]
    public void AFactTheStoreKeepsInItsOwnColumnIsPartOfTheHash()
    {
        // A fight's start, mob and zone are columns of their own in the fight store, so its JSON leaves them out
        // ([JsonIgnore]). They're still read from the log.
        var text = JsonSerializer.Serialize(new Fight { Mob = "a gnoll", Zone = "Befallen" }, Facts);
        Assert.Contains("\"Mob\":\"a gnoll\"", text);
        Assert.Contains("\"Zone\":\"Befallen\"", text);
    }

    private static void Hold(string reader, string constant, int version, string hash, int heldVersion, string heldHash,
        string versionName, string hashName)
    {
        if (version == heldVersion && hash == heldHash) return;
        Assert.Fail(version == heldVersion
            ? $"What {reader} reads from logs changed, and {constant} is still {version}. Players' history would be figured " +
              $"one way and new play another, and every comparison between them would drift. Raise {constant} to " +
              $"{version + 1} so the app reads every log again, mark the release note `reread: true`, and set " +
              $"{versionName} = {version + 1} and {hashName} = \"{hash}\" here."
            : $"{constant} is {version}: set {versionName} = {version} and {hashName} = \"{hash}\" here, in the same commit.");
    }

    private const string AaCap = "You have reached the AA point cap, and cannot gain any further experience until some of your stored AA point pool is used.";

    private static readonly (string, string)[] AtTheCap =
    [
        ("12:00:00", "You have gained a level! Welcome to level 50!"),
        ("12:00:10", "You have entered Nektropos Castle 4 (Refined)."),
        ("12:01:00", "You gain experience! (1.250%)"),
        ("12:01:00", "You have slain a ghoul!"),
        ("12:02:00", AaCap),
        ("12:02:00", "You gain experience!"),
        ("12:02:00", "You have slain a ghoul!"),
        ("12:03:00", "You gain party experience!"),
        ("12:03:00", "A ghoul has been slain by Groupmate!"),
        ("12:04:00", "You have slain a ghoul!"),
        ("12:05:00", "A ghoul has been slain by Stranger!"),
        ("12:06:00", "You gain experience!"),
        ("12:30:00", "You have slain a ghoul!"),
        ("12:31:00", "You have entered Nektropos Castle."),
    ];

    /// <summary>
    /// The fixture has no D0 crawl left by a zone line and walked back into, and no 1-point hit (visit-boundaries): a D0
    /// crawl out to a neighbour, back through the open world, with 1-point hits each way, so a change to either moves the hash.
    /// </summary>
    private static readonly (string, string)[] BackThroughTheOpenWorld =
    [
        ("12:00:00", "You have entered Nagafen's Lair."),
        ("12:00:40", "Player Standin creating instance Nagafen's Lair 101."),
        ("12:01:10", "You have entered Nagafen's Lair."),
        ("12:02:00", "You slash a greater kobold for 1 point of damage."),
        ("12:02:01", "A greater kobold bashes YOU for 1 point of damage."),
        ("12:02:02", "Groupmate bashes a greater kobold for 1 point of damage."),
        ("12:02:03", "You slash a greater kobold for 300 points of damage."),
        ("12:02:03", "You gain experience! (2.000%)"),
        ("12:02:03", "You have slain a greater kobold!"),
        ("12:38:30", "You have entered Solusek's Eye."),
        ("12:38:45", "You have entered Nagafen's Lair."),
        ("12:38:50", "You looted a Mote of Infinitesimal Potential from a kobold's corpse and stored it in your currency"),
        ("12:39:02", "You have entered Nagafen's Lair."),
        ("12:40:00", "You slash a greater kobold for 300 points of damage."),
        ("12:40:00", "You gain experience! (2.000%)"),
        ("12:40:00", "You have slain a greater kobold!"),
        ("12:51:00", "You have entered Toxxulia Forest."),
    ];

    /// <summary>Everything the tracker hands the store, in the order it does, then the state it carries on from.</summary>
    private static List<string> TrackerRecords()
    {
        var sink = new Recorder();
        var e = new PathfinderEngine(TestLog.Character, new TrackerOptions(), sink);
        e.ReadFile(TestLog.BefallenUnrest);
        // The fixture has no play at the level cap: a crawl at 50 in each XP line form the logs have there (kills-at-cap,
        // docs/log-formats.md "Kill XP"), so a change to how they're read moves the hash.
        foreach (var (hms, body) in AtTheCap) e.ProcessLine($"[Sun Nov 15 {hms} 2026] {body}");
        foreach (var (hms, body) in BackThroughTheOpenWorld) e.ProcessLine($"[Mon Nov 16 {hms} 2026] {body}");
        e.Flush();
        Assert.True(sink.Lines.Count(l => l.StartsWith("visit ")) >= 5, "the fixture's crawls weren't read");
        var state = JsonSerializer.SerializeToNode(e.State, Facts)!.AsObject();
        state.Remove(nameof(EngineState.Version));   // the version is held beside the hash, not inside it
        sink.Lines.Add("state " + state.ToJsonString());
        return sink.Lines;
    }

    /// <summary>Every fight the meter closes, read as FightMeterService.Import reads a log.</summary>
    private static List<string> MeterRecords()
    {
        var lines = new List<string>();
        var context = new FightContext();
        var tracker = new FightTracker(TestLog.Character, context, f => lines.Add("fight " + JsonSerializer.Serialize(f, Facts)));
        var parser = new LineParser(TestLog.Character, context.Pets, context.Charm);
        LogReader.ReadFrom(TestLog.BefallenUnrest, 0, raw =>
        {
            if (!LogLine.TryParse(raw, out var line)) return;
            tracker.OnLine(line.Time);
            var ev = parser.Parse(line) ?? CombatParser.Parse(line);
            if (ev != null) tracker.Apply(ev);
        });
        foreach (var (hms, body) in BackThroughTheOpenWorld)
        {
            var line = new LogLine(DateTime.Parse($"2026-11-16T{hms}", System.Globalization.CultureInfo.InvariantCulture), body);
            tracker.OnLine(line.Time);
            var ev = parser.Parse(line) ?? CombatParser.Parse(line);
            if (ev != null) tracker.Apply(ev);
        }
        tracker.CloseAll();
        Assert.True(lines.Count > 100, "the fixture's fights weren't read");
        return lines;
    }

    private sealed class Recorder : ITrackerSink
    {
        public List<string> Lines { get; } = [];
        public void OnVisitClosed(Visit v) => Lines.Add("visit " + JsonSerializer.Serialize(v, Facts));
        public void OnVisitUpdated(Visit v) => Lines.Add("visit-updated " + JsonSerializer.Serialize(v, Facts));
        public void OnKill(KillRecord k) => Lines.Add("kill " + JsonSerializer.Serialize(k, Facts));
        public void OnLoot(LootRecord l) => Lines.Add("loot " + JsonSerializer.Serialize(l, Facts));
        public void OnLevel(LevelRecord l) => Lines.Add("level " + JsonSerializer.Serialize(l, Facts));
        public void OnHighTier(HighTierDrop d) => Lines.Add("high-tier " + JsonSerializer.Serialize(d, Facts));
    }

    private static string Hash(List<string> lines) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))))[..16].ToLowerInvariant();

    /// <summary>
    /// Facts only, whatever the store's own JSON says: every public property with a public setter, by name, in name
    /// order. A getter worked out on load isn't one, a column the store keeps beside the JSON is, and moving a property
    /// in the source changes nothing.
    /// </summary>
    private static readonly JsonSerializerOptions Facts = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OnlyFacts } },
    };

    private static void OnlyFacts(JsonTypeInfo type)
    {
        if (type.Kind != JsonTypeInfoKind.Object) return;
        type.Properties.Clear();
        foreach (var p in type.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (p.GetIndexParameters().Length > 0 || p.GetGetMethod() == null || p.GetSetMethod() == null) continue;
            var fact = type.CreateJsonPropertyInfo(p.PropertyType, p.Name);
            fact.Get = p.GetValue;
            type.Properties.Add(fact);
        }
    }
}
