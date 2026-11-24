using System.Diagnostics;
using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>A folder of this test's own that's gone afterwards: a copy of the page with a slip in it, or a stand-in for the browser.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pf-pagetest-" + Guid.NewGuid().ToString("N"));
    public TempFolder() => Directory.CreateDirectory(Path);
    public string File(string name, string text) { var f = System.IO.Path.Combine(Path, name); System.IO.File.WriteAllText(f, text); return f; }
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}

/// <summary>
/// The page tests' harness, held to what a gate needs of it: a page that breaks fails its test and says how, and a
/// browser that's missing, gives nothing back or hangs fails too, and never passes in silence or stops the gate.
/// The copies with a slip in them are to these tests what tools/check.ps1's "page scripts gate" samples are to webcheck.
/// </summary>
public class PageHarnessTests
{
    /// <summary>A copy of the page's files with one script changed. The change must take, or the sample has rotted with the source.</summary>
    private static PageHarness WithASlip(TempFolder copy, string script, string from, string to)
    {
        var source = new PageHarness().Web;
        foreach (var f in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(copy.Path, Path.GetRelativePath(source, f));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target);
        }
        var file = Path.Combine(copy.Path, "js", script);
        var text = File.ReadAllText(file);
        Assert.True(text.Contains(from), $"{script} no longer has `{from}`: this sample needs the line it breaks");
        File.WriteAllText(file, text.Replace(from, to));
        return new PageHarness { Web = copy.Path };
    }

    private static async Task<string> OneCrawl() => (await PageMessage.FarmingAtTheCap()).With("reinc.home.farm.crawls", 1).ToJsonString();

    private static async Task<JsonObject> JustStarted() => (await PageMessage.FarmingAtTheCap())
        .With("snapshot.session.early", true).With("snapshot.session.crawlMinutes", 2).With("snapshot.session.wallMinutes", 2);

    /// <summary>Where a slip is, as a failure names it: "home.js:298", the script and the line that has <paramref name="text"/>.</summary>
    private static string Place(TempFolder copy, string script, string text)
    {
        var lines = File.ReadAllLines(Path.Combine(copy.Path, "js", script));
        return $"{script}:{Array.FindIndex(lines, l => l.Contains(text)) + 1}";
    }

    /// <summary>
    /// A name nothing declares, on a path only real data reaches: webcheck's empty message never gets to Home's farming
    /// card, so it passes this copy. The test that opens the page fails, with the error and the line that threw it.
    /// Core passes a listener's error on (dispatchHost), so the error is reported from a line of core.js: the place a
    /// failure names is the top of the error's stack, the line in home.js, or it sends someone to the wrong script.
    /// </summary>
    [Fact]
    public async Task APageWhoseDrawThrowsFailsItsTestWithTheErrorAndWhereItIs()
    {
        using var copy = new TempFolder();
        var broken = WithASlip(copy, "home.js", "const early = !!s.early,", "const early = !!sess.early,");
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => broken.Open("home", message));
        Assert.Contains("sess is not defined", e.Message);
        Assert.Contains(Place(copy, "home.js", "const early = !!sess.early,"), e.Message);
    }

    /// <summary>
    /// 0.9.37's kind: a script throws while it loads. Here home.js looks for a card index.html doesn't have, so the
    /// rest of home.js never runs. Webcheck's stand-in for the page has every card, so it passes this copy, and Home's
    /// hero still draws from its data, so a test that only read the text would pass too. Any error fails the test, with
    /// the script and the line. Edge hides both for a page opened from a file ("Script error.") unless it's started so
    /// that the page's scripts count as the page's own: a failure that says only that sends nobody anywhere.
    /// </summary>
    [Fact]
    public async Task AScriptThatThrowsWhileLoadingFailsTheTestWithTheErrorAndItsLine()
    {
        using var copy = new TempFolder();
        var broken = WithASlip(copy, "home.js", "$(\"#homeHero\").addEventListener(\"click\"", "$(\"#homeHeroo\").addEventListener(\"click\"");
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => broken.Open("home", message));
        Assert.Contains("addEventListener", e.Message);   // Cannot read properties of null (reading 'addEventListener')
        Assert.Contains(Place(copy, "home.js", "$(\"#homeHeroo\")"), e.Message);
        Assert.DoesNotContain("Script error", e.Message);
    }

    /// <summary>
    /// Nothing throws here: Home just forgets to wait for a fair rate, as it did in 0.10.3. The harness reads what the
    /// copy really draws, so Home's own test fails on it. A source-text test passed any page whose text still matched.
    /// </summary>
    [Fact]
    public async Task APageThatDrawsTheWrongThingFailsHomesTest()
    {
        using var copy = new TempFolder();
        var broken = WithASlip(copy, "home.js", "const early = !!s.early,", "const early = false,");
        var message = await JustStarted();
        var home = broken.Open("home", message.ToJsonString());
        Assert.Contains("T5-eq an hour this session", home.Text("homeHero"));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => HomePageTests.WaitsForTheSessionRate(home, message));
    }

    /// <summary>
    /// Core hears the message and drops it: the hold that Report a problem puts on messages while it takes its picture
    /// is left on from the start. Nothing throws, webcheck passes this copy, and every page would sit at "Reading your
    /// history…". The test fails and says the message was never taken; it doesn't go on to read an empty page.
    /// Taken means core's `data` is the message that was sent: that a listener was called isn't enough, as here.
    /// </summary>
    [Fact]
    public async Task APageThatNeverTakesItsDataFailsTheTestAndSaysSo()
    {
        using var copy = new TempFolder();
        var broken = WithASlip(copy, "core.js", "let heldMessages = null;", "let heldMessages = [];");
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => broken.Open("home", message));
        Assert.Contains("never took the page data", e.Message);
    }

    /// <summary>
    /// Any error fails the test, even when every string it wants was drawn. Two kinds stop no script, so they never
    /// reach the window's error event: what the page logs with console.error (core.js logs a held message's throw
    /// that way), and a promise nobody waits for, which rejects only after the page has been read. Here core does
    /// both as it takes the page data, and Home draws all the same. The test after this one does the same from a click.
    /// </summary>
    [Fact]
    public async Task WhatAPageLogsAsAnErrorOrRejectsUnheardFailsTheTestThoughHomeDrew()
    {
        using var copy = new TempFolder();
        var broken = WithASlip(copy, "core.js", "  data = e.data;",
            "  data = e.data; console.error(\"a held message threw\"); Promise.reject(new Error(\"nobody waits for this\"));");
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => broken.Open("home", message));
        Assert.Contains("console.error: a held message threw", e.Message);
        Assert.Contains("Unhandled rejection: nobody waits for this", e.Message);
        Assert.Contains(Place(copy, "core.js", "nobody waits for this"), e.Message);
        Assert.Contains("T5-eq an hour this session", e.Message);   // Home drew: the failure ends with what it had drawn
    }

    /// <summary>
    /// The same three kinds from a click: here Roster's listener for Set counts logs an error, starts a promise nobody
    /// waits for, and then throws on a name nothing declares, so the click on Home's banner goes nowhere. Nothing
    /// about the page as drawn says so. The run fails as the page's, with all three and the line of roster.js that
    /// threw. (The pages' own handlers that wait, report.js's Report a problem and share.js's Copy, are behind the
    /// rail's button and Settings' share card: no click in the page tests reaches them yet.)
    /// </summary>
    [Fact]
    public async Task AnErrorInAClicksHandlerFailsTheTestWithTheErrorAndWhereItIs()
    {
        using var copy = new TempFolder();
        var broken = WithASlip(copy, "roster.js", "  if (page !== \"roster\") show(\"roster\");",
            "  console.error(\"the counts stayed shut\"); Promise.reject(new Error(\"nobody waits for this click\")); if (page !== \"roster\") showw(\"roster\");");
        var message = await PageMessage.FromFixture();
        var e = Assert.Throws<PageFailed>(() => broken.Run("home", PageStep.Send(message), PageStep.Click("#page-home .banner [data-set-counts]")));
        Assert.StartsWith("The page:", e.Message);
        Assert.Contains("showw is not defined", e.Message);
        Assert.Contains(Place(copy, "roster.js", "showw(\"roster\")"), e.Message);
        Assert.Contains("console.error: the counts stayed shut", e.Message);
        Assert.Contains("Unhandled rejection: nobody waits for this click", e.Message);
        Assert.Contains("Your log shows no reincarnations", e.Message);   // Home is still what's drawn: the click went nowhere
        // Each error says which step it came in (added with the build): the click, not the message before it.
        Assert.Equal(3, e.Message.Split("in step 2)").Length - 1);
        Assert.DoesNotContain("in step 1)", e.Message);
    }

    /// <summary>
    /// A click needs something the player can see. On Home, Roster's banner is in the page but not shown: a step that
    /// clicks it is a slip in the test, and the run fails as the test's with the step and what it looked for. It
    /// doesn't click the hidden button, and it doesn't go on to read a page the click never changed.
    /// </summary>
    [Fact]
    public async Task AClickOnSomethingThePageDoesNotShowFailsAsTheTestsAndNamesTheStep()
    {
        var message = await PageMessage.FromFixture();
        using var scratch = new TempFolder();
        var harness = new PageHarness { Scratch = scratch.Path };
        var e = Assert.Throws<PageFailed>(() => harness.Run("home", PageStep.Send(message), PageStep.Click("#countsBanner [data-set-counts]")));
        Assert.StartsWith("The test:", e.Message);
        Assert.Contains("step 2", e.Message);
        Assert.Contains("#countsBanner [data-set-counts]", e.Message);
        Assert.Empty(Directory.GetFileSystemEntries(scratch.Path));
    }

    /// <summary>The gate needs Edge on this PC. Without it a page test fails and says where it looked: it's never skipped, and never passes.</summary>
    [Fact]
    public async Task WithoutEdgeAPageTestFailsAndSaysWhereItLooked()
    {
        using var nowhere = new TempFolder();
        var missing = Path.Combine(nowhere.Path, "msedge.exe");
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => new PageHarness { Browsers = [missing] }.Open("home", message));
        Assert.Contains("Microsoft Edge", e.Message);
        Assert.Contains(missing, e.Message);
    }

    /// <summary>
    /// Edge updates itself, and one day may not know a switch or may fall over. Then the failure is the browser's, with
    /// what it wrote and how it ended, and not "the text isn't on the page", which would send someone to look at Home.
    /// </summary>
    [Fact]
    public async Task ABrowserThatGivesNothingBackFailsAsTheBrowserNotAsThePage()
    {
        using var stand = new TempFolder();
        var silent = stand.File("silent.cmd", "@echo this browser knows no such switch 1>&2\r\n@exit /b 3\r\n");
        using var scratch = new TempFolder();
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => new PageHarness { Browsers = [silent], Scratch = scratch.Path }.Open("home", message));
        Assert.Contains("gave no page back", e.Message);
        Assert.Contains("this browser knows no such switch", e.Message);
        Assert.Contains("exit code 3", e.Message);
        Assert.Empty(Directory.GetFileSystemEntries(scratch.Path));
    }

    /// <summary>
    /// The browser ran and gave a page back, but what the harness reads from it isn't there: its own script was dropped
    /// (in the scratch run a `const s` of its own met the page's, and the script went without an error), or the page
    /// went somewhere else. That's the harness's failure and says so. It isn't the browser's ("gave no page back"), and
    /// it must not go on as a page with nothing on it, where every test would say its text is missing.
    /// </summary>
    [Fact]
    public async Task APageGivenBackWithoutItsReadingFailsAsTheHarnessNotAsThePage()
    {
        using var stand = new TempFolder();
        stand.File("page.html", "<!doctype html><html><body><section id=\"homeHero\">Reading your history…</section></body></html>");
        var unread = stand.File("unread.cmd", "@type \"%~dp0page.html\"\r\n");
        using var scratch = new TempFolder();
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => new PageHarness { Browsers = [unread], Scratch = scratch.Path }.Open("home", message));
        Assert.Contains("without the reading", e.Message);
        Assert.Contains("harness", e.Message);
        Assert.DoesNotContain("gave no page back", e.Message);
        Assert.Empty(Directory.GetFileSystemEntries(scratch.Path));
    }

    /// <summary>
    /// The window sends JSON, and a test's message goes into the page as it is. One that isn't JSON (a slip in the
    /// test: half a message, a file's name where its text was meant) would end the harness's own script on the page,
    /// and read as the harness's failure or the page's. It's the test's, it says which message, and no browser is
    /// started for it: the stand-in here leaves a file when it runs, and there is none.
    /// </summary>
    [Fact]
    public async Task AMessageThatIsNotJsonFailsAsTheTestsBeforeAnyBrowserStarts()
    {
        using var stand = new TempFolder();
        var started = Path.Combine(stand.Path, "started.txt");
        var browser = stand.File("marks.cmd", "@echo started > \"%~dp0started.txt\"\r\n");
        using var scratch = new TempFolder();
        var message = await OneCrawl();
        var e = Assert.Throws<PageFailed>(() => new PageHarness { Browsers = [browser], Scratch = scratch.Path }.Open("home", message, "{"));
        Assert.StartsWith("The test:", e.Message);
        Assert.Contains("message 2 of 2 isn't JSON", e.Message);
        Assert.False(File.Exists(started), "the browser was started for a message that isn't JSON");
        Assert.Empty(Directory.GetFileSystemEntries(scratch.Path));
    }

    /// <summary>
    /// A test finds a reading by its name, so two steps that read under one name are a slip in the test: one of them
    /// could never be read. It says the name, and no browser is started for it, as for a message that isn't JSON.
    /// </summary>
    [Fact]
    public async Task TwoReadingsUnderOneNameFailAsTheTestsBeforeAnyBrowserStarts()
    {
        using var stand = new TempFolder();
        var started = Path.Combine(stand.Path, "started.txt");
        var browser = stand.File("marks.cmd", "@echo started > \"%~dp0started.txt\"\r\n");
        using var scratch = new TempFolder();
        var message = await PageMessage.FromFixture();
        var e = Assert.Throws<PageFailed>(() => new PageHarness { Browsers = [browser], Scratch = scratch.Path }.Run("home",
            PageStep.Send(message), PageStep.Read("asks"), PageStep.Send(message), PageStep.Read("asks")));
        Assert.StartsWith("The test:", e.Message);
        Assert.Contains("2 steps read the page as \"asks\"", e.Message);
        Assert.False(File.Exists(started), "the browser was started for a run with two readings under one name");
        Assert.Empty(Directory.GetFileSystemEntries(scratch.Path));
    }
}

