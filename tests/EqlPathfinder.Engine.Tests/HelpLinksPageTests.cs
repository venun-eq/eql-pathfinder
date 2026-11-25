using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Help links each setting it names, and its words point at things that are there (docs/specs/help-and-wording.md).</summary>
public sealed class HelpLinkPages : IAsyncLifetime
{
    internal PageRun Run { get; private set; } = null!;

    /// <summary>Each Help link by the setting it opens: its selector, and the reading after a click.</summary>
    internal static readonly (string Name, string Selector)[] Links =
    [
        ("sharing", "#page-help button.link[data-go=\"settings\"][data-anchor=\"shareCard\"]"),
        ("fragment", "#page-help button.link[data-go=\"settings\"][data-anchor=\"fragmentSetting\"]"),
        ("fragmentCard", "#page-help button.link[data-go=\"help\"][data-anchor=\"help-fragment\"]"),
        ("howYouPlay", "#page-help button.link[data-go=\"settings\"][data-anchor=\"howYouPlay\"]"),
        ("farmRate", "#page-help button.link[data-go=\"settings\"][data-anchor=\"farmRateSetting\"]"),
        ("meter", "#page-help button.link[data-go=\"settings\"][data-anchor=\"meterSettings\"]"),
        ("roster", "#page-help button.link[data-go=\"roster\"]"),
    ];

    public async Task InitializeAsync()
    {
        var m = await PageMessage.FromFixture();
        var steps = new List<PageStep> { PageStep.Send(m), PageStep.Read("help") };
        foreach (var (name, selector) in Links)
            steps.AddRange([PageStep.Click(selector), PageStep.Read(name), PageStep.Click("button[data-page=\"help\"]")]);
        var opened = await PageRuns.AtOnce(h => h.Run("help", steps.ToArray()));
        Run = opened.Runs[0];
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class HelpLinksPageTests(HelpLinkPages pages) : IClassFixture<HelpLinkPages>
{
    [Theory]
    [InlineData("sharing", "settings", "shareCard")]
    [InlineData("fragment", "settings", "fragmentSetting")]
    [InlineData("howYouPlay", "settings", "howYouPlay")]
    [InlineData("farmRate", "settings", "farmRateSetting")]
    [InlineData("meter", "settings", "meterSettings")]
    [InlineData("roster", "roster", "page-roster")]
    [InlineData("fragmentCard", "help", "help-fragment")]
    public void ALinkOpensItsPageWithItsSpotThere(string link, string page, string spot)
    {
        var at = pages.Run.At(link);
        Assert.Equal(page, at.Page);
        Assert.NotEqual("", at.Text(spot));
    }

    [Fact]
    public void TheFragmentSpotIsTheFragmentRow() => Assert.Contains("What's a fragment worth?", pages.Run.At("fragment").Text("fragmentSetting"));

    [Fact]
    public void TheMeterSpotStartsAtShowTheFightMeter() => Assert.Contains("Show the fight meter", pages.Run.At("meter").Text("meterSettings"));

    [Fact]
    public void TheMotesCardSaysWhereToPickAndWhichToPick()
    {
        var t = pages.Run.At("help").Text("page-help");
        Assert.Contains("T5-eq counts a fragment at the value you pick in Settings → Counting; the next card, What's a fragment worth?, says which to pick.", t);
        Assert.DoesNotContain("What's a fragment worth?, below", t);
    }

    [Fact]
    public void HelpLeavesOutWordsThatPointAtNothing()
    {
        var t = pages.Run.At("help").Text("page-help");
        Assert.DoesNotContain("Slower place", t);
        Assert.Contains("Kedge Keep levels faster", t);
        Assert.DoesNotContain("before that rule", t);
        Assert.DoesNotContain("Fastest or Counting motes", t);
        Assert.DoesNotContain("Fastest or Counting motes", File.ReadAllText(Path.Combine(Web(), "js", "core.js")));
        Assert.DoesNotContain("numbers (they can't", t);
    }

    /// <summary>Every mention of a setting in Help is a link: with the links taken out, none is left.</summary>
    [Fact]
    public void HelpNamesNoSettingWithoutALink()
    {
        var html = File.ReadAllText(Path.Combine(Web(), "index.html"));
        var help = html[html.IndexOf("id=\"page-help\"", StringComparison.Ordinal)..html.IndexOf("id=\"page-settings\"", StringComparison.Ordinal)];
        var bare = Regex.Replace(help, "<button class=\"link\".*?</button>", "", RegexOptions.Singleline);
        Assert.DoesNotMatch("Settings →|Plan → Roster", bare);
    }

    private static string Web([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "EqlPathfinder.App", "web"));
}

public class FarmLineDecimalsPageTests
{
    [Fact]
    public async Task AFarmNumberUnderOneShowsADecimalNeverZero()
    {
        var m = (await PageMessage.FromFixture()).With("reinc.farmSource.kind", "default").With("reinc.farmSource.crawls", 3);
        m["reinc"]!["farm"] = new JsonObject
        {
            ["crawls"] = 3,
            ["usual"] = 0.27,
            ["best"] = 0.01,
            ["bestZone"] = "Kedge Keep",
            ["preCrawls"] = 2,
            ["preUsual"] = 0.27,
            ["preBest"] = 1.2,
            ["preBestZone"] = "Kedge Keep",
            ["difficulties"] = new JsonArray(3, 4),
        };
        var opened = await PageRuns.AtOnce(h => h.Run("settings", PageStep.Send(m), PageStep.Read("farm")));
        var t = opened.Runs[0].At("farm").Text("farmRateSetting");
        Assert.Matches(@"Before it: typical 0\.3, best Kedge Keep 1\.(\s|$)", t);
        Assert.Contains("typical 0.3 over 3 crawls, best Kedge Keep under 0.1", t);
        Assert.DoesNotMatch(@"\b0\.0\b|typical 0(\.(\s|$)|[ ,])", t);
    }

    [Fact]
    public async Task ANumberJustUnderOneReadsOneNotOnePointZero()
    {
        var m = (await PageMessage.FromFixture()).With("reinc.farmSource.kind", "default").With("reinc.farmSource.crawls", 3);
        m["reinc"]!["farm"] = new JsonObject
        {
            ["crawls"] = 3,
            ["usual"] = 0.96,
            ["best"] = 0.96,
            ["bestZone"] = "Kedge Keep",
            ["difficulties"] = new JsonArray(3, 4),
        };
        var opened = await PageRuns.AtOnce(h => h.Run("settings", PageStep.Send(m), PageStep.Read("farm")));
        var t = opened.Runs[0].At("farm").Text("farmRateSetting");
        Assert.Matches(@"typical 1 over 3 crawls, best Kedge Keep 1(\s|\.|,|$)", t);
        Assert.DoesNotContain("1.0", t);
    }
}
