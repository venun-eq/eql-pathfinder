using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Sharing;

/// <summary>
/// Schema 2 of what community sharing sends ("EQL Pathfinder: Sharing spec"): the facts each crawl is made of
/// (every kill, reward, ding, death, damage per spell, loot), plus the numbers this app version works out from them
/// (<c>derived</c>, re-sent when an update changes the math). No names: a salted code stands in for the character and
/// each row. No clock times: a day, an order (<c>seq</c>), and seconds from the start of the crawl or stretch.
/// Phase 0 builds it for the Settings preview only; nothing is sent.
/// </summary>
public sealed record SharePayload(int Schema, string AppVersion, int Engine, string GameServer, string Character, string? Since,
    ShareColumns Columns, IReadOnlyList<SharedCrawl> Crawls, IReadOnlyList<SharedStretch> Stretches,
    IReadOnlyList<SharedReincarnation> Reincarnations, IReadOnlyList<SharedRelevel> Relevels);

/// <summary>What each position in the compact rows means.</summary>
public sealed record ShareColumns(CompactRow Kills, CompactRow Rewards, CompactRow Dings);

/// <param name="Seq">Order among all of this PC's crawls, stretches and reincarnations (instead of a clock time).</param>
/// <param name="TrioConfirmed">A /who during the crawl showed the trio; otherwise it was carried over from before.</param>
/// <param name="TrioSource">How this app knows the trio: "who", "spells" (the spells cast fit it), "partial" (they fit it and
/// others), "carried" (nothing cast to check it by) or "none". <see cref="SharedCrawl.Casts"/> are the facts it judged by.</param>
/// <param name="Casts">Spells begun ("You begin casting"), by name without rank, and how many times.</param>
/// <param name="Pauses">Schema 4: when you were out of the instance mid-visit, [from, to] in seconds from the start.</param>
/// <param name="End">Schema 4: where the visit's active time ends (completion or the last straggler, else leaving), in seconds
/// from the start. With <paramref name="Pauses"/> and the kill rows, the community job works out idle and active time itself
/// (<see cref="Tracking.ActiveTime"/>) instead of taking <see cref="CrawlDerived"/> on trust.</param>
/// <param name="Bonus">True when played during a bonus-XP event (excluded by the community job); null otherwise.</param>
public sealed record SharedCrawl(string Id, string Day, int Seq, string Kind, string Zone, int? Difficulty, string? Trio, bool TrioConfirmed,
    string TrioSource, string? Race,
    int Rank, int? LevelStart, int? LevelEnd, double ActiveSeconds, bool Completed, bool Refunded, double? ResetMinutes,
    IReadOnlyList<CompactRow> Kills, IReadOnlyList<CompactRow> Rewards, IReadOnlyList<CompactRow> Dings, IReadOnlyList<int> Deaths,
    SharedDamage Damage, IReadOnlyDictionary<string, int> Casts, SharedLoot Loot, CrawlDerived Derived,
    IReadOnlyList<int[]>? Pauses = null, int? End = null, bool? Bonus = null);

/// <summary>Damage dealt by source and by spell or ability (names are the game's), and damage taken.</summary>
public sealed record SharedDamage(long Total, double FightSeconds, long Taken, IReadOnlyDictionary<string, long> BySource,
    IReadOnlyDictionary<string, long> ByAbility);

/// <summary>Counts by tier number (1 = Infinitesimal … 10 = Infinite); the chest counts are the Reward Chest's share.</summary>
public sealed record SharedLoot(IReadOnlyDictionary<int, int> Motes, IReadOnlyDictionary<int, int> Fragments,
    IReadOnlyDictionary<int, int> ChestMotes, IReadOnlyDictionary<int, int> ChestFragments);

/// <summary>What this app version works out from the facts. Re-sent after an update that changes the math.</summary>
public sealed record CrawlDerived(double Levels, double KillLevels, double RewardLevels, double ActiveMinutes, double IdleMinutes);

/// <param name="Trio">Active trio for the hunt (nearest closed visit within 6 hours, <see cref="Analysis.PlayFacts.TrioNear"/>).
/// Null from older clients or when no visit is near; the community job then borrows from the same day's nearest crawl.</param>
/// <param name="Rank">The trio's reincarnation rank then (null with <paramref name="Trio"/>).</param>
/// <param name="Bonus">True when hunted during a bonus-XP event; null otherwise.</param>
public sealed record SharedStretch(string Id, string Day, int Seq, string? Zone, IReadOnlyList<CompactRow> Kills, StretchDerived Derived,
    string? Trio = null, int? Rank = null, bool? Bonus = null);

public sealed record StretchDerived(double Minutes, double Levels);

/// <param name="Kind">"class" or "race".</param>
public sealed record SharedReincarnation(string Id, string Day, int Seq, string Kind, string Name);

/// <summary>A relevel as this app version puts it together (the server can rebuild it from crawls, stretches and reincarnations).</summary>
public sealed record SharedRelevel(string Id, string Started, string Trio, string? Race, int Rank, int LevelFrom, int LevelTo, bool Finished,
    double Hours, double OpenWorldHours, int Deaths);