/// <summary>In a class of its own, so its wait runs beside the other page tests and not after them.</summary>
public class PageHarnessHangTests
{
    /// <summary>
    /// A browser that never finishes must not stop the gate: `finish` runs with nobody watching. The stand-in is a batch
    /// file whose child holds a file open for 30 seconds, as Edge's helpers hold the profile. After the time allowed, the
    /// test has failed and said so, the child is gone with its parent (its file can be removed), and nothing is left in
    /// the run's folder.
    /// The time allowed is also all the time Windows gets to start the batch file and its child. At 2 seconds, with 8
    /// test runs going at once, the child hadn't started in 9 runs of 24 and the test failed for that; at 5 it started
    /// in all 24. Every hourly release runs this, so a flake here costs players an hour.
    /// </summary>
    [Fact]
    public async Task ABrowserThatHangsIsStoppedWithEverythingItStartedAndFailsTheTest()
    {
        using var stand = new TempFolder();
        var held = Path.Combine(stand.Path, "held.txt");
        var hung = stand.File("hung.cmd", "@ping -n 30 127.0.0.1 > \"%~dp0held.txt\"\r\n");
        using var scratch = new TempFolder();
        var message = (await PageMessage.FromFixture()).ToJsonString();
        var harness = new PageHarness { Browsers = [hung], Timeout = TimeSpan.FromSeconds(5), Scratch = scratch.Path, Slots = new EdgeSlots("EqlPathfinderTest" + Guid.NewGuid().ToString("N"), 1, TimeSpan.FromSeconds(5)) };

        var clock = Stopwatch.StartNew();
        var e = Assert.Throws<PageFailed>(() => harness.Open("home", message));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"a browser allowed 5 seconds was waited on for {clock.Elapsed.TotalSeconds:0.0}");
        Assert.Contains("took longer than 5 seconds", e.Message);

