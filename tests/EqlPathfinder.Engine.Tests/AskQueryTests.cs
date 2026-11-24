using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

public class AskQueryTests
{
    /// <summary>
    /// The options the window reads the page's question with: the engine's own, the ones MainWindow.SendAsk uses. Until
    /// 11-17 this test kept a copy, and the copy had already drifted from the window's (docs/debt.md).
    /// </summary>
    private static readonly JsonSerializerOptions Page = PageJson.Query;

    [Fact]
    public void EveryFieldThePagePostsReachesTheQuery()
    {
        // As web/js/ask.js posts it ({type: "ask", ...askState}); 0.9.2 copied fields by hand and dropped where and with.
        var q = JsonSerializer.Deserialize<AskQuery>("""
            {"type":"ask","q":"level","from":11,"to":"34","goal":"motes","at":20,"scope":"all","diff":"Kedge Keep|41","where":"open","with":"group"}
            """, Page)!;
        Assert.Equal(new AskQuery("level", 11, 34, "motes", 20, "all", "Kedge Keep|41", "open", "group"), q);
        Assert.Equal(new AskQuery(null, null, null, null, null, null, null), JsonSerializer.Deserialize<AskQuery>("""{"type":"ask"}""", Page));
    }
}
