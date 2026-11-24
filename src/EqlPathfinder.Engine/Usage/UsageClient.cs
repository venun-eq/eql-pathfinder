using System.Net.Http.Headers;
using System.Text;

namespace EqlPathfinder.Engine.Usage;

/// <summary>
/// Sends usage summaries to the community server (POST /v1/usage, one row per copy per day, replaced when sent again) and
/// deletes them (DELETE /v1/usage) when the player switches usage off. No key, no signature: the random id is the only link
/// between a copy's days, and the server keeps no address.
/// </summary>
public sealed class UsageClient(HttpClient http, Uri api, string version)
{
    public async Task SendAsync(UsagePayload payload, CancellationToken ct = default)
    {
        using var req = Request(HttpMethod.Post, UsageRecorder.ToJson(payload));
        using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    public async Task ForgetAsync(string id, CancellationToken ct = default)
    {
        using var req = Request(HttpMethod.Delete, "{\"id\":\"" + id + "\"}");
        using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage Request(HttpMethod method, string json)
    {
        var req = new HttpRequestMessage(method, new Uri(api, "/v1/usage"))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("EQLPathfinder", version));
        return req;
    }
}
