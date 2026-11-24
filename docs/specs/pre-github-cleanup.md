# Pre-GitHub cleanup: WebView2 lockdown, spec mockups, and check.ps1

**Why:** Before publishing the repository on GitHub, lock down the app's WebView2 host windows against unexpected
navigations, popups, permissions, and foreign `WebMessageReceived` origins; shrink ~16 MB of 2x-DPI mockup PNGs in
`docs/specs/` into crisp WebP; remove the stray duplicated `queue-board` block after `check: all green` in
`tools/check.ps1`; and update `CLAUDE.md`'s stale "no git remote" note.
**Players:** everyone. The app looks and behaves identically in normal use, with tighter browser sandboxing in Release
builds.

## Done when
- [x] `WebViewPolicy` validates allowed local app navigation (`https://pathfinder.local/<page>`), `WebMessageReceived` origin, and allowlisted external HTTPS links (`eqlpathfinder.com`), rejecting `http://`, `file://`, `javascript:`, `data:`, `edge://`, userinfo, non-default ports, and path traversal - `WebViewPolicyTests` (32 tests)
- [x] `WebViewSecurity.Configure` locks `CoreWebView2Settings` (context menus, status bar, zoom/pinch, swipe nav, script dialogs, built-in error page, autofill, password autosave, host objects, and DevTools + browser accelerator keys in Release), enforces `NavigationStarting`, `NewWindowRequested`, `PermissionRequested` (deny), and `WebMessageReceived` origin checks in both `MainWindow` and `WelcomeWindow`, and `MainWindow.Go` serializes script arguments via `JsonSerializer.Serialize` - `tools/check.ps1` (Release build)
- [x] All 34 mockup PNGs under `docs/specs/campaign-*/` are converted to max-1400px WebP and every markdown link in `docs/specs/campaign-*.md` points to `.webp` - `tools/check.ps1`
- [x] `tools/check.ps1` runs the 5-sample `queue-board` self-test in-place (before `-SamplesOnly` and `check: all green`) with the trailing duplicate removed, and `CLAUDE.md` reflects manual GitHub pushes - `tools/check.ps1`

## Decisions
- **Pure URI policy in `EqlPathfinder.Engine.Pages.WebViewPolicy`:** keeps all navigation/origin/external-URL rules unit-tested under `EqlPathfinder.Engine.Tests` without needing a live WebView2 runtime, while `EqlPathfinder.App.WebViewSecurity` applies them uniformly to both `MainWindow` and `WelcomeWindow`.
- **Keep `Ctrl+Shift+R` and `navigator.clipboard.writeText` working:** `Ctrl+Shift+R` is intercepted in WPF `PreviewKeyDown` before WebView2, and `writeText` on user gesture does not require `ClipboardRead` permission.
- **Keep spec mockups as WebP rather than deleting them:** retains full design-decision context in `docs/specs/` at ~1/6 the repository footprint.

## Where
- `src/EqlPathfinder.Engine/Pages/WebViewPolicy.cs`, `tests/EqlPathfinder.Engine.Tests/WebViewPolicyTests.cs`
- `src/EqlPathfinder.App/WebViewSecurity.cs`, `src/EqlPathfinder.App/MainWindow.xaml.cs`, `src/EqlPathfinder.App/WelcomeWindow.xaml.cs`
- `docs/specs/campaign-*/*.webp`, `docs/specs/campaign-*.md`
- `tools/check.ps1`, `CLAUDE.md`, `docs/releases/next/pre-github-cleanup.md`

## Reviews
- architect: checked `WebViewPolicy` URI parsing (scheme, host, default port, userinfo, `..` traversal, exact page path), `WebViewSecurity` settings and event handlers in both `MainWindow` and `WelcomeWindow` (including `#if !DEBUG` DevTools/accelerator keys, `NavigationStarting` cancel, `NewWindowRequested` handled, `PermissionRequested` deny, and `WebMessageReceived` origin guard), `MainWindow.Go` JSON argument encoding, and `tools/check.ps1` 5-sample `queue-board` placement.
- designer: not needed, no player-facing visual or copy changes; `WebViewSecurity` preserves normal UI interaction, shortcuts (`Ctrl+Shift+R`, copy/paste), and appearance identically.
