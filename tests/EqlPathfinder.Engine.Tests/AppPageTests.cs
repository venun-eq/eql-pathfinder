using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The app's page (src/EqlPathfinder.App/web), read from the source: things that only go wrong inside WebView2.</summary>
public class AppPageTests
{
    private static string WebDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "EqlPathfinder.App", "web"));

    [Fact]
    public void NoBoxLeansOnTheBrowsersDatalistPopup()
    {
        // WebView2's popup for <input list> showed every option but didn't filter as you typed: a player's report on the
        // Upgrades item box in 0.9.34. Suggestions are drawn by the page instead (upgrade.js, renderUpPick).
        var datalist = new Regex(@"<datalist\b|\slist\s*=\s*[""'`$]", RegexOptions.IgnoreCase);
        var files = Directory.GetFiles(WebDir(), "*.html").Concat(Directory.GetFiles(Path.Combine(WebDir(), "js"), "*.js")).ToList();
        Assert.Contains(files, f => Path.GetFileName(f) == "index.html");
        foreach (var f in files)
            Assert.False(datalist.IsMatch(File.ReadAllText(f)), $"{Path.GetFileName(f)} uses a datalist");
    }

    [Fact]
    public void TheUpgradesItemBoxHasItsOwnSuggestionList()
    {
        var html = File.ReadAllText(Path.Combine(WebDir(), "index.html"));
        Assert.Matches(new Regex(@"<input\b[^>]*\bid=""upItem""[^>]*\brole=""combobox""[^>]*\baria-controls=""upItems"""), html);
        Assert.Matches(new Regex(@"<div\b[^>]*\bid=""upItems""[^>]*\brole=""listbox"""), html);
    }

    [Fact]
    public void NoBetaSwitchOrCameAcrossWordsAreLeftOnThePage()
    {
        // The new planner is out of beta (docs/specs/planner-out-of-beta.md): no switch, no came-across card, no old
        // planner's lists. Comments count too, so none is left describing code that's gone. The one "old planner" a player
        // reads is the card's first sentence.
        const string Card = "Plans made on the old planner";
        string[] gone = ["(beta)", "came across", "bring across", "old planner", "Earn these first", "Start again from your old plan"];
        var files = Directory.GetFiles(WebDir(), "*.html").Concat(Directory.GetFiles(Path.Combine(WebDir(), "js"), "*.js")).ToList();
        Assert.Contains(files, f => Path.GetFileName(f) == "campaign.js");
        var left = (from f in files
                    let text = File.ReadAllText(f).Replace(Card, "")
                    from w in gone
                    where text.Contains(w, StringComparison.OrdinalIgnoreCase)
                    select $"{Path.GetFileName(f)}: \"{w}\"").ToList();
        Assert.True(left.Count == 0, "Still on the page:\n" + string.Join("\n", left));
    }

    private static IEnumerable<(string File, string Text)> Scripts() =>
        Directory.GetFiles(Path.Combine(WebDir(), "js"), "*.js").Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

    [Fact]
    public void EveryClockTimeThePageShowsIsKnownToReportAProblem()
    {
        // Report a problem hides each clock time the page has shown from its screenshot (report.js hidePrivate), by the
        // strings core.js clockShown collected: clock times never leave the PC. A time formatted any other way would be
        // missed in locales that don't write it with a colon. core.js dt is the one place that formats date and time
        // together, and it hands the time to clockShown itself.
        var time = new Regex(@"toLocaleTimeString\s*\(|\bhour\s*:");
        var dt = new Regex(@"const dt = s => \{.*?\n\};", RegexOptions.Singleline);
        var core = Scripts().Single(s => s.File == "core.js").Text;
        Assert.Matches(new Regex(@"clockShown\(d\.toLocaleTimeString"), dt.Match(core).Value);
        foreach (var (file, text) in Scripts())
        {
            var lines = (file == "core.js" ? dt.Replace(text, "") : text).Split('\n');
            foreach (var line in lines.Where(l => time.IsMatch(l)))
                Assert.True(line.Contains("clockShown("), $"{file}: a clock time not given to clockShown: {line.Trim()}");
        }
    }

    [Fact]
    public void OnlyCoreListensToTheWindowSoAScreenshotCanHoldEveryMessage()
    {
        // While Report a problem takes its screenshot, core.js holds the window's messages so nothing redraws with a name
        // or a time showing. A script listening for itself would redraw anyway.
        var listen = new Regex(@"webview\s*\??\.\s*addEventListener\s*\(\s*[""']message");
        var direct = Scripts().Where(s => listen.IsMatch(s.Text)).Select(s => s.File).ToList();
        Assert.Equal(["core.js"], direct);
        Assert.Contains("heldMessages.push", Scripts().Single(s => s.File == "core.js").Text);
    }

    [Fact]
    public void ReportAProblemIsOnTheRailAndItsShortcutIsTheWindows()
    {
        var html = File.ReadAllText(Path.Combine(WebDir(), "index.html"));
        // A rail button that isn't a page: the rail's page buttons are found by [data-page], or Report would open Home.
        Assert.Matches(new Regex(@"<button data-report\b"), html);
        Assert.Matches(new Regex(@"<dialog\b[^>]*\bid=""report"""), html);
        Assert.Contains("<script src=\"js/report.js\"></script>", html);
        var core = Scripts().Single(s => s.File == "core.js").Text;
        Assert.DoesNotContain("$$(\".rail button\")", core);
        // Ctrl+Shift+R is WebView2's hard reload: the window takes it before the browser does (MainWindow.xaml.cs).
        var window = File.ReadAllText(Path.Combine(WebDir(), "..", "MainWindow.xaml.cs"));
        Assert.Matches(new Regex(@"PreviewKeyDown[\s\S]{0,400}Key\.R[\s\S]{0,400}openReport\(\)"), window);
    }

    [Fact]
    public void TheWindowAndThePreviewSendThePageDataTheEngineBuilds()
    {
        // PageDataTests hold the engine's page data to the message every page was sent before it moved there. That says
        // something about the app only while the window and the --snapshot preview send that message and nothing they
        // put together themselves, and the test project can't load the app to watch them: so this reads their source.
        // The preview's own copy had lost six settings by 11-17, and it set `data` and called render() itself, so it
        // would have drawn every page of a build whose message handler threw (docs/debt.md).
        var window = File.ReadAllText(Path.Combine(WebDir(), "..", "MainWindow.xaml.cs"));
        var preview = File.ReadAllText(Path.Combine(WebDir(), "..", "DevSnapshots.cs"));
        Assert.Contains("PageData.Build(", window);
        Assert.Contains("PageData.Build(", preview);
        // The window writes and reads its messages with the engine's options, the ones the tests use: it keeps none.
        Assert.DoesNotContain("JsonNamingPolicy", window);
        Assert.DoesNotContain("AllowReadingFromString", window);
        // The preview hands the page data over as a message, as the window does.
        Assert.DoesNotMatch(new Regex(@"\bdata\s*=\s*\{"), preview);
        Assert.DoesNotContain("render();</script>", preview);
    }

    [Fact]
    public void TheWindowAndThePreviewSendTheUpgradesAnswerTheEngineBuilds()
    {
        // UpgradesAnswerTests and UpgradePageTests hold the engine's answer (Pages/UpgradePage) to what upgrade.js reads
        // and draws. That says something about the app only while the window and the --snapshot preview send that
        // answer and none they put together themselves, and the test project can't load the app to watch them: so
        // this reads their source, as the test above does for the page data. Comments aren't read.
        static string Code(string file) => Regex.Replace(File.ReadAllText(file), @"//[^\n]*", "");
        foreach (var name in new[] { "MainWindow.xaml.cs", "DevSnapshots.cs" })
        {
            var code = Code(Path.Combine(WebDir(), "..", name));
            Assert.True(code.Contains("UpgradePage.Build("), $"{name} doesn't send the Upgrades answer the engine builds (UpgradePage.Build)");
            Assert.False(Regex.IsMatch(code, @"kind\s*=\s*""upgrade"""), $"{name} puts an Upgrades answer together itself");
        }
    }

    [Fact]
    public void ThePreviewAndThePageTestsUseOneStandInForTheWindow()
    {
        // preview.html is opened in Chrome (the app-screenshots skill), where there's no window behind the page, so
        // --snapshot writes a stand-in for chrome.webview into it. Nothing ran that stand-in: it was shown to work
        // once, by hand (docs/specs/page-data-in-engine.md). The page tests need the same thing in headless Edge, so
        // there is one text (the engine's WindowStandIn.Script), the tests run it every time, and the preview gets
        // what they proved. The test project can't load the app, so that the two don't drift apart again is read
        // from the source: neither writes a chrome.webview of its own.
        static string Code(string file) => Regex.Replace(File.ReadAllText(file), @"//[^\n]*", "");
        var own = new Regex(@"\bwebview\s*=(?![=>])");
        var preview = Code(Path.Combine(WebDir(), "..", "DevSnapshots.cs"));
        var harness = Code(Path.Combine(Path.GetDirectoryName(WebDir())!, "..", "..", "tests", "EqlPathfinder.Engine.Tests", "PageHarness.cs"));
        foreach (var (name, text) in new[] { ("DevSnapshots.cs", preview), ("PageHarness.cs", harness) })
        {
            Assert.False(own.IsMatch(text), $"{name} writes a chrome.webview of its own");
            Assert.Contains("WindowStandIn.Script", text);
        }
        // And the one text is a stand-in: it only steps in where there is no window, as in WebView2 there is.
        Assert.Matches(new Regex(@"if\s*\(\s*!window\.chrome\?\.webview\s*\)"), EqlPathfinder.Engine.Pages.WindowStandIn.Script);
    }

    [Fact]
    public void RenderingScreenshotsNeverStartsTheAppOnThePlayersData()
    {
        // --snapshot and --meter-snapshot need an Application for its resources and for MainWindow. WPF queues OnStartup
        // in Application's constructor and the first message pump runs it, so with a plain `new App()` the app's whole
        // start-up ran inside a render: it loaded the real settings.json and opened the real pathfinder.db, whatever
        // database was passed. On 11-18, with the installed app open, that put "couldn't open its database" on the
        // player's desktop three times, wrote to the real error.log and never exited. With it closed it would have
        // tracked, and could have shared, as the real install (docs/engineering.md, rule 5: one writer per database).
        // The test project can't load the app, and the run that shows this needs the installed app open, so this reads
        // the source: a render makes an App of its own whose OnStartup does nothing, and only Program.Main makes the
        // plain one. Comments aren't read, so they can name what this guards.
        static string Code(string file) => Regex.Replace(File.ReadAllText(file), @"//[^\n]*", "");
        var plain = new Regex(@"\bnew\s+(EqlPathfinder\.App\.)?App\s*\(|\bApp\s+\w+\s*=\s*new\s*\(");
        var files = Directory.GetFiles(Path.Combine(WebDir(), ".."), "*.cs");
        Assert.Single(plain.Matches(Code(files.Single(f => Path.GetFileName(f) == "Program.cs"))));
        foreach (var f in files.Where(f => Path.GetFileName(f) != "Program.cs"))
            Assert.False(plain.IsMatch(Code(f)), $"{Path.GetFileName(f)} makes the app itself, so the app's start-up runs there: on the real settings and database");

        var preview = Code(files.Single(f => Path.GetFileName(f) == "DevSnapshots.cs"));
        var own = Regex.Match(preview, @"\bclass\s+(?<name>\w+)\s*:\s*App\b[\s\S]*?\boverride\s+void\s+OnStartup\s*\([^)]*\)\s*\{(?<body>[^{}]*)\}");
        Assert.True(own.Success, "DevSnapshots.cs has no App of its own with OnStartup overridden");
        Assert.True(string.IsNullOrWhiteSpace(own.Groups["body"].Value), "the render's OnStartup does something: " + own.Groups["body"].Value.Trim());
        Assert.Matches(new Regex(@"\bnew\s+" + own.Groups["name"].Value + @"\s*\("), preview);
    }
}
