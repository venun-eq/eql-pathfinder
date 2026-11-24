using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The app's real page (src/EqlPathfinder.App/web: index.html, app.css and every script, as they are), opened in
/// headless Microsoft Edge with a stand-in for the window, taken through a test's steps (the window sends a message,
/// the player clicks or types), and read back: what a player could read, what the page posted to the window, and any
/// error.
///
/// A run is a batch (docs/specs/page-test-harness.md): every step is decided before the browser starts, the page
/// goes through them by itself and is read where a step says and after the last, and no test talks to a live page
/// (docs/specs/page-tests-other-pages.md has why a run is read more than once: one start of the browser then covers
/// a page's states and its click). Three parts, kept apart so that starting the browser once for a whole run can
/// replace the middle one later: <see cref="ComposedPage"/> puts the page together, <see cref="HeadlessEdge"/> runs a
/// browser on it, and <see cref="PageReading"/> turns what came back into a <see cref="PageRun"/> or a
/// <see cref="PageFailed"/>. Each failure starts with whose it is: the browser's, the page's, the test's or the
/// harness's own.
/// </summary>
internal sealed class PageHarness
{
    /// <summary>The page's files. A test of the harness itself passes a copy with a slip in it. Never written to.</summary>
    public string Web { get; init; } = SourceWeb();

    /// <summary>Where Edge is looked for, in order. Empty means the places Edge installs itself.</summary>
    public IReadOnlyList<string> Browsers { get; init; } = [];

    /// <summary>
    /// How long one page may take before its browser, and everything it started, is stopped and the test fails. A fuse:
    /// the slowest of 16 starts at once took 3 seconds. A browser that hung with no renderer left is tried once more
    /// (<see cref="Retries"/>: logged, red past 2 a run); anything else that failed isn't tried again.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How many browsers run at once on this PC. A test of the harness with a stand-in browser gives its own, so it never waits behind real ones.</summary>
    internal EdgeSlots Slots { get; init; } = EdgeSlots.Shared;

    /// <summary>The retries this run may use; a test of the harness gives its own so the real run's are not spent.</summary>
    internal PageRetries Retries { get; init; } = PageRetries.Shared;

    /// <summary>What lists the processes still running with a profile. Null: Windows is asked, and only for Edge itself (a stand-in has no renderer to look for and is never retried).</summary>
    internal Func<string, List<string>>? Alive { get; init; }

    /// <summary>Where a run keeps its copy of the page and the browser's profile. Both are gone when <see cref="Run"/> returns or throws.</summary>
    public string Scratch { get; init; } = Path.GetTempPath();

    /// <summary>
    /// Opens <paramref name="page"/> (a name from core.js PAGES) and sends each message in turn, as JSON, the way the
    /// window does: the run whose every step is a message.
    /// </summary>
    public PageRun Open(string page, params string[] messages) => Run(page, messages.Select(PageStep.Send).ToArray());

    /// <summary>
    /// Opens <paramref name="page"/> (a name from core.js PAGES) and does each step in order, as the window would and
    /// as a player would, giving the page its own short timers after each (Upgrades posts 120 ms after a change). The
    /// page is read at each <see cref="PageStep.Read"/> and after the last step. Throws <see cref="PageFailed"/> when
    /// Edge is missing, hangs or gives nothing back, when a script throws or doesn't load (while the page loads, as it
    /// takes a message or in a click's handler), when the page never takes the page data it was sent, and when a step
    /// can't be done: a click on nothing the page shows is a slip in the test, and the run ends there.
    /// </summary>
    public PageRun Run(string page, params PageStep[] steps)
    {
        var browser = HeadlessEdge.Find(Browsers);
        // A folder of this run's own, never Edge's: two runs at once, two worktrees checking at once and the user's
        // open Edge don't meet.
        var run = Path.Combine(Scratch, "pf-pagerun-" + Guid.NewGuid().ToString("N"));
        PageRun? read = null;
        Exception? failed = null;
        try
        {
            var file = ComposedPage.Write(Web, Path.Combine(run, "page"), page, steps);
            read = PageReading.From(page, browser, HeadlessEdge.Dump(browser, file, Path.Combine(run, "profile"), ComposedPage.Takes(steps), Timeout, Slots, Retries, Alive, page));
        }
        catch (Exception e) { failed = e; }

        // A failed run tidies up as a good one does, and tidying fails closed: Edge holds files in its profile for as
        // long as any part of it runs, so a folder that can't be removed is the only sign that something is left.
        if (Remove(run) is { } why)
            throw new PageFailed($"The browser: part of it is still running, or something else holds this run's folder, which couldn't be removed: {run} ({why})"
                + (failed == null ? "" : "\nBefore that, the run had failed: " + failed.Message));
        if (failed != null) ExceptionDispatchInfo.Throw(failed);
        return read!;
    }