        Assert.True(File.Exists(held), "the stand-in never started, so nothing was stopped");
        // The child holds the file for as long as it runs. A second is for Windows to let go of it, not for the child to end.
        for (var tries = 0; ; tries++)
        {
            try { File.Delete(held); break; }
            catch (IOException) when (tries < 10) { await Task.Delay(100); }
        }
        Assert.Empty(Directory.GetFileSystemEntries(scratch.Path));
    }

    /// <summary>
    /// The fuse counts from when the browser had its slot, and the browser starts only with one. Another run holds the only
    /// slot for 3 seconds; the hung stand-in is allowed 2. It must not start while the slot is held, must then fail on its
    /// own time (not "stayed taken"), and must have taken at least hold + timeout: a fuse counted from the start of the
    /// run would have gone off at 2 seconds.
    /// </summary>
    [Fact]
    public async Task AHungBrowserWaitsForItsSlotAndItsTimeoutCountsFromTheSlot()
    {
        using var stand = new TempFolder();
        var held = Path.Combine(stand.Path, "held.txt");
        var hung = stand.File("hung.cmd", "@ping -n 30 127.0.0.1 > \"%~dp0held.txt\"\r\n");
        using var scratch = new TempFolder();
        var message = (await PageMessage.FromFixture()).ToJsonString();
        var slots = new EdgeSlots("EqlPathfinderTest" + Guid.NewGuid().ToString("N"), 1, TimeSpan.FromSeconds(30));
        var harness = new PageHarness { Browsers = [hung], Timeout = TimeSpan.FromSeconds(2), Scratch = scratch.Path, Slots = slots };

        var taken = new ManualResetEventSlim();
        var other = new Thread(() => { using var _ = slots.Acquire(); taken.Set(); Thread.Sleep(3000); });
        other.Start();
        Assert.True(taken.Wait(TimeSpan.FromSeconds(5)));
        var clock = Stopwatch.StartNew();
        var seenWhileHeld = false;
        var watcher = Task.Run(() => { while (clock.Elapsed < TimeSpan.FromSeconds(2.5)) { if (File.Exists(held)) seenWhileHeld = true; Thread.Sleep(50); } });

        var e = Assert.Throws<PageFailed>(() => harness.Open("home", message));
        var elapsed = clock.Elapsed;
        await watcher;
        other.Join();

        Assert.False(seenWhileHeld, "the browser started while another run held the only slot");
        Assert.DoesNotContain("stayed taken", e.Message);
        Assert.Contains("took longer than 2 seconds", e.Message);
        Assert.Contains("had a browser slot from the start", e.Message);
        Assert.True(elapsed >= TimeSpan.FromSeconds(4.5), $"the fuse went off after {elapsed.TotalSeconds:0.0}s: it counted from the run's start, not from the slot");
        for (var tries = 0; File.Exists(held); tries++)
        {
            try { File.Delete(held); }
            catch (IOException) when (tries < 10) { await Task.Delay(100); }
        }
    }
}

