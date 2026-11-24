using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EqlPathfinder.Engine.Community;

/// <summary>Where the job reads and writes. The token needs D1 Read and Workers R2 Storage Write on the account.
/// The database is always told (EQLP_D1, in the NAS's eqlp.env): the source names none, so a public copy of it points at nobody's.</summary>
public sealed record CommunityTarget(string AccountId, string Token, string Database,
    string Bucket = CommunityTarget.DefaultBucket, string Key = CommunityTarget.DefaultKey)
{
    public const string DefaultBucket = "eql-pathfinder-updates";
    public const string DefaultKey = "community/v1.json";

    /// <summary>From CF_ACCOUNT_ID, CF_API_TOKEN and EQLP_D1, and null without all three (EQLP_BUCKET and EQLP_COMMUNITY_KEY override the defaults).</summary>
    public static CommunityTarget? FromEnvironment()
    {
        string? E(string k) => Environment.GetEnvironmentVariable(k) is { Length: > 0 } v ? v.Trim() : null;
        if (E("CF_ACCOUNT_ID") is not { } account || E("CF_API_TOKEN") is not { } token || E("EQLP_D1") is not { } database) return null;
        return new CommunityTarget(account, token, database, E("EQLP_BUCKET") ?? DefaultBucket,
            E("EQLP_COMMUNITY_KEY") ?? DefaultKey);
    }
}

/// <summary>
/// The community job (every 30 minutes on the NAS): every shared crawl from D1 (players and installs not flagged), the community file built
/// from them, and the file uploaded to the update bucket, which serves it at the address in the server's config.
/// </summary>
public sealed class CommunityJob(HttpClient http, CommunityTarget target)
{
    private const int Page = 500;

    /// <summary>Maximum compressed bytes accepted for one unit's facts blob: 96 KiB, what the Worker's 128 KiB of
    /// base64 (MAX_FACTS) decodes to. D1 skips larger rows in SQL.</summary>
    public const int MaxCompressedFactsBytes = 96 * 1024;

    /// <summary>Maximum uncompressed JSON bytes accepted for one unit's facts blob (4 MiB; protects the job from gzip bombs).</summary>
    public const int MaxFactsBytes = 4 * 1024 * 1024;

    private static InvalidDataException TooLarge(string message)
    {
        var ex = new InvalidDataException(message);
        ex.Data["TooLarge"] = true;
        return ex;
    }

    /// <summary>Database rows D1 read for this job so far (the free plan allows 5 million a day): the job's log line says it.</summary>
    public long RowsRead { get; private set; }

    /// <summary>Why play was left out and how much the file shows, filled in by <see cref="BuildAsync"/>.</summary>
    public CommunityStats Stats { get; } = new();

    private string Account => $"https://api.cloudflare.com/client/v4/accounts/{target.AccountId}/";