    /// <summary>Removes a run's folder, trying for two seconds: Edge's helpers let go of the profile a moment after it exits. Null when it's gone.</summary>
    private static string? Remove(string folder)
    {
        for (var tries = 0; ; tries++)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                return null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (tries == 20) return e.Message;
                Thread.Sleep(100);
            }
        }
    }

    private static string SourceWeb([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "EqlPathfinder.App", "web"));
}

/// <summary>
/// What one opened page ended up as, after the run's last step. What was drawn is read through <see cref="Text"/> and
/// no other way: the list behind it (each element with an id, and what a player can read in it) is kept in here,
/// because asked directly for an id the page doesn't have it would answer as it does for a hidden card, and a test
/// with a slip in an id would pass. The readings the run took on its way are kept the same way, behind <see cref="At"/>.
/// </summary>
internal sealed class PageRun(string page, IReadOnlyDictionary<string, string> shown, IReadOnlyList<JsonObject> posted, IReadOnlyDictionary<string, PageRun>? readings = null)
{
    /// <summary>The page that's open (core.js <c>page</c>).</summary>
    public string Page { get; } = page;

    /// <summary>Every message the page posted to the window, in order.</summary>
    public IReadOnlyList<JsonObject> Posted { get; } = posted;

    /// <summary>
    /// The page as it was at the <see cref="PageStep.Read"/> step with this name: what was drawn, the page open, and
    /// what had been posted by then. A name no step of the run read is a slip in the test and throws: it never reads as
    /// the end of the run.
    /// </summary>
    public PageRun At(string reading) => readings != null && readings.TryGetValue(reading, out var then)
        ? then
        : throw new PageFailed($"The test: it reads the page as it was at \"{reading}\", and no step of the run read it under that name. "
            + (readings is { Count: > 0 } ? "The run's readings: " + string.Join(", ", readings.Keys.Select(k => $"\"{k}\"")) + "." : "This run has no reading but its end."));

    /// <summary>
    /// What a player can read in the element with this id, as the browser lays it out: a label drawn in capitals reads
    /// in capitals, and a hidden card reads as nothing. An id the page doesn't have is a slip in the test and throws.
    /// </summary>
    public string Text(string id) => shown.TryGetValue(id, out var text)
        ? text
        : throw new PageFailed($"The test: it reads the element with the id \"{id}\", and the page has none. Hidden would read as nothing; this id isn't in index.html or anything the page drew.");


    /// <summary>The hover texts (title attributes) inside the element with this id, one a line, as the page drew them.</summary>
    public string Hover(string id) => Text(id + " (hover)");

    /// <summary>Whether the element with this id, or a card around it, is drawn faded (<c>.stale</c>: waiting on the next data message).</summary>
    public bool Faded(string id) => Text(id + " (faded)") != "";
}

/// <summary>
/// One thing a run does to the page, in order: the window sends a message, the player clicks or types, or the test
/// reads the page as it is just then, under a name.
/// </summary>
internal abstract record PageStep
{
    /// <summary>The window sends this message, as JSON.</summary>
    public static PageStep Send(string json) => new Sent(json);
    public static PageStep Send(JsonNode message) => new Sent(message.ToJsonString());

    /// <summary>
    /// The player clicks the first element the selector finds that they can see, with everything a click is: mouse down
    /// (Upgrades' list picks on that), focus, mouse up, click. Nothing visible to click is a slip in the test.
    /// </summary>
    public static PageStep Click(string selector) => new Clicked(selector);

