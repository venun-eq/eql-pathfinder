namespace EqlPathfinder.Engine.Pages;

/// <summary>
/// Navigation, message-origin, and external-link rules for the app's WebView2 windows.
/// Kept in the engine so unit tests can exercise every edge case without spinning up WebView2.
/// </summary>
public static class WebViewPolicy
{
    public const string AppHost = "pathfinder.local";

    public static string AppUrl(string expectedPage) => $"https://{AppHost}/{expectedPage.TrimStart('/')}";

    /// <summary>
    /// Only the window's own local page on https://pathfinder.local (exact host, default port, no userinfo,
    /// exact normalized path) may load in WebView2. Blocks arbitrary http/https sites, file://, javascript:, data:,
    /// edge://, and path traversal.
    /// </summary>
    public static bool IsAllowedAppNavigation(string? uri, string expectedPage)
    {
        if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(expectedPage)) return false;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;
        if (!string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(u.Host, AppHost, StringComparison.OrdinalIgnoreCase)) return false;
        if (!u.IsDefaultPort || !string.IsNullOrEmpty(u.UserInfo)) return false;
        if (uri.Contains("..", StringComparison.Ordinal)) return false;
        var expectedPath = "/" + expectedPage.TrimStart('/');
        return string.Equals(u.AbsolutePath, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Accepts WebMessageReceived only when it originates from the window's own local page.</summary>
    public static bool IsAllowedMessageOrigin(string? source, string expectedPage) =>
        IsAllowedAppNavigation(source, expectedPage);

    /// <summary>
    /// External URLs allowed to open in the user's default browser via ShellExecute if ever clicked from WebView2.
    /// Restricted to HTTPS on eqlpathfinder.com (and www.eqlpathfinder.com) with no userinfo and default port.
    /// </summary>
    public static bool TryOpenExternal(string? uri, Action<string> open)
    {
        if (!IsAllowedExternalUrl(uri)) return false;
        open(uri!);
        return true;
    }

    public static bool IsAllowedExternalUrl(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return false;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;
        if (!string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        if (!u.IsDefaultPort || !string.IsNullOrEmpty(u.UserInfo)) return false;
        return string.Equals(u.Host, "eqlpathfinder.com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.Host, "www.eqlpathfinder.com", StringComparison.OrdinalIgnoreCase);
    }
}