    public async Task<CommunityFile> BuildAsync(DateTime now, CancellationToken ct = default)
    {
        var rules = await RulesAsync(ct);
        var crawls = new List<PlayerCrawl>();
        var stretches = new List<PlayerStretch>();
        // Players registered too recently don't count yet (rules.MinPlayerDays; 0 = everyone).
        long registeredBy = rules.MinPlayerDays > 0
            ? new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds() - rules.MinPlayerDays * 86400L : 4102444800L;   // 2100: everyone
        if (rules.MinPlayerDays > 0)
        {
            var young = await QueryAsync("SELECT COUNT(*) AS n FROM players WHERE flagged = 0 AND created > ?1", ct, registeredBy);
            Stats.Count($"players registered under {rules.MinPlayerDays} days", young.Count > 0 ? young[0].GetProperty("n").GetInt32() : 0);
        }
        // Each page starts after the last row of the one before (not OFFSET, which D1 counts as reading every skipped row
        // again: a run would read about n² / 1000 rows, past the free plan's 5 million a day at around 50 players).
        for (string after = ""; ;)
        {
            var rows = await QueryAsync($"""
                SELECT u.id, u.player, u.kind, hex(u.facts) AS facts FROM units u
                JOIN players p ON p.id = u.player JOIN installs i ON i.id = u.install
                WHERE u.kind IN ('crawl', 'instance', 'stretch') AND p.flagged = 0 AND i.flagged = 0 AND u.quarantined = 0
                  AND u.facts IS NOT NULL AND length(u.facts) <= ?3 AND u.schema >= 2
                  AND p.created <= ?2 AND u.id > ?1
                ORDER BY u.id LIMIT {Page}
                """, ct, after, registeredBy, MaxCompressedFactsBytes);
            if (rows.Count > 0)
            {
                var next = rows[^1].TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(next) || string.CompareOrdinal(next, after) <= 0)
                    throw new InvalidDataException("D1 unit pagination did not advance.");
                after = next;
            }
            foreach (var r in rows)
            {
                try
                {
                    var player = r.TryGetProperty("player", out var pEl) ? pEl.GetString() : null;
                    var kind = r.TryGetProperty("kind", out var kEl) ? kEl.GetString() : null;
                    var hex = r.TryGetProperty("facts", out var fEl) ? fEl.GetString() : null;
                    if (string.IsNullOrEmpty(player) || string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(hex))
                    {
                        Stats.Count("unreadable facts");
                        continue;
                    }
                    var facts = Unhex(hex);
                    if (kind == "stretch")
                    {
                        if (CommunityBuilder.ReadStretch(facts) is { } stretch) stretches.Add(new PlayerStretch(player, stretch));
                        else Stats.Count("unreadable facts");
                    }
                    else if (CommunityBuilder.ReadCrawl(facts) is { } crawl) crawls.Add(new PlayerCrawl(player, crawl));
                    else Stats.Count("unreadable facts");
                }
                catch (InvalidDataException ex) when (ex.Data.Contains("TooLarge"))
                {
                    Stats.Count("facts too large");
                }
                catch (Exception ex) when (ex is FormatException or InvalidDataException or EndOfStreamException or JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    Stats.Count("unreadable facts");
                }
            }
            if (rows.Count < Page) break;
        }
        return CommunityBuilder.Build(crawls, stretches, rules, now, Stats);
    }

    /// <summary>The rules from the server's config table (min_players, min_crawls, max_share, min_player_days), defaults otherwise.</summary>
    public async Task<CommunityRules> RulesAsync(CancellationToken ct = default)
    {
        var d = new CommunityRules();
        var rows = await QueryAsync("SELECT key, value FROM config WHERE key IN ('min_players', 'min_crawls', 'max_share', 'min_player_days')", ct);
        var c = rows.ToDictionary(r => r.GetProperty("key").GetString() ?? "", r => r.GetProperty("value").GetString() ?? "");
        int I(string k, int def, int max) =>
            c.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 && n <= max ? n : def;
        double D(string k, double def) =>
            c.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) && x > 0 && x <= 1 ? x : def;
        return new CommunityRules(I("min_players", d.MinPlayers, 1000), I("min_crawls", d.MinCrawls, 100_000), D("max_share", d.MaxShare),
            I("min_player_days", d.MinPlayerDays, 3650));
    }

    public async Task UploadAsync(CommunityFile file, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(file, CommunityBuilder.Json);
        using var req = new HttpRequestMessage(HttpMethod.Put,
            $"{Account}r2/buckets/{target.Bucket}/objects/{Uri.EscapeDataString(target.Key)}")
        { Content = new ByteArrayContent(body) };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        await SendAsync(req, "R2 upload", ct);
    }

    private async Task<List<JsonElement>> QueryAsync(string sql, CancellationToken ct, params object[] args)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Account}d1/database/{target.Database}/query")
        {
            Content = JsonContent.Create(new { sql, @params = args }),
        };
        using var doc = await SendAsync(req, "D1 query", ct);
        var result = doc.RootElement.GetProperty("result")[0];
        if (result.TryGetProperty("meta", out var meta) && meta.TryGetProperty("rows_read", out var read) && read.TryGetInt64(out var n)) RowsRead += n;
        return result.GetProperty("results").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage req, string operation, CancellationToken ct)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.Token);
        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        JsonDocument? doc = null;
        try { doc = JsonDocument.Parse(text); } catch (JsonException) { }
        if (!res.IsSuccessStatusCode || doc == null || !doc.RootElement.TryGetProperty("success", out var ok) || !ok.GetBoolean())
        {
            doc?.Dispose();
            throw new HttpRequestException($"Cloudflare {operation}: {(int)res.StatusCode} {Trim(Redact(text))}");
        }
        return doc;
    }

    private string Redact(string s)
    {
        foreach (var secret in new[] { target.Token, target.AccountId, target.Database })
            if (!string.IsNullOrEmpty(secret)) s = s.Replace(secret, "[redacted]", StringComparison.Ordinal);
        return s;
    }

    private static string Trim(string s) => s.Length > 400 ? s[..400] + "…" : s;

    /// <summary>D1's hex(facts) back to the unit's JSON (gzip JSON), bounded to <paramref name="maxBytes"/> uncompressed bytes.</summary>
    public static string Unhex(string hex, int maxBytes = MaxFactsBytes)
    {
        ArgumentNullException.ThrowIfNull(hex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        if (hex.Length > MaxCompressedFactsBytes * 2)
            throw TooLarge($"Compressed facts exceeded {MaxCompressedFactsBytes:N0} bytes.");
        return Gunzip(Convert.FromHexString(hex), maxBytes);
    }

    /// <summary>Decompresses gzip UTF-8 bytes up to <paramref name="maxBytes"/>, throwing <see cref="InvalidDataException"/> if larger.</summary>
    internal static string Gunzip(byte[] compressed, int maxBytes = MaxFactsBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        if (compressed.Length > MaxCompressedFactsBytes)
            throw TooLarge($"Compressed facts exceeded {MaxCompressedFactsBytes:N0} bytes.");
        using var gz = new GZipStream(new MemoryStream(compressed, writable: false), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int total = 0;
        while (true)
        {
            int remaining = maxBytes - total;
            int toRead = remaining > 0 ? Math.Min(buffer.Length, remaining) : 1;
            int n = gz.Read(buffer, 0, toRead);
            if (n == 0) break;
            if (remaining == 0)
                throw TooLarge($"Uncompressed facts exceeded {maxBytes:N0} bytes.");
            total += n;
            output.Write(buffer, 0, n);
        }
        output.Position = 0;
        using var r = new StreamReader(output, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return r.ReadToEnd();
    }
}
