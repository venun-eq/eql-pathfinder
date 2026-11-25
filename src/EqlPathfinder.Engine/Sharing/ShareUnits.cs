using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EqlPathfinder.Engine.Sharing;

/// <summary>
/// One shared unit as uploaded: the columns the server indexes (copies, for querying) and the whole unit as the app
/// built it, gzip-compressed JSON in base64 (<see cref="Facts"/>). <see cref="Hash"/> is the unit's content, so a unit is
/// sent again only when it changed: a crawl still open at the last upload, or a fix to how the log is read.
/// </summary>
public sealed record ShareUnit(string Kind, string Id, string Day, int? Seq, string? Zone, int? Difficulty, int? LevelStart, int? LevelEnd,
    string? Trio, string? Race, int Rank, object? Derived, string Facts)
{
    [JsonIgnore] public string Hash { get; init; } = "";
}

/// <summary>One upload: at most <see cref="ShareUnits.BatchSize"/> units of one character.</summary>
public sealed record ShareUpload(int Schema, string App, int Engine, string? Server, string Character, IReadOnlyList<ShareUnit> Rows);

public static class ShareUnits
{
    /// <summary>Units per upload. The server takes up to 200; 100 keeps each request small and quick.</summary>
    public const int BatchSize = 100;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Every unit in a payload, in the order it happened.</summary>
    public static List<ShareUnit> From(SharePayload p)
    {
        var units = new List<ShareUnit>(p.Crawls.Count + p.Stretches.Count + p.Reincarnations.Count + p.Relevels.Count);
        foreach (var c in p.Crawls)
            units.Add(Make(p, c, c.Kind, c.Id, c.Day, c.Seq, c.Zone, c.Difficulty, c.LevelStart, c.LevelEnd, c.Trio, c.Race, c.Rank, null));
        foreach (var s in p.Stretches)
            units.Add(Make(p, s, "stretch", s.Id, s.Day, s.Seq, s.Zone, null, null, null, s.Trio, null, s.Rank ?? 0, null));
        foreach (var r in p.Reincarnations)
            units.Add(Make(p, r, "reincarnation", r.Id, r.Day, r.Seq, null, null, null, null, r.Kind == "class" ? r.Name : null,
                r.Kind == "race" ? r.Name : null, 0, null));
        foreach (var j in p.Relevels)
            units.Add(Make(p, j, "relevel", j.Id, j.Started, null, null, null, j.LevelFrom, j.LevelTo, j.Trio, j.Race, j.Rank,
                new { j.Finished, j.Hours, j.OpenWorldHours, j.Deaths }));
        return units.OrderBy(u => u.Seq ?? int.MaxValue).ThenBy(u => u.Day, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The ids this character sent (<paramref name="sent"/>, the ledger) that the app no longer builds, to withdraw from the
    /// server (docs/specs/share-without-derived.md). None when nothing is built, or when the build holds fewer units than
    /// it lost: that looks like a read gone wrong, not play that moved (a re-read that moved every id is exactly half and goes).
    /// </summary>
    public static IReadOnlyList<string> Withdrawn(IReadOnlyDictionary<string, string> sent, IReadOnlyList<ShareUnit> built)
    {
        if (built.Count == 0) return [];
        var live = built.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var gone = sent.Keys.Where(id => !live.Contains(id)).Order(StringComparer.Ordinal).ToList();
        return gone.Count > built.Count ? [] : gone;
    }

    /// <summary>The units in batches for upload, each with the payload's header.</summary>
    public static IEnumerable<ShareUpload> Batches(SharePayload p, IReadOnlyList<ShareUnit> units)
    {
        for (int i = 0; i < units.Count; i += BatchSize)
            yield return new ShareUpload(p.Schema, p.AppVersion, p.Engine, p.GameServer, p.Character, units.Skip(i).Take(BatchSize).ToList());
    }

    /// <param name="facts">
    /// Content hash inputs: the facts plus the schema version. The community job computes every rate from facts
    /// (docs/community-job.md), so math-only updates never trigger history re-uploads. A change to the facts (a fix to how the log is
    /// read, a new fact like schema 4's pauses) changes the hash and goes up.
    /// </param>
    private static ShareUnit Make<T>(SharePayload p, T unit, string kind, string id, string day, int? seq, string? zone, int? difficulty,
        int? levelStart, int? levelEnd, string? trio, string? race, int rank, object? derived, object? facts = null)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(unit, Json);
        var factsJson = facts == null ? json : JsonSerializer.SerializeToUtf8Bytes(facts, facts.GetType(), Json);
        var hash = Convert.ToHexString(SHA256.HashData([.. factsJson, .. BitConverter.GetBytes(p.Schema)]))[..24].ToLowerInvariant();
        return new ShareUnit(kind, id, day, seq, zone, difficulty, levelStart, levelEnd, trio, race, rank, derived, Gzip(json)) { Hash = hash };
    }

    private static string Gzip(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(data);
        return Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);
    }

    /// <summary>A unit's facts back as JSON (the community job and tests), bounded to <paramref name="maxBytes"/> uncompressed bytes.</summary>
    public static string Unzip(string facts, int maxBytes = Community.CommunityJob.MaxFactsBytes)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        if (facts.Length > (Community.CommunityJob.MaxCompressedFactsBytes / 3 + 2) * 4)
            throw new InvalidDataException($"Compressed facts exceeded {Community.CommunityJob.MaxCompressedFactsBytes:N0} bytes.");
        return Community.CommunityJob.Gunzip(Convert.FromBase64String(facts), maxBytes);
    }
}

/// <summary>What sharing builds, and whether the build may be used to withdraw units (the read it came from was whole).</summary>
public sealed record ShareRead(SharePayload Payload, bool Settled);
