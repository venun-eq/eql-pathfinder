using System.Diagnostics;
using System.IO;
using EqlPathfinder.Engine.Pages;
using Microsoft.Web.WebView2.Core;

namespace EqlPathfinder.App;

/// <summary>
/// Shared WebView2 lockdown applied to every user-facing WebView2 window (MainWindow and WelcomeWindow):
/// locks CoreWebView2Settings, pins navigation and WebMessageReceived to https://pathfinder.local/&lt;page&gt;,
/// blocks popups, denies browser permissions, and only hands allowlisted HTTPS links to the default browser.
/// </summary>
internal static class WebViewSecurity
{
    public static void Configure(CoreWebView2 core, string expectedPage, Action<string> onMessage)
    {
        var s = core.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsPinchZoomEnabled = false;
        s.IsSwipeNavigationEnabled = false;
        s.AreDefaultScriptDialogsEnabled = false;
        s.IsBuiltInErrorPageEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.AreHostObjectsAllowed = false;
#if !DEBUG
        s.AreDevToolsEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
#endif

        core.SetVirtualHostNameToFolderMapping(
            WebViewPolicy.AppHost,
            Path.Combine(AppContext.BaseDirectory, "web"),
            CoreWebView2HostResourceAccessKind.DenyCors);

        core.NavigationStarting += (_, e) =>
        {
            if (WebViewPolicy.IsAllowedAppNavigation(e.Uri, expectedPage)) return;
            e.Cancel = true;
            if (e.IsUserInitiated && WebViewPolicy.IsAllowedExternalUrl(e.Uri))
                OpenExternal(e.Uri);
        };

        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (e.IsUserInitiated && WebViewPolicy.IsAllowedExternalUrl(e.Uri))
                OpenExternal(e.Uri);
        };

        core.PermissionRequested += (_, e) =>
        {
            e.State = CoreWebView2PermissionState.Deny;
            e.Handled = true;
        };

        core.WebMessageReceived += (_, e) =>
        {
            if (!WebViewPolicy.IsAllowedMessageOrigin(e.Source, expectedPage)) return;
            onMessage(e.WebMessageAsJson);
        };
    }

    internal static void OpenExternal(string uri)
    {
        try { WebViewPolicy.TryOpenExternal(uri, u => Process.Start(new ProcessStartInfo(u) { UseShellExecute = true })); }
        catch (Exception ex) { AppSettings.LogError("open external url", ex); }
    }
}
