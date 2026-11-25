using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EqlPathfinder.Engine.Sharing;

/// <summary>
/// How a request proves which install sent it (server/src/auth.ts checks the same thing): the install's ECDSA P-256 key
/// signs "EQLP1\n{METHOD}\n{path}\n{unix time}\n{install}\n{hex SHA-256 of the body}" (IEEE P1363, r||s).
/// </summary>
public static class ShareProtocol
{
    public const string Prefix = "EQLP1";
    /// <summary>The community API (server/), behind api.eqlpathfinder.com's edge rate limit.</summary>
    public const string DefaultApi = "https://api.eqlpathfinder.com";
    /// <summary>The community numbers (rebuilt every 30 minutes) from everyone who shares (also /v1/config "community"), on the update bucket's address.</summary>
    public const string DefaultCommunity = "https://updates.eqlpathfinder.com/community/v1.json";

    /// <summary>The install ID: the first 32 hex characters of SHA-256 over the public key (SPKI).</summary>
    public static string InstallId(byte[] spki) => Hex(SHA256.HashData(spki))[..32];

    public static string Canonical(string method, string path, long time, string install, ReadOnlySpan<byte> body) =>
        string.Join('\n', Prefix, method, path, time.ToString(CultureInfo.InvariantCulture), install, Hex(SHA256.HashData(body)));

    public static ECDsa NewKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>A signature as the server checks it (ECDsa's default format here is IEEE P1363).</summary>
    public static byte[] Sign(ECDsa key, string canonical) =>
        key.SignData(Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    internal static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
}

public sealed record ShareLimits(int RowsPerUpload, int RowsPerDay);
public sealed record ShareConfig(bool RequireEnrollment, string? MinApp, string? Community, ShareLimits? Limits);
public sealed record ShareRegistration(string Install, string Player, string? Salt, bool Existing);
public sealed record ShareUploadResult(int Accepted, int Ignored);
public sealed record ShareMe(string Player, string Install, Dictionary<string, int> Counts, long? LastUpload, int Pcs);
public sealed record ShareDeleted(int Deleted, int Pcs);
public sealed record ShareWithdrawn(int Withdrawn);
public sealed record ShareCode(string Code, long Expires);
public sealed record ShareClaim(string Player, string? Salt, int Dropped);

/// <summary>An answer from the API other than success: <see cref="Code"/> is the API's reason ("daily_limit", "bad_code", ...).</summary>
public sealed class ShareApiException(int status, string code, string message, int? retryAfter = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    /// <summary>Seconds the server asks to wait (daily limit, too many code attempts).</summary>
    public int? RetryAfter { get; } = retryAfter;
    /// <summary>The network failed or the server didn't answer: try again later.</summary>
    public bool Transient => Status is 0 or >= 500 or 408;
}

/// <summary>The community API's client. Keeps no state beyond the key: the app decides what to send and when.</summary>
public sealed class ShareClient(HttpClient http, Uri api, ECDsa key)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly byte[] _publicKey = key.ExportSubjectPublicKeyInfo();

    public string InstallId => ShareProtocol.InstallId(_publicKey);
    public string PublicKey => Convert.ToBase64String(_publicKey);

    public Task<ShareConfig> ConfigAsync(CancellationToken ct = default) => Send<ShareConfig>(HttpMethod.Get, "/v1/config", null, ct, sign: false);

    /// <summary>Registers this install. With a link code it joins that player and gets the player's salt back.</summary>
    public Task<ShareRegistration> RegisterAsync(string app, string? enroll = null, string? link = null, CancellationToken ct = default) =>
        Send<ShareRegistration>(HttpMethod.Post, "/v1/register", new { publicKey = PublicKey, app, enroll, link }, ct);

    public Task<ShareUploadResult> UploadAsync(ShareUpload batch, CancellationToken ct = default) =>
        Send<ShareUploadResult>(HttpMethod.Post, "/v1/summaries", batch, ct);

    public Task<ShareMe> MeAsync(CancellationToken ct = default) => Send<ShareMe>(HttpMethod.Get, "/v1/me", null, ct);

    /// <summary>Deletes the named units of one character that this install sent last (up to 200 ids): what its app no longer builds.</summary>
    public Task<ShareWithdrawn> WithdrawAsync(string character, IReadOnlyList<string> ids, CancellationToken ct = default) =>
        Send<ShareWithdrawn>(HttpMethod.Post, "/v1/units/withdraw", new { character, ids }, ct);

    /// <summary>Deletes everything this player shared, on every linked PC, and forgets the installs.</summary>
    public Task<ShareDeleted> DeleteEverythingAsync(CancellationToken ct = default) => Send<ShareDeleted>(HttpMethod.Delete, "/v1/me", null, ct);

    /// <summary>A one-time code (10 minutes) for another PC to join this player; it carries the salt.</summary>
    public Task<ShareCode> LinkAsync(string salt, CancellationToken ct = default) => Send<ShareCode>(HttpMethod.Post, "/v1/link", new { salt }, ct);

    /// <summary>This (already sharing) install joins the player that made the code. Its old rows are dropped on the server.</summary>
    public Task<ShareClaim> ClaimAsync(string code, CancellationToken ct = default) =>
        Send<ShareClaim>(HttpMethod.Post, "/v1/link/claim", new { code }, ct);

    /// <summary>An enrollment code (24 hours, 1 use) for a friend, while the server requires one.</summary>
    public Task<ShareCode> EnrollAsync(CancellationToken ct = default) => Send<ShareCode>(HttpMethod.Post, "/v1/enroll", new { }, ct);

    private async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct, bool sign = true)
    {
        var bytes = body == null ? [] : JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), Json);
        using var req = new HttpRequestMessage(method, new Uri(api, path));
        if (body != null)
        {
            req.Content = new ByteArrayContent(bytes);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }
        if (sign)
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var canonical = ShareProtocol.Canonical(method.Method, path, time, InstallId, bytes);
            req.Headers.Add("X-EQLP-Install", InstallId);
            req.Headers.Add("X-EQLP-Time", time.ToString(CultureInfo.InvariantCulture));
            req.Headers.Add("X-EQLP-Signature", Convert.ToBase64String(ShareProtocol.Sign(key, canonical)));
        }
        HttpResponseMessage res;
        try { res = await http.SendAsync(req, ct).ConfigureAwait(false); }
        catch (HttpRequestException ex) { throw new ShareApiException(0, "network", "Couldn't reach the sharing server: " + ex.Message); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ShareApiException(408, "timeout", "The sharing server didn't answer in time."); }
        using (res)
        {
            var text = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
                return JsonSerializer.Deserialize<T>(text, Json) ?? throw new ShareApiException((int)res.StatusCode, "empty", "The server sent an empty answer.");
            string code = "http_" + (int)res.StatusCode, message = $"The sharing server answered {(int)res.StatusCode} {res.ReasonPhrase}.";
            int? retry = res.Headers.RetryAfter?.Delta is { } d ? (int)d.TotalSeconds : null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var e) && e.GetString() is { } c) code = c;
                if (root.TryGetProperty("message", out var m) && m.GetString() is { } msg) message = msg;
                if (root.TryGetProperty("retryAfter", out var r) && r.TryGetInt32(out var ra)) retry = ra;
            }
            catch (JsonException) { /* not JSON (Cloudflare's own error page): keep the HTTP status */ }
            throw new ShareApiException(res.StatusCode == HttpStatusCode.OK ? 0 : (int)res.StatusCode, code, message, retry);
        }
    }
}