/// <summary>A row of numbers written on one line (a kill, a reward, a ding), so thousands of them stay small and readable.</summary>
[JsonConverter(typeof(CompactRowConverter))]
public readonly record struct CompactRow(string Json);

public sealed class CompactRowConverter : JsonConverter<CompactRow>
{
    public override CompactRow Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return new CompactRow(doc.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, CompactRow value, JsonSerializerOptions options) => writer.WriteRawValue(value.Json);
}

public static class ShareBuilder
{
    /// <summary>Schema version (3 = spells &amp; trio source; 4 = pauses &amp; end time so the community job computes active time from facts). Bumping re-sends every unit.</summary>
    public const int Schema = 4;
    public static readonly ShareColumns Columns = new(
        new("[\"t\",\"xp\",\"party\",\"secs\",\"level\"]"), new("[\"t\",\"share\",\"sizing\",\"estimated\",\"unknown\"]"), new("[\"t\",\"level\"]"));

    /// <summary>
    /// All play in the logs (first-time leveling, relevels, and cap farming), or only play from <paramref name="since"/> on
    /// when sharing new play only: closed crawls and instances, open-world stretches below the cap, reincarnations, and relevels. <paramref name="kills"/> and
    /// <paramref name="events"/> are the store's rows for that window (kills; dings and deaths).
    /// </summary>
    public static SharePayload Build(string appVersion, string gameServer, string characterKey, byte[] salt,
        IReadOnlyList<Visit> visits, IReadOnlyList<Journey> journeys, IReadOnlyList<KillRecord> kills, IReadOnlyList<LevelRecord> events,
        EngineState s, TrackerOptions o, DateTime? since = null, PlayFacts? facts = null)
    {
        string Code(string what) => Hash(salt, characterKey + "|" + what);
        bool After(DateTime t) => since == null || t >= since;
        var play = facts ?? PlayFacts.From(visits, s);
        var killsByVisit = kills.Where(k => k.VisitId != null).GroupBy(k => k.VisitId!).ToDictionary(g => g.Key, g => g.OrderBy(k => k.Time).ToList());
        var field = FieldHunts.From(kills.Where(k => k.VisitId == null && After(k.Time)), o.LevelCap);
        // Play during a bonus-XP event goes marked ("bonus": true), so the community job can leave it out: a bonus makes a place
        // look faster than it is. Only marked units carry the field, so nothing else changes on the wire.
        bool? Bonus(DateTime from, DateTime to) => play.DuringBonus(from, to) ? true : null;
        var openKills = kills.Where(k => k.VisitId == null).OrderBy(k => k.Time).ToList();

        // One order for everything, in place of clock times.
        // Dungeon Crawls, and any other instance with a kill in it ("kind": "crawl" or "instance").
        var crawlVisits = visits.Where(v => v.Closed && (v.Difficulty != null || v.Kills > 0) && !v.PlayedOnACapLoadout(o.LevelCap) && After(v.Start))
            .OrderBy(v => v.Start).ToList();
        var reincs = s.Roster.Classes.Values.SelectMany(i => i.Seen.Select(t => (Time: t, Kind: "class", i.Name)))
            .Concat(s.Roster.Races.Values.SelectMany(i => i.Seen.Select(t => (Time: t, Kind: "race", i.Name))))
            .Where(x => After(x.Time)).ToList();
        var order = crawlVisits.Select(v => (v.Start, Key: "v:" + v.Id))
            .Concat(field.Select(f => (f.Start, Key: $"f:{f.Start:O}")))
            .Concat(reincs.Select(r => (Start: r.Time, Key: $"r:{r.Kind}:{r.Name}:{r.Time:O}")))
            .OrderBy(x => x.Start).ThenBy(x => x.Key, StringComparer.Ordinal).DistinctBy(x => x.Key)
            .Select((x, i) => (x.Key, Seq: i + 1)).ToDictionary(x => x.Key, x => x.Seq);

        // The slot a trio came from never leaves the PC: a slot's trio is one from an earlier /who ("carried"), an unmet slot has none.
        static string SourceShared(string source) => source switch { "slot" => "carried", "unmet" => "none", _ => source };
        var crawls = new List<SharedCrawl>();
        foreach (var v in crawlVisits)
        {
            var st = VisitStats.From(v, v.EndTime, o);
            if (v.Kills == 0 && st.ActiveMinutes < 1) continue;
            var call = play.Trio(v.Id);
            var (trio, race) = (call.Trio, call.Race);
            int rank = play.RankAt(trio, v.Start);
            var end = v.EndTime.AddSeconds(5);
            int T(DateTime t) => (int)Math.Max(0, Math.Round((t - v.Start).TotalSeconds));
            var killRows = killsByVisit.GetValueOrDefault(v.Id)?.Select(k => KillRow(k, T(k.Time))).ToList() ?? [];
            var rewards = v.Rewards.OrderBy(r => r.Time)
                .Select(r => Row(T(r.Time), N(r.Share), B(r.Sizing), B(r.Estimated), B(r.Unknown))).ToList();
            var inside = events.Where(e => e.Time >= v.Start && e.Time <= end).ToList();
            var dings = inside.Where(e => e.Kind == "ding").Select(e => Row(T(e.Time), N(e.Level))).ToList();
            var deaths = inside.Where(e => e.Kind == "death").Select(e => T(e.Time)).ToList();
            var d = v.Damage;
            crawls.Add(new SharedCrawl(Code(v.Id), Day(v.Start), order["v:" + v.Id], v.Difficulty != null ? "crawl" : "instance", v.BaseZone,
                v.Difficulty, trio, v.TrioConfirmed, SourceShared(call.Source), race, rank,
                v.LevelStart, v.LevelEnd, Math.Round(v.ActiveSeconds), v.Completed, v.Refunded, v.ResetMinutes is { } rm ? R2(rm) : null,
                killRows, rewards, dings, deaths,
                new SharedDamage(d.Total, Math.Round(d.FightSeconds), d.Taken, new SortedDictionary<string, long>(d.BySource, StringComparer.Ordinal),
                    new SortedDictionary<string, long>(d.ByAbility, StringComparer.Ordinal)),
                new SortedDictionary<string, int>(v.Casts, StringComparer.Ordinal),
                new SharedLoot(Tiers(v.Loot.Motes), Tiers(v.Loot.Fragments), Tiers(v.Loot.ChestMotes), Tiers(v.Loot.ChestFragments)),
                new CrawlDerived(R3(st.LevelsGained), R3(st.KillLevels), R3(st.RewardLevels), R2(st.ActiveMinutes), R2(st.IdleMinutes)),
                v.Pauses.Select(p => new[] { T(p.From), T(p.To) }).ToList(), T(v.IdleEnd(v.EndTime)), Bonus(v.Start, v.EndTime)));
        }

        var stretches = new List<SharedStretch>();
        foreach (var f in field)
        {
            var mine = openKills.Where(k => k.Time >= f.Start && k.Time <= f.End && k.Zone == f.Zone && k.Level is { } l && l < o.LevelCap);
            // Who was hunting: the same trio and rank the app's own Ask gives this hunt (docs/community-job.md, the open-world gap).
            var trio = play.TrioNear(f.Start);
            stretches.Add(new SharedStretch(Code($"stretch|{f.Start:O}"), Day(f.Start), order[$"f:{f.Start:O}"], f.Zone,
                mine.Select(k => KillRow(k, (int)Math.Round((k.Time - f.Start).TotalSeconds))).ToList(), new StretchDerived(R2(f.Minutes), R3(f.Levels)),
                trio, trio != null ? play.RankAt(trio, f.Start) : null, Bonus(f.Start, f.End)));
        }

        var reincarnations = reincs
            .Select(r => new SharedReincarnation(Code($"reincarnation|{r.Kind}|{r.Name}|{r.Time:O}"), Day(r.Time),
                order[$"r:{r.Kind}:{r.Name}:{r.Time:O}"], r.Kind, r.Name))
            .DistinctBy(r => r.Seq).OrderBy(r => r.Seq).ToList();

        var relevels = journeys.Where(j => since == null || j.Reincarnated >= since.Value.AddMinutes(-1)).OrderBy(j => j.Start)
            .Select(j => new SharedRelevel(Code($"relevel|{j.TrioKey}|{j.Reincarnated:O}"), Day(j.Reincarnated), j.TrioKey, j.Race, j.Rank,
                j.LevelFrom, j.LevelNow, j.Done, R2(j.ActiveHours), R2(j.FieldHours), j.Deaths))
            .ToList();

        return new SharePayload(Schema, appVersion, EngineState.CurrentVersion, gameServer, Hash(salt, characterKey)[..12],
            since is { } sv ? Day(sv) : null, Columns, crawls, stretches, reincarnations, relevels);
    }