    /// <summary>The player clicks into the box the selector finds and types: it has the focus, holds the text, and hears "input".</summary>
    public static PageStep Type(string selector, string text) => new Typed(selector, text);

    /// <summary>
    /// The page's main area hears a scroll event though nothing moved, as a browser delivers one late: a click's focus
    /// scrolled the button into view a frame ago, and the event arrives after the next step.
    /// </summary>
    public static PageStep Scroll() => new Scrolled(0);

    /// <summary>The player scrolls the main area down by this many pixels (it hears the scroll event too); <see cref="PageRun.Text"/> "#main scrollTop" says how far it got.</summary>
    public static PageStep Scroll(int by) => new Scrolled(by);

    /// <summary>The page as it is now, kept under this name for <see cref="PageRun.At"/>.</summary>
    public static PageStep Read(string name) => new ReadAs(name);

    internal sealed record Sent(string Json) : PageStep;
    internal sealed record Clicked(string Selector) : PageStep;
    internal sealed record Typed(string Selector, string Text) : PageStep;
    internal sealed record Scrolled(int By) : PageStep;
    internal sealed record ReadAs(string Name) : PageStep;
}

/// <summary>A page test that couldn't run, or a page that broke: the message says which, and where.</summary>
internal sealed class PageFailed(string message) : Exception(message);

