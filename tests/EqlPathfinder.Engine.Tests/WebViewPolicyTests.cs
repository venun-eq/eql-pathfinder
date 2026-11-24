using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

public class WebViewPolicyTests
{
    [Theory]
    [InlineData("https://pathfinder.local/index.html", "index.html")]
    [InlineData("https://pathfinder.local/index.html#settings", "index.html")]
    [InlineData("https://PATHFINDER.LOCAL/index.html", "index.html")]
    [InlineData("https://pathfinder.local/welcome.html", "welcome.html")]
    public void AllowsExpectedLocalAppPage(string uri, string page)
    {
        Assert.True(WebViewPolicy.IsAllowedAppNavigation(uri, page));
        Assert.True(WebViewPolicy.IsAllowedMessageOrigin(uri, page));
    }

    [Theory]
    [InlineData(null, "index.html")]
    [InlineData("", "index.html")]
    [InlineData("http://pathfinder.local/index.html", "index.html")]
    [InlineData("https://pathfinder.local/welcome.html", "index.html")]
    [InlineData("https://pathfinder.local/other.html", "index.html")]
    [InlineData("https://pathfinder.local:8443/index.html", "index.html")]
    [InlineData("https://user@pathfinder.local/index.html", "index.html")]
    [InlineData("https://pathfinder.local.evil.example/index.html", "index.html")]
    [InlineData("https://evil.example/index.html", "index.html")]
    [InlineData("https://pathfinder.local/sub/../index.html", "index.html")]
    [InlineData("file:///C:/Windows/win.ini", "index.html")]
    [InlineData("javascript:alert(1)", "index.html")]
    [InlineData("data:text/html,<h1>hi</h1>", "index.html")]
    [InlineData("about:blank", "index.html")]
    [InlineData("edge://settings", "index.html")]
    public void BlocksUnexpectedNavigationsAndMessageOrigins(string? uri, string page)
    {
        Assert.False(WebViewPolicy.IsAllowedAppNavigation(uri, page));
        Assert.False(WebViewPolicy.IsAllowedMessageOrigin(uri, page));
    }

    [Theory]
    [InlineData("https://eqlpathfinder.com/")]
    [InlineData("https://eqlpathfinder.com/privacy.html")]
    [InlineData("https://www.eqlpathfinder.com/changelog.html")]
    public void AllowsProjectHttpsExternalLinks(string uri)
    {
        Assert.True(WebViewPolicy.IsAllowedExternalUrl(uri));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://eqlpathfinder.com/")]
    [InlineData("https://eqlpathfinder.com:8443/")]
    [InlineData("https://user@eqlpathfinder.com/")]
    [InlineData("https://eqlpathfinder.com.evil.example/")]
    [InlineData("https://example.com/")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-msdt:/id")]
    [InlineData("javascript:alert(1)")]
    public void BlocksDisallowedExternalUrls(string? uri)
    {
        Assert.False(WebViewPolicy.IsAllowedExternalUrl(uri));
        var opened = new List<string>();
        Assert.False(WebViewPolicy.TryOpenExternal(uri, opened.Add));
        Assert.Empty(opened);
    }
}
