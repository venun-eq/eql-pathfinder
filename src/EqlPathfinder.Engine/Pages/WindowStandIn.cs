namespace EqlPathfinder.Engine.Pages;

/// <summary>
/// A stand-in for the window (<c>chrome.webview</c>) where a page is opened without one: the page tests' headless Edge
/// and the <c>--snapshot</c> preview in Chrome. One text for both, so the tests prove on every run what the preview uses.
/// It lives in the engine because that is the one project the app and the tests both reference.
/// </summary>
public static class WindowStandIn
{
    /// <summary>
    /// The script that goes in before core.js looks for the window. It steps in only where there is no window, so in
    /// WebView2 it does nothing.
    ///
    /// Implemented as a real EventTarget using <c>dispatchEvent(new MessageEvent("message", { data }))</c> so a throwing
    /// listener does not block subsequent listeners and surfaces on <c>window</c>'s <c>error</c> event, matching WebView2.
    ///
    /// <c>postMessage</c> keeps a copy of each thing the page posts, through JSON as the window gets it, in order, in
    /// <c>chrome.webview.posted</c>. That list is also how a sender knows the stand-in stepped in: the real
    /// <c>chrome.webview</c> has none, and there nobody but the window sends.
    ///
    /// It declares no global name (its one name is inside the block): the page's scripts share one global scope.
    /// </summary>
    public const string Script = """
        if (!window.chrome?.webview) {
          const standIn = new EventTarget();
          standIn.posted = [];
          standIn.postMessage = message => { standIn.posted.push(JSON.parse(JSON.stringify(message))); };
          window.chrome = window.chrome || {};
          window.chrome.webview = standIn;
        }
        """;
}