/// <summary>
/// Putting the page together: a copy of the page's files with the harness's two scripts and the stand-in for the
/// window written into its index.html, and the run's steps written into the second. Nothing here knows about a browser.
/// </summary>
file static class ComposedPage
{
    /// <summary>Where the reading is left on the page, for <see cref="PageReading"/> to find.</summary>
    public const string ReadingId = "pagetest-reading";

    /// <summary>
    /// What the page is given after each step, on its own clock, before the next: its short timers run (Upgrades posts
    /// its request 120 ms after the last change) and a promise nobody waits for has rejected. The page's clock moves
    /// on by this much a step, so a longer timer of the page's (Campaign's "click again to clear" goes back after 4.1
    /// seconds) fires some twenty steps after the click that set it.
    /// </summary>
    private const int SettleMs = 200;

    /// <summary>
    /// How long the run takes on the page's clock: its steps, and a second over. The browser writes the page out when
    /// that much has passed, so with less the run is cut short and fails as the harness's, without a reading (seen
    /// with 100 ms). With nothing over, every run still came back whole: the second is room, not something a test needs.
    /// </summary>
    public static TimeSpan Takes(PageStep[] steps) => TimeSpan.FromMilliseconds(SettleMs * steps.Count(s => s is not PageStep.ReadAs) + 1000);

    /// <summary>
    /// Goes in first, before the stand-in and core.js. It listens for every error from here on: an uncaught one while a
    /// script loads or in a listener (listened to on the way down, so a script that doesn't load counts), an unhandled
    /// rejection and console.error, which is where core.js sends a held message's throw. Each is kept with the step it
    /// came in. It opens the page a player would come back to (core.js reads <c>page</c> from localStorage). And it
    /// waits for the closing script's "pagetest" event, which carries the steps.
    ///
    /// Then each step in order, with a wait on a timer after each (never an animation frame: one asked for late in a
    /// headless run never came). A message is sent as the window sends it. A click goes to the first match a player
    /// can see: mouse down (Upgrades' list picks on that), focus, mouse up, click. Typing focuses the box, replaces
    /// its text and fires "input". A step with nothing shown to click or type into ends the run: that's the test's
    /// slip, and the reading says which step. These are the page's events and not the browser's own: the focus goes
    /// to the very element clicked when it can take it, a disabled button isn't told apart, and nothing checks that
    /// what is typed into holds text. A test that needs one of those adds it here, with itself as the proof.
    ///
    /// A reading is every element with an id (innerText, so capitals are as drawn; an element that isn't laid out reads
    /// as nothing, where innerText alone would give a <c>display: none</c> element's whole text), the page that's open
    /// and what has been posted so far. One is taken at each step that asks, and one after the last. An error that
    /// lands later is added to the reading already on the page.
    ///
    /// Taken means core's <c>data</c> is the very message sent, the last one without a kind, as soon as it was sent: a
    /// listener that was called and dropped it hasn't taken it. Opened is the page that's open until the player does
    /// something (a click may open another). core.js declares <c>page</c> and <c>data</c> with let, so they are
    /// reached by name, and reading them throws when core.js stopped before their lines.
    ///
    /// Everything is inside one function. The page's scripts share one global scope: a <c>const s</c> of the scratch
    /// run's own met theirs, the whole script was dropped without an error, and Home sat at "Reading your history…".
    /// </summary>
    private const string Opening = """
        (() => {
          const errors = [], reading = { errors, done: false };
          let holder = null, step = 0;
          const write = () => {
            if (holder) holder.textContent = JSON.stringify(reading).replace(/[<\u007f-￿]/g, c => "\\u" + c.charCodeAt(0).toString(16).padStart(4, "0"));
          };
          const failed = (text, at) => { errors.push({ text, at, step }); write(); };
          const top = stack => {
            const at = /^\s*at .*?([^\/\\(\s]+):(\d+):\d+\)?$/m.exec(String(stack ?? ""));
            return at ? at[1] + ":" + at[2] : "";
          };
          addEventListener("error", e => {
            if (e.target !== window) failed((String(e.target.src || e.target.href || "").split("/").pop() || e.target.tagName) + " didn't load", "");
            else failed(e.message || String(e.error), top(e.error?.stack) || (e.filename ? e.filename.split("/").pop() + ":" + e.lineno : ""));
          }, true);
          addEventListener("unhandledrejection", e => failed("Unhandled rejection: " + (e.reason?.message ?? String(e.reason)), top(e.reason?.stack)));
          const consoleError = console.error;
          console.error = (...said) => {
            failed("console.error: " + said.map(x => x instanceof Error ? x.message : String(x)).join(" "), top(said.find(x => x instanceof Error)?.stack));
            consoleError.apply(console, said);
          };
          localStorage.setItem("page", PAGE);

          const open = () => {
            try { if (typeof page === "string") return page; } catch { /* core.js stopped before its line */ }
            return null;
          };
          const seen = el => el.checkVisibility({ visibilityProperty: true });
          const look = () => {
            const shown = Object.create(null);
            for (const el of document.querySelectorAll("[id]"))
              if (!(el.id in shown)) {
                shown[el.id] = seen(el) ? el.innerText ?? "" : "";
                shown[el.id + " (faded)"] = el.closest(".stale") ? "faded" : "";
                shown[el.id + " (hover)"] = seen(el) ? [el, ...el.querySelectorAll("[title]")].map(t => t.title).filter(Boolean).join(String.fromCharCode(10)) : "";
              }
            shown["#main scrollTop"] = String(document.querySelector("#main").scrollTop);
            return { shown, page: open(), posted: window.chrome.webview.posted.slice() };
          };
          const find = (selector, doing) => {
            const all = [...document.querySelectorAll(selector)], el = all.find(seen);
            if (!el) reading.slip = { step, what: doing + " `" + selector + "`: " + (all.length ? "the page has " + all.length + " of those, and shows none" : "the page has nothing that matches") };
            return el;
          };
          const mouse = (el, event) => el.dispatchEvent(new MouseEvent(event, { bubbles: true, cancelable: true, view: window }));
          const click = el => { mouse(el, "mousedown"); el.focus(); mouse(el, "mouseup"); mouse(el, "click"); };
          const type = (el, text) => { el.focus(); el.value = text; el.dispatchEvent(new InputEvent("input", { bubbles: true })); };

          addEventListener("pagetest", async e => {
            try {
              const steps = e.detail, lastData = steps.findLast(s => "send" in s && !s.send?.kind);
              let played = false;
              Object.assign(reading, { steps: steps.length, opened: open(), took: true, readings: [] });
              for (const s of steps) {
                step++;
                if ("read" in s) { reading.readings.push({ name: s.read, ...look() }); continue; }
                if ("scroll" in s) { const main = document.querySelector("#main"); main.scrollTop += s.scroll; main.dispatchEvent(new Event("scroll")); }
                else if ("send" in s) {
                  window.chrome.webview.dispatchEvent(new MessageEvent("message", { data: s.send }));
                  if (s === lastData) try { reading.took = data === s.send; } catch { reading.took = false; }
                  if (!played) reading.opened = open();
                } else {
                  played = true;
                  const el = "click" in s ? find(s.click, "a click on") : find(s.type, "typing into");
                  if (!el) break;
                  if ("click" in s) click(el); else type(el, s.text);
                }
                await new Promise(waited => setTimeout(waited, SETTLE));
              }
              Object.assign(reading, look());
              reading.done = true;
            } finally {
              holder = document.body.appendChild(Object.assign(document.createElement("script"), { type: "application/json", id: READING }));
              write();
            }
          }, { once: true });
        })();
        """;

    /// <summary>Copies <paramref name="web"/> to <paramref name="copy"/> and returns the page to open there.</summary>
    public static string Write(string web, string copy, string page, PageStep[] steps)
    {
        var messages = steps.Count(s => s is PageStep.Sent);
        for (int i = 0, message = 0; i < steps.Length; i++)
        {
            if (steps[i] is not PageStep.Sent sent) continue;
            message++;
            try { using var _ = JsonDocument.Parse(sent.Json); }
            catch (JsonException e) { throw new PageFailed($"The test: message {message} of {messages} isn't JSON, and the window sends JSON (step {i + 1} of the run): {e.Message}"); }
        }
        if (steps.OfType<PageStep.ReadAs>().GroupBy(r => r.Name).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new PageFailed($"The test: {twice.Count()} steps read the page as \"{twice.Key}\". A reading's name is how a test finds it, so each needs its own.");

        foreach (var f in Directory.GetFiles(web, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(copy, Path.GetRelativePath(web, f));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target);
        }

        var index = Path.Combine(copy, "index.html");
        var html = File.ReadAllText(index);
        const string core = "<script src=\"js/core.js\"></script>", end = "</body>";
        if (!html.Contains(core) || !html.Contains(end))
            throw new PageFailed("The harness: index.html no longer loads js/core.js, or no longer ends its body, the way the page tests expect");

        var opening = Opening.Replace("PAGE", InScript(JsonSerializer.Serialize(page))).Replace("READING", JsonSerializer.Serialize(ReadingId))
            .Replace("SETTLE", SettleMs.ToString(CultureInfo.InvariantCulture));
        var standIn = WindowStandIn.Script;
        // The steps are written into the page as they are: a "<" in one can't end the script it's in.
        var closing = "window.dispatchEvent(new CustomEvent(\"pagetest\", { detail: [" + string.Join(", ", steps.Select(s => InScript(InPage(s)))) + "] }));";
        html = html.Replace(core, $"<script>\n{opening}\n</script>\n<script>\n{standIn}\n</script>\n{core}")
            .Replace(end, $"<script>{closing}</script>\n{end}");
        File.WriteAllText(index, html);
        return index;
    }

    /// <summary>A step as the opening script takes it: a message as the window's JSON, the rest by what the player does.</summary>
    private static string InPage(PageStep step) => step switch
    {
        PageStep.Sent sent => "{ \"send\": " + sent.Json + " }",
        PageStep.Clicked clicked => new JsonObject { ["click"] = clicked.Selector }.ToJsonString(),
        PageStep.Typed typed => new JsonObject { ["type"] = typed.Selector, ["text"] = typed.Text }.ToJsonString(),
        PageStep.Scrolled scrolled => "{ \"scroll\": " + scrolled.By + " }",
        PageStep.ReadAs read => new JsonObject { ["read"] = read.Name }.ToJsonString(),
        _ => throw new PageFailed($"The harness: it has no way to do a step of the kind {step.GetType().Name}"),
    };

    private static string InScript(string json) => json.Replace("<", "\\u003c");
}

/// <summary>What a browser gave back for one page.</summary>
file sealed record Dumped(int ExitCode, string Output, string Errors);

/// <summary>A browser that outlived its timeout, and whether Edge's renderer was among the processes left (a hang with none is the stall that gets one retry).</summary>
file sealed class Hung(string message, bool noRenderer) : Exception(message)
{
    public bool NoRenderer { get; } = noRenderer;
}

/// <summary>
/// Running a browser on a page: the only part that knows where Edge is, its switches, that the page comes back as text
/// on its output, and the process. Started without the shell.
/// </summary>
file static class HeadlessEdge
{
    public static string Find(IReadOnlyList<string> given)
    {
        var places = given.Count > 0 ? given : Installs();
        return places.FirstOrDefault(File.Exists) ?? throw new PageFailed(
            "The browser: Microsoft Edge wasn't found, and a page test needs it: it's never skipped. Looked in:\n  " + string.Join("\n  ", places));
    }

    private static IReadOnlyList<string> Installs() =>
        new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.LocalApplicationData }
            .Select(Environment.GetFolderPath).Where(d => d.Length > 0).Distinct()
            .Select(d => Path.Combine(d, "Microsoft", "Edge", "Application", "msedge.exe")).ToList();

    /// <summary>
    /// Draws <paramref name="file"/>, lets it run for <paramref name="pageTime"/> on the page's own clock, and gives
    /// back what Edge wrote. A browser still going after <paramref name="timeout"/> is stopped with everything it
    /// started, and that is the failure.
    /// </summary>
    public static Dumped Dump(string browser, string file, string profile, TimeSpan pageTime, TimeSpan timeout, EdgeSlots slots, PageRetries retries, Func<string, List<string>>? lister, string page)
    {
        try { return Attempt(browser, file, profile, pageTime, timeout, slots, lister); }
        catch (Hung first)
        {
            // Only the stall with no renderer left is retried, once, in a fresh slot with a fresh profile. A page that
            // really hangs keeps its renderer and fails here. Past the run's retries the browser stays red.
            if (!first.NoRenderer) throw new PageFailed(first.Message);
            if (!retries.TryTake())
                throw new PageFailed(first.Message + $"\nThis browser needed a retry too, and the run's {retries.Max} retries were used: more than {retries.Max} browsers hanging with no renderer is a red run ({PageRetries.Marker}).");
            retries.Log(PageRetries.Who(page), first.Message);
            try { return Attempt(browser, file, profile + "-retry", pageTime, timeout, slots, lister); }
            catch (Hung second) { throw new PageFailed(second.Message + "\nIt was retried once after hanging with no renderer, and hung again."); }
        }
    }

    private static Dumped Attempt(string browser, string file, string profile, TimeSpan pageTime, TimeSpan timeout, EdgeSlots slots, Func<string, List<string>>? lister)
    {
        var start = new ProcessStartInfo(browser)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in new[]
                 {
                     "--headless=new", "--disable-gpu",
                     // Its own profile, and nothing of Edge's own that reaches out: no first run, sync or background networking.
                     "--user-data-dir=" + profile, "--no-first-run", "--no-default-browser-check", "--disable-sync",
                     "--disable-background-networking", "--disable-component-update", "--disable-extensions",
                     // One window whatever this PC's display is (the --snapshot pages' size), and one locale: with
                     // --lang=de-DE the same page drew "76,9".
                     "--window-size=1274,757", "--force-device-scale-factor=1", "--lang=en-US",
                     // Opened from a file, each script is its own origin and an uncaught error reaches the page as
                     // "Script error." with no file, line or stack. With this they count as the page's own, as in the app.
                     "--allow-file-access-from-files",
                     // The page is written out once this much of its own time has passed, and that time is the
                     // browser's to hurry: a timer's wait costs the run nothing by the clock on the wall.
                     "--virtual-time-budget=" + ((long)pageTime.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
                     "--dump-dom", new Uri(file).AbsoluteUri,
                 })
            start.ArgumentList.Add(a);

        var name = Path.GetFileName(browser);
        // A browser starts only once a slot is free, and the timeout counts from then: a run that waited its turn
        // isn't one that hung. Every test run on this PC shares the slots (EdgeSlots).
        using var slot = slots.Acquire();
        PageLimits.RaiseToNormal();
        Process edge;
        try { edge = Process.Start(start) ?? throw new InvalidOperationException("no process was started"); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new PageFailed($"The browser: {browser} couldn't be started: {e.Message}");
        }
        using (edge)
        {
            // Read as it comes, so that a browser that hangs still has what it wrote to show.
            var output = new StringBuilder();
            var errors = new StringBuilder();
            edge.OutputDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
            edge.ErrorDataReceived += (_, e) => { lock (errors) errors.AppendLine(e.Data); };
            edge.BeginOutputReadLine();
            edge.BeginErrorReadLine();
            // With its output read this way, this waits for the output to end too: that is, for every helper that inherited it.
            if (!edge.WaitForExit(timeout))
            {
                var itself = edge.HasExited ? "its own process had already ended, so something that inherited its output was holding the run" : "its own process was still running";
                // Asked only of Edge: a stand-in browser in a harness test has no helpers worth the seconds this takes under load.
                var asked = lister != null || name.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase);
                var alive = lister != null ? lister(profile) : asked ? StillAlive(profile) : [];
                // Retried only when Windows really answered and no renderer is among the answer: "could not ask" is not "no renderer".
                // An empty answer while Edge's own process was still running is no answer either: its own browser process names the profile.
                var answered = !(alive.Count == 0 && !edge.HasExited);
                var noRenderer = asked && answered && !alive.Any(a => a.StartsWith("renderer:", StringComparison.Ordinal) || a.StartsWith("(Windows was not asked", StringComparison.Ordinal) || a.StartsWith("(could not ask", StringComparison.Ordinal));
                try { var dumps = Directory.Exists(profile) ? Directory.GetFiles(profile, "*.dmp", SearchOption.AllDirectories).Length : 0; alive.Add($"(crash dumps in the profile: {dumps})"); } catch (IOException) { }
                string left = "";
                try { edge.Kill(entireProcessTree: true); }
                catch (Exception e) when (e is AggregateException or InvalidOperationException or System.ComponentModel.Win32Exception) { left = " Stopping it failed: " + e.Message; }
                edge.WaitForExit(TimeSpan.FromSeconds(5));
                string said;
                lock (errors) said = errors.ToString().Trim();
                throw new Hung($"The browser: {name} took longer than {timeout.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} seconds, and was stopped with everything it started, and it had a browser slot from the start, so it was slow or hung, not waiting its turn.{left}"
                    + $"\nWhen it was stopped, {itself}."
                    + $"\nStill running with this run's profile ({alive.Count}):\n  " + (alive.Count == 0 ? "(none)" : string.Join("\n  ", alive))
                    + $"\nWhat it wrote on stderr:\n{(said.Length == 0 ? "(nothing)" : said.Length > 2000 ? said[..2000] + "…" : said)}", noRenderer);
            }
            edge.WaitForExit();
            string o, er;
            lock (output) o = output.ToString();
            lock (errors) er = errors.ToString();
            return new Dumped(edge.ExitCode, o, er);
        }
    }

    /// <summary>Every process whose command line names this run's profile (Edge's helpers all do), as "type: command line", asked of Windows before anything is stopped.</summary>
    private static List<string> StillAlive(string profile)
    {
        try
        {
            var ask = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                ArgumentList = { "-NoProfile", "-NonInteractive", "-Command",
                    "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($env:PF_PROFILE) } | ForEach-Object { $t = if ($_.CommandLine -match '--type=([a-z-]+)') { $Matches[1] } else { 'browser' }; $t + ': ' + $_.CommandLine.Substring(0, [Math]::Min(300, $_.CommandLine.Length)) }" },
            };
            ask.Environment["PF_PROFILE"] = profile;
            using var p = Process.Start(ask)!;
            var text = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(3_000)) { try { p.Kill(); } catch (InvalidOperationException) { } return ["(Windows was not asked in time: it took more than 3 seconds)"]; }
            return text.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        catch (Exception e) { return ["(could not ask Windows: " + e.Message + ")"]; }
    }
}