/// <summary>
/// The one retry (docs/specs/page-tests-steady.md): Edge's stall under a low priority leaves no renderer process, and
/// that hang alone is run once more. A hang that keeps its renderer is a real one and stays red; a run that needs more
/// than 2 retries is red. The stand-in hangs the first time it's started and ends at once the second time.
/// </summary>
public class PageHarnessRetryTests
{
    private static readonly string Stand =
        "@if exist \"%~dp0ran.txt\" (echo ^<html^>^</html^> & exit /b 0)\r\n@echo x> \"%~dp0ran.txt\"\r\n@ping -n 30 127.0.0.1 > \"%~dp0held.txt\"\r\n";

    // What Windows says when it answered and no renderer is left: Edge's own browser process names the profile.
    private static readonly List<string> Answer = ["browser: msedge.exe --user-data-dir=profile"];

    private static async Task<(PageFailed Failed, int Starts, string Log, PageRetries Retries)> Hang(List<string> alive, int alreadyUsed = 0)
    {
        using var stand = new TempFolder();
        var hung = stand.File("hung.cmd", Stand);
        using var scratch = new TempFolder();
        var log = Path.Combine(scratch.Path, "page-retries.log");
        var retries = new PageRetries(log);
        for (var i = 0; i < alreadyUsed; i++) retries.TryTake();
        var message = (await PageMessage.FromFixture()).ToJsonString();
        var harness = new PageHarness
        {
            Browsers = [hung],
            Timeout = TimeSpan.FromSeconds(4),
            Scratch = scratch.Path,
            Retries = retries,
            Alive = _ => alive,
            Slots = new EdgeSlots("EqlPathfinderTest" + Guid.NewGuid().ToString("N"), 1, TimeSpan.FromSeconds(5)),
        };
        var e = Assert.Throws<PageFailed>(() => harness.Open("home", message));
        var starts = File.Exists(Path.Combine(stand.Path, "ran.txt")) ? 1 : 0;
        for (var tries = 0; File.Exists(Path.Combine(stand.Path, "held.txt")) && tries < 20; tries++)
        {
            try { File.Delete(Path.Combine(stand.Path, "held.txt")); } catch (IOException) { await Task.Delay(100); }
        }
        return (e, starts, File.Exists(log) ? File.ReadAllText(log) : "", retries);
    }

