using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using EqlPathfinder.Engine.Sharing;

namespace EqlPathfinder.App;

/// <summary>
/// Feedback and the roadmap, on the community server (docs/roadmap.md): what's coming with everyone's votes, a vote,
/// and a note as the player wrote it. No account: votes carry an anonymous random id from this copy's settings
/// (<see cref="AppSettings.VoterId"/>), feedback carries a name and the error log only when the player adds them. A
/// report from Report a problem also carries the page, the window's details and, if the player kept it, a screenshot of
/// the page with names, clock times and folders hidden (web/js/report.js); Your reports reads this copy's back by the same id.
/// </summary>
internal sealed class FeedbackClient(AppSettings settings, string version)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private Uri Api => new(Environment.GetEnvironmentVariable("EQLPATHFINDER_API") is { Length: > 0 } e ? e
        : settings.ShareApi is { Length: > 0 } s ? s : ShareProtocol.DefaultApi);

    private string Voter
    {
        get
        {
            if (settings.VoterId is not { Length: 32 } id)
            {
                settings.VoterId = id = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                settings.Save();
            }
            return id;
        }
    }

    /// <summary>{kind: "roadmap", items} for the page, or {kind: "roadmap", error} when the server can't be reached.</summary>
    public async Task<object> RoadmapAsync()
    {
        try { return Message(await Get($"/v1/roadmap?voter={Voter}")); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new { kind = "roadmap", error = "What's coming can't be loaded right now (no connection?)." };
        }
    }

    public async Task<object> VoteAsync(string item, bool on)
    {
        try { return Message(await Post("/v1/vote", new { item, voter = Voter, on })); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new { kind = "roadmap", error = "The vote didn't go through (no connection?). Try again in a minute." };
        }
    }

    /// <summary>A screenshot's most bytes, as the server takes it (server/src/feedback.ts MAX_SHOT).</summary>
    public const int MaxShot = 1_500_000;

    /// <summary>
    /// Sends what the player wrote; the error log's last 20,000 characters only when they ticked the box. From Report a
    /// problem (<paramref name="from"/> "report") it also carries the window's details and, if they kept it, the screenshot
    /// as the page made it (names, clock times and folders already hidden), uploaded after the report with its one-time token.
    /// The reply's <c>shot</c> says how the screenshot went: sent, limit (today's are used up), failed, or null for none.
    /// </summary>
    public async Task<object> SendAsync(string kind, string text, string? contact, bool withErrors, string? page,
        string from = "help", string? details = null, byte[]? shot = null)
    {
        string? errors = null;
        if (withErrors && File.Exists(AppSettings.ErrorLogPath))
        {
            var log = await File.ReadAllTextAsync(AppSettings.ErrorLogPath);
            errors = log.Length > 20_000 ? log[^20_000..] : log;
        }
        try
        {
            var sent = await Post("/v1/feedback", new
            {
                kind,
                text,
                contact = string.IsNullOrWhiteSpace(contact) ? null : contact,
                errors,
                page,
                details,
                shot = shot != null ? true : (bool?)null,
                app = version,
                voter = Voter,
            });
            string? shotResult = null;
            if (shot != null)
            {
                if (sent.TryGetProperty("upload", out var up) && up.GetString() is { Length: 32 } token
                    && sent.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var id))
                {
                    try { await Put($"/v1/feedback/{id}/shot", shot, token); shotResult = "sent"; }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    {
                        AppSettings.LogError("report screenshot upload", ex);
                        shotResult = "failed";
                    }
                }
                else shotResult = sent.TryGetProperty("shot", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() == "limit" ? "limit" : "failed";
            }
            return new { kind = "feedbackSent", from, ok = true, shot = shotResult };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new
            {
                kind = "feedbackSent",
                from,
                ok = false,
                error = ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
                ? "That's a lot at once: wait a minute and send again." : "It didn't send (no connection?). Your text is still here; try again."
            };
        }
    }

    /// <summary>Help → Your reports: what this copy sent, each with its status and any note back. {kind: "myReports", items} or {kind, error}.</summary>
    public async Task<object> MineAsync()
    {
        try { return new { kind = "myReports", items = (await Get($"/v1/feedback?voter={Voter}")).GetProperty("items") }; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            return new { kind = "myReports", error = "Your reports can't be loaded right now (no connection?)." };
        }
    }

    /// <summary>The first bytes of a PNG, JPEG or WebP: anything else the page hands over isn't sent as a screenshot.</summary>
    public static bool IsImage(byte[] b) =>
        b.Length is > 12 and <= MaxShot && (
            b.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            || (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            || (b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8)));

    // A screenshot can take longer than a note on a slow connection.
    private static readonly HttpClient Upload = new() { Timeout = TimeSpan.FromSeconds(90) };

    private async Task Put(string path, byte[] body, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, new Uri(Api, path)) { Content = new ByteArrayContent(body) };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        req.Headers.UserAgent.ParseAdd("EQLPathfinder/" + version);
        req.Headers.Add("x-eqlp-upload", token);
        using var res = await Upload.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }

    private static object Message(JsonElement roadmap) => new { kind = "roadmap", items = roadmap.GetProperty("items") };

    private async Task<JsonElement> Get(string path)
    {
        using var res = await Http.GetAsync(new Uri(Api, path));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> Post(string path, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(Api, path)) { Content = JsonContent.Create(body) };
        req.Headers.UserAgent.ParseAdd("EQLPathfinder/" + version);
        using var res = await Http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }
}
