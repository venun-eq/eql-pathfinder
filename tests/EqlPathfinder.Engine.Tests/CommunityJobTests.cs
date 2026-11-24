using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using EqlPathfinder.Engine.Community;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The job's questions to D1, against a fake D1 that answers from a config table and records each query.</summary>
public class CommunityJobTests
{
    private sealed class FakeD1(Dictionary<string, string> config, int youngPlayers, IReadOnlyList<object>? unitPages = null,
        Func<HttpRequestMessage, HttpResponseMessage?>? onRequest = null) : HttpMessageHandler
    {
        private int _unitCall;
        public List<(string Sql, JsonElement Params)> Queries { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (onRequest?.Invoke(request) is { } custom) return custom;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var sql = body.RootElement.GetProperty("sql").GetString()!;
            Queries.Add((sql, body.RootElement.GetProperty("params").Clone()));
            object rows = sql.Contains("FROM config") ? config.Select(kv => new { key = kv.Key, value = kv.Value }).ToArray()
                : sql.Contains("FROM players") ? new[] { new { n = youngPlayers } }
                : unitPages != null && _unitCall < unitPages.Count ? unitPages[_unitCall++]
                : Array.Empty<object>();
            var json = JsonSerializer.Serialize(new { success = true, result = new[] { new { results = rows, meta = new { rows_read = 1 } } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static string GzipHex(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(raw);
        return Convert.ToHexString(ms.ToArray());
    }

    private static string GzipHex(string json) => GzipHex(Encoding.UTF8.GetBytes(json));

    private static string ValidCrawlHex(string id, string zone = "Befallen", int level = 10)
    {
        var crawl = new SharedCrawl(id, "2026-11-14", 1, "crawl", zone, 4, "WIZ/ENC/CLR", true, "who", "Ogre", 0, level, level + 1,
            1800, true, false, null,
            Enumerable.Range(0, 20).Select(i => new CompactRow(FormattableString.Invariant($"[{i * 60},5,0,8,{level}]"))).ToList(),
            [], [], [],
            new SharedDamage(1000, 900, 0, new Dictionary<string, long>(), new Dictionary<string, long> { ["Fireball"] = 1000 }),
            new Dictionary<string, int>(),
            new SharedLoot(new Dictionary<int, int> { [5] = 4 }, new Dictionary<int, int>(), new Dictionary<int, int>(), new Dictionary<int, int>()),
            new CrawlDerived(1, 1, 0, 30, 0));
        return GzipHex(JsonSerializer.Serialize(crawl, CommunityBuilder.Json));
    }

    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The job is told its database (EQLP_D1, in the NAS's eqlp.env): the source names none, so a public copy
    /// of the code isn't tied to one account's database, and a job that wasn't told stops rather than guessing.</summary>
    [Fact]
    public void TheCommunityJobIsToldItsDatabaseAndHasNoneOfItsOwn()
    {
        string[] names = ["CF_ACCOUNT_ID", "CF_API_TOKEN", "EQLP_D1"];
        var before = names.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            Environment.SetEnvironmentVariable("CF_ACCOUNT_ID", "acct");
            Environment.SetEnvironmentVariable("CF_API_TOKEN", "token");
            Environment.SetEnvironmentVariable("EQLP_D1", "made-up-database");
            Assert.Equal("made-up-database", CommunityTarget.FromEnvironment()?.Database);
            Environment.SetEnvironmentVariable("EQLP_D1", null);
            Assert.Null(CommunityTarget.FromEnvironment());
        }
        finally
        {
            for (var i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], before[i]);
        }
    }

    private static async Task<(FakeD1 d1, CommunityJob job, CommunityFile file)> Run(Dictionary<string, string> config, int young = 0)
    {
        var d1 = new FakeD1(config, young);
        var job = new CommunityJob(new HttpClient(d1), new CommunityTarget("acct", "token", "made-up-database"));
        var file = await job.BuildAsync(Now);
        return (d1, job, file);
    }

    [Fact]
    public async Task New_players_count_only_after_the_configured_days()
    {
        var (d1, job, file) = await Run(new() { ["min_player_days"] = "3" }, young: 2);
        Assert.Equal(3, file.Rules.MinPlayerDays);
        var units = d1.Queries.Single(q => q.Sql.Contains("FROM units"));
        Assert.Contains("p.created <= ?2", units.Sql);
        long cutoff = new DateTimeOffset(Now).ToUnixTimeSeconds() - 3 * 86400;
        Assert.Equal(cutoff, units.Params[1].GetInt64());
        Assert.Equal(2, job.Stats.LeftOut["players registered under 3 days"]);
    }

    [Fact]
    public async Task Off_by_default_everyone_counts()
    {
        var (d1, job, file) = await Run(new());
        Assert.Equal(0, file.Rules.MinPlayerDays);
        Assert.DoesNotContain(d1.Queries, q => q.Sql.Contains("FROM players WHERE"));
        var units = d1.Queries.Single(q => q.Sql.Contains("FROM units"));
        Assert.True(units.Params[1].GetInt64() > new DateTimeOffset(Now).ToUnixTimeSeconds() + 365L * 86400 * 50);
        Assert.Empty(job.Stats.LeftOut);
    }

    [Fact]
    public async Task QuarantinedUnitsAreExcludedFromTheQuery()
    {
        var (d1, _, _) = await Run(new());
        var units = d1.Queries.Single(q => q.Sql.Contains("FROM units"));
        Assert.Contains("u.quarantined = 0", units.Sql);
    }

    [Fact]
    public async Task QueryBoundsCompressedFactsLengthAndPageSizeInD1()
    {
        var (d1, _, _) = await Run(new());
        var units = d1.Queries.Single(q => q.Sql.Contains("FROM units"));
        Assert.Contains("length(u.facts) <= ?3", units.Sql);
        Assert.Contains("LIMIT 500", units.Sql);
        Assert.Equal(98_304, CommunityJob.MaxCompressedFactsBytes);
        Assert.Equal(CommunityJob.MaxCompressedFactsBytes, units.Params[2].GetInt32());
    }

    [Fact]
    public void UnhexRejectsGzipBombsAndOversizedPayloads()
    {
        // Exactly at the limit succeeds; one byte over throws InvalidDataException.
        var atLimit = GzipHex(new string('a', 64));
        Assert.Equal(64, CommunityJob.Unhex(atLimit, maxBytes: 64).Length);
        Assert.Throws<InvalidDataException>(() => CommunityJob.Unhex(GzipHex(new string('a', 65)), maxBytes: 64));

        // A small compressed gzip bomb (5 MiB of zeroes compresses to ~5 KB) fails against the default 4 MiB cap.
        var bombHex = GzipHex(new byte[CommunityJob.MaxFactsBytes + 1024]);
        Assert.True(bombHex.Length < 100_000, "test payload should be a small compressed bomb");
        Assert.Throws<InvalidDataException>(() => CommunityJob.Unhex(bombHex));

        // ShareUnits.Unzip uses the same bounded decompression cap.
        var bombB64 = Convert.ToBase64String(Convert.FromHexString(bombHex));
        Assert.Throws<InvalidDataException>(() => ShareUnits.Unzip(bombB64));

        // Oversized compressed hex is rejected before decompression.
        var hugeHex = new string('0', (CommunityJob.MaxCompressedFactsBytes + 1) * 2);
        Assert.Throws<InvalidDataException>(() => CommunityJob.Unhex(hugeHex));

        Assert.Throws<ArgumentOutOfRangeException>(() => CommunityJob.Unhex(atLimit, maxBytes: 0));
    }

    [Fact]
    public async Task BadRowsAreSkippedAndValidRowsStillPublish()
    {
        // Two valid players (5 crawls total) mixed with hostile/corrupt rows in D1.
        var validA1 = ValidCrawlHex("a1");
        var validA2 = ValidCrawlHex("a2");
        var validA3 = ValidCrawlHex("a3");
        var validB1 = ValidCrawlHex("b1");
        var validB2 = ValidCrawlHex("b2");
        var bomb = GzipHex(new byte[CommunityJob.MaxFactsBytes + 1024]);
        var truncatedGzip = validA1[..28];   // valid gzip header cut off mid-stream

        var rows = new object[]
        {
            new { id = "01", player = "a", kind = "crawl", facts = validA1 },
            new { id = "02", player = "bad", kind = "crawl", facts = "not-valid-hex!!" },
            new { id = "03", player = "bad", kind = "crawl", facts = Convert.ToHexString(Encoding.UTF8.GetBytes("not gzip")) },
            new { id = "03b", player = "bad", kind = "crawl", facts = truncatedGzip },
            new { id = "04", player = "bad", kind = "crawl", facts = bomb },
            new { id = "05", player = "bad", kind = "crawl", facts = GzipHex("{not json") },
            new { id = "06", player = "bad", kind = "crawl", facts = GzipHex("null") },
            new { id = "07", player = "bad", kind = "crawl", facts = GzipHex("{}") },
            new { id = "08", player = "bad", kind = "stretch", facts = GzipHex("null") },
            new { id = "09", player = "a", kind = "crawl", facts = validA2 },
            new { id = "10", player = "a", kind = "crawl", facts = validA3 },
            new { id = "11", player = "b", kind = "crawl", facts = validB1 },
            new { id = "12", player = "b", kind = "crawl", facts = validB2 },
        };

        var d1 = new FakeD1(new(), 0, unitPages: [rows]);
        var job = new CommunityJob(new HttpClient(d1), new CommunityTarget("acct", "token", "made-up-database"));
        var file = await job.BuildAsync(Now);

        var befallen = Assert.Single(file.Places);
        Assert.Equal("Befallen D4", befallen.Place);
        Assert.Equal(2, befallen.Players[9]);
        Assert.Equal(5, befallen.Crawls[9]);
        Assert.Equal(1, job.Stats.LeftOut["facts too large"]);
        Assert.Equal(6, job.Stats.LeftOut["unreadable facts"]);
    }

    [Fact]
    public async Task AStalledD1PageStopsInsteadOfLoopingForever()
    {
        // A full page (500 rows) whose last row id doesn't advance past the previous page's cursor.
        var page1 = Enumerable.Range(1, 500).Select(i => (object)new { id = $"id-{i:D4}", player = "a", kind = "crawl", facts = GzipHex("{}") }).ToArray();
        var stalled = Enumerable.Range(1, 500).Select(i => (object)new { id = $"id-{i:D4}", player = "a", kind = "crawl", facts = GzipHex("{}") }).ToArray();
        var d1 = new FakeD1(new(), 0, unitPages: [page1, stalled]);
        var job = new CommunityJob(new HttpClient(d1), new CommunityTarget("acct", "token", "made-up-database"));

        await Assert.ThrowsAsync<InvalidDataException>(() => job.BuildAsync(Now));
    }

    [Fact]
    public async Task CloudflareErrorsNeverRevealAccountOrDatabaseIds()
    {
        const string secretAccount = "99887766554433221100aabbccddeeff"; // gitleaks:allow (a made-up id, so the public copy's secret scan passes)
        const string secretDb = "11111111-2222-3333-4444-555555555555";
        const string secretToken = "cf_secret_token_value";

        var d1Fail = new FakeD1(new(), 0, onRequest: _ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("""{"success":false,"errors":[{"message":"boom"}]}""", Encoding.UTF8, "application/json") });
        var job = new CommunityJob(new HttpClient(d1Fail), new CommunityTarget(secretAccount, secretToken, secretDb));

        var ex1 = await Assert.ThrowsAsync<HttpRequestException>(() => job.RulesAsync());
        Assert.DoesNotContain(secretAccount, ex1.Message);
        Assert.DoesNotContain(secretDb, ex1.Message);
        Assert.DoesNotContain(secretToken, ex1.Message);
        Assert.Contains("D1 query", ex1.Message);

        var ex2 = await Assert.ThrowsAsync<HttpRequestException>(() => job.UploadAsync(new CommunityFile(2, Now, 0, 0, 0, new CommunityRules(), [], [], [])));
        Assert.DoesNotContain(secretAccount, ex2.Message);
        Assert.DoesNotContain(secretDb, ex2.Message);
        Assert.DoesNotContain(secretToken, ex2.Message);
        Assert.Contains("R2 upload", ex2.Message);
    }
}