/// <summary>Turning what the browser gave back into a <see cref="PageRun"/>, or the failure and whose it is. Nothing here knows about Edge.</summary>
file static class PageReading
{
    public static PageRun From(string page, string browser, Dumped dumped)
    {
        var name = Path.GetFileName(browser);
        var holder = Regex.Match(dumped.Output, "<script\\b[^>]*\\bid=\"" + ComposedPage.ReadingId + "\"[^>]*>(.*?)</script>", RegexOptions.Singleline);
        if (!holder.Success)
        {
            // Edge updates itself: a switch it stops knowing, or a crash, is the browser's failure and not "the text isn't on the page".
            if (!dumped.Output.Contains("<html", StringComparison.OrdinalIgnoreCase))
                throw new PageFailed($"The browser: {name} gave no page back (exit code {dumped.ExitCode}). What it wrote:\n{Short((dumped.Errors + "\n" + dumped.Output).Trim(), 2000)}");
            throw new PageFailed($"The harness: {name} gave a page back without the reading the harness's script leaves in it (exit code {dumped.ExitCode}): " +
                $"that script was dropped, the page went somewhere else, or the run was written out before its steps were done. This is not the page's failure. The page began:\n{Short(dumped.Output.Trim(), 600)}");
        }

        var reading = JsonNode.Parse(holder.Groups[1].Value)!.AsObject();
        // Each error with where it was thrown and the step it came in; one from while the page loaded has no step.
        var errors = reading["errors"]!.AsArray().Select(e =>
        {
            var where = new[] { (string?)e!["at"] is { Length: > 0 } at ? "at " + at : null, (int?)e["step"] is > 0 and var step ? "in step " + step : null }.Where(w => w != null).ToList();
            return ((string?)e["text"] ?? "") + (where.Count > 0 ? $" ({string.Join(", ", where)})" : "");
        }).ToList();
        if ((bool?)reading["done"] != true)
            throw new PageFailed("The harness: its own script stopped before the reading was whole. This is not the page's failure. The errors on the page:\n  " + string.Join("\n  ", errors));

        var end = Look(reading, reading["readings"]!.AsArray().ToDictionary(r => (string)r!["name"]!, r => Look(r!.AsObject())));
        // What the page that's open now had drawn: a click may have left the page the test asked for.
        var open = (string?)reading["page"] ?? page;
        var drawn = $"\nWhat {open} had drawn:\n{((string?)reading["shown"]!["page-" + open] is { Length: > 0 } text ? Short(text, 600) : "(nothing)")}";
        var stopped = reading["slip"] is JsonObject slip ? $"step {(int?)slip["step"]} of {(int?)reading["steps"]} couldn't be done, {(string?)slip["what"]}" : null;
        if (errors.Count > 0)
            throw new PageFailed($"The page: {(errors.Count == 1 ? "an error" : errors.Count + " errors")} while {page} loaded and went through the run's steps{(errors.Count > 1 ? ", in the order they came" : "")}:\n  "
                + string.Join("\n  ", errors.Take(10)) + (stopped == null ? "" : $"\nThe run ended early: {stopped}.") + drawn);
        var opened = (string?)reading["opened"];
        if (opened != page)
            throw new PageFailed($"The page: it opened another page. The test asked for \"{page}\" and {(opened == null ? "core.js never got as far as choosing one" : $"\"{opened}\" is open")}." + drawn);
        if ((bool?)reading["took"] != true)
            throw new PageFailed("The page: it never took the page data it was sent. core.js's `data` isn't the last message without a kind: its listener wasn't called, or was called and dropped it." + drawn);
        // After the page's own failures: a page that broke or drew nothing leaves nothing to click, and that isn't the test's slip.
        if (stopped != null)
            throw new PageFailed($"The test: {stopped}. The run ended there." + drawn);

        return end;
    }

    /// <summary>One reading of the page: what was drawn, the page that was open, and what had been posted by then.</summary>
    private static PageRun Look(JsonObject read, IReadOnlyDictionary<string, PageRun>? readings = null) => new(
        (string?)read["page"] ?? "", read["shown"]!.AsObject().ToDictionary(p => p.Key, p => (string?)p.Value ?? ""),
        read["posted"]!.AsArray().Select(p => p!.AsObject()).ToList(), readings);

    private static string Short(string text, int most) => text.Length <= most ? text : text[..most] + "…";
}
