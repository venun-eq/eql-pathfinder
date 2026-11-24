namespace EqlPathfinder.Engine.Tests;

/// <summary>What a fixture's runs came back as, with what they left in the folder they were given and the page's own files before and after them.</summary>
internal sealed record OpenedPages(IReadOnlyList<PageRun> Runs, IReadOnlyList<string> LeftBehind, IReadOnlyList<string> WebBefore, IReadOnlyList<string> WebAfter);

/// <summary>
/// How a page's fixture opens its runs (docs/quality.md): all at once, one start of the browser each, in a scratch
/// folder of their own that's gone afterwards. One after another a start costs about half a second; at once, each one
/// past the first costs about 0.2 (docs/specs/page-test-harness.md). Every page's fixture opens its runs through here.
/// </summary>
internal static class PageRuns
{
    public static async Task<OpenedPages> AtOnce(params Func<PageHarness, PageRun>[] runs)
    {
        var scratch = Path.Combine(Path.GetTempPath(), "pf-pages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var harness = new PageHarness { Scratch = scratch };
            var before = Listing(harness.Web);
            // Each browser has its own profile: none waits for another's lock.
            var opened = await Task.WhenAll(runs.Select(run => Task.Run(() => run(harness))));
            return new OpenedPages(opened, Directory.GetFileSystemEntries(scratch).Select(Path.GetFileName).ToList()!, before, Listing(harness.Web));
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch (IOException) { }
        }
    }

    /// <summary>Every file under a folder with its size and when it was last written: a file added, changed or removed shows.</summary>
    internal static IReadOnlyList<string> Listing(string dir) =>
        Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => $"{Path.GetRelativePath(dir, f)} {new FileInfo(f).Length} {File.GetLastWriteTimeUtc(f):O}").ToList();
}

/// <summary>What the page tests read from a run that every page's tests need.</summary>
internal static class PageRunReads
{
    /// <summary>What the page posted to the window with this type, in order.</summary>
    public static IReadOnlyList<System.Text.Json.Nodes.JsonObject> PostedAs(this PageRun run, string type) =>
        run.Posted.Where(p => (string?)p["type"] == type).ToList();

    /// <summary>
    /// Roster with its count entry open, where any "Set counts" leads from any page: the entry's own banner with Done,
    /// every tile ready to tap, and nothing sent to the window until a tile is tapped.
    /// </summary>
    public static void IsRosterWithItsCountsOpen(PageRun run, string from)
    {
        Assert.Equal("roster", run.Page);
        var banner = run.Text("countsBanner");
        Assert.Contains("Set how many times each has reincarnated so far", banner);
        Assert.Contains("Done", banner);
        Assert.DoesNotContain("Your log shows no reincarnations", banner);
        Assert.Contains("tap to add", run.Text("classTiles"));
        Assert.Contains("tap to add", run.Text("raceTiles"));
        Assert.Empty(run.PostedAs("counts"));
        if (from != "roster")
        {
            Assert.Equal("", run.Text("page-" + from));   // the page the click came from is no longer shown
            Assert.Single(run.PostedAs("seen"), p => (string?)p["page"] == "roster");
        }
    }
}