    [Fact]
    public async Task AHangWithNoRendererIsRetriedOnceAndTheSecondTryGetsPastTheHang()
    {
        var (e, _, log, retries) = await Hang(Answer);
        Assert.DoesNotContain("took longer", e.Message); // the second start ended at once: it got past the hang
        Assert.Contains("gave a page back without the reading", e.Message); // and only the stand-in's empty page is left to fail
        Assert.Equal(1, retries.Used);
        Assert.Contains(PageRetries.Marker, log);
        Assert.Contains("took longer than 4 seconds", log); // the whole dump
        Assert.Contains("PageHarnessRetryTests", log); // who asked
    }

    [Fact]
    public async Task AHangThatKeepsItsRendererIsNotRetried()
    {
        var (e, _, log, retries) = await Hang(["renderer: msedge.exe --type=renderer"]);
        Assert.Contains("took longer than 4 seconds", e.Message);
        Assert.Equal(0, retries.Used);
        Assert.Equal("", log);
    }

    [Theory]
    [InlineData("(could not ask Windows: x)")]
    [InlineData("(Windows was not asked in time: it took more than 3 seconds)")]
    [InlineData("")] // an empty answer while the browser's own process was still running
    public async Task AHangIsNotRetriedWhenWindowsDidNotReallyAnswer(string answer)
    {
        var (e, _, log, retries) = await Hang(answer == "" ? [] : [answer]);
        Assert.Contains("took longer than 4 seconds", e.Message);
        Assert.Equal(0, retries.Used);
        Assert.Equal("", log);
    }

    [Fact]
    public async Task WhoNamesTheTestClassAndMethodAfterAnAwait()
    {
        await Task.Yield();
        Assert.StartsWith("PageHarnessRetryTests.WhoNamesTheTestClassAndMethodAfterAnAwait (home)", PageRetries.Who("home"));
    }

    [Fact]
    public async Task AThirdRetryInOneRunTurnsItRed()
    {
        var (e, _, _, retries) = await Hang(Answer, alreadyUsed: 2);
        Assert.Contains("took longer than 4 seconds", e.Message);
        Assert.Contains("retries were used", e.Message);
        Assert.Equal(3, retries.Used);
    }
}