    /// <summary>A new random salt for a player's character codes and row IDs.</summary>
    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(16);

    private static CompactRow KillRow(KillRecord k, int t) => Row(t, k.XpUnknown ? "null" : N(k.Percent), B(k.Party), k.KillSeconds is { } ks ? N(Math.Round(ks, 1)) : "null", N(k.Level));

    private static CompactRow Row(params object[] parts) => new("[" + string.Join(",", parts.Select(p => p switch
    {
        int i => i.ToString(CultureInfo.InvariantCulture),
        _ => p.ToString(),
    })) + "]");

    private static string N(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
    private static string N(int? v) => v is { } x ? x.ToString(CultureInfo.InvariantCulture) : "null";
    private static string B(bool b) => b ? "1" : "0";

    private static string Hash(byte[] salt, string text) =>
        Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(text)))[..20].ToLowerInvariant();

    private static Dictionary<int, int> Tiers(int[] counts)
    {
        var d = new Dictionary<int, int>();
        for (int i = 1; i < counts.Length; i++) if (counts[i] > 0) d[i] = counts[i];
        return d;
    }

    private static string Day(DateTime t) => t.ToString("yyyy-MM-dd");
    private static double R2(double x) => Math.Round(x, 2);
    private static double R3(double x) => Math.Round(x, 3);
}
