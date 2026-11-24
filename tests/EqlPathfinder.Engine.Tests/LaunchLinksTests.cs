using System.Runtime.CompilerServices;

namespace EqlPathfinder.Engine.Tests;

/// <summary>docs/specs/launch-links.md: the GitHub, Discord and website links on the site, in the app and in README, read as text.</summary>
public class LaunchLinksTests
{
    private static string Repo([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Repo(), .. parts]));

    private const string GitHub = "https://eqlpathfinder.com/github";
    private const string Discord = "https://eqlpathfinder.com/discord";
    private const string Website = "https://eqlpathfinder.com";

    [Fact]
    public void EverySitePageHeaderHasGitHubAndDiscordWithMarks()
    {
        var header = Read("site", "partials", "header.html");
        Assert.Matches(@"<a[^>]*href=""/github""[^>]*aria-label=""GitHub""", header);
        Assert.Matches(@"<a[^>]*href=""/discord""[^>]*aria-label=""Discord""", header);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(header, "<svg").Count - 1); // the brand's mark, then one each
        Assert.Contains("class=\"off\"", header);
    }

    [Fact]
    public void EverySitePageFooterHasTheLinksAndWhoMadeIt()
    {
        var footer = Read("site", "partials", "footer.html");
        Assert.Contains("href=\"/github\"", footer);
        Assert.Contains("href=\"/discord\"", footer);
        Assert.Contains("Made by Venun. Free and open source under the MIT license.", footer);
    }

    [Fact]
    public void TheHomePageSaysOpenSourceAndExplainsTheWindowsBox()
    {
        var home = Read("site", "index.html");
        Assert.Contains("Free · Open source · Windows 10 and 11 · Signed installer, updates itself", home);
        Assert.Contains("href=\"#windows-warning\"", home);
        Assert.Contains("Why it shows", home);
        Assert.Contains("id=\"windows-warning\"", home);
        Assert.Contains("Windows may warn you the first time.", home);
        Assert.Contains("Click <b>More info</b>", home);
        Assert.Contains("Click <b>Run anyway</b>", home);
        Assert.Contains("the developer's name", home);
        Assert.Contains("role=\"img\"", home);
        Assert.Contains("what to click", home);
        Assert.Contains("Source on GitHub", home);
        // Trust: four cards, Open source among them, the third reworded.
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(home, "<div class=\"card\">").Count);
        Assert.Contains("<h3>Open source</h3>", home);
        Assert.Contains("signed with a publisher Microsoft has verified", home);
        Assert.DoesNotContain("signed by a verified publisher", home);
    }

    [Fact]
    public void TheCertificatesNameIsNeverPrinted()
    {
        foreach (var file in new[] { Path.Combine("site", "index.html"), Path.Combine("site", "partials", "footer.html"), "README.md" })
        {
            var text = Read(file);
            Assert.DoesNotContain("CN=", text);
            Assert.DoesNotContain("Subject", text);
        }
        foreach (var f in Directory.GetFiles(Path.Combine(Repo(), "src", "EqlPathfinder.App", "web"), "*.html"))
            Assert.DoesNotContain("CN=", File.ReadAllText(f));
    }

    [Fact]
    public void TheRedirectsKeepTheRestOfTheGitHubAddress_AndDiscordRedirectsToTheInvite()
    {
        var redirects = Read("site", "public", "_redirects");
        Assert.Matches(@"(?m)^/github\s+https://github\.com/venun-eq/eql-pathfinder\s+302\s*$", redirects);
        Assert.Matches(@"(?m)^/github/\*\s+https://github\.com/venun-eq/eql-pathfinder/:splat\s+302\s*$", redirects);
        Assert.Matches(@"(?m)^/discord/?\s+https://discord\.gg/\S+\s+302\s*$", redirects);
        Assert.False(File.Exists(Path.Combine(Repo(), "site", "discord.html")));
        Assert.DoesNotContain("discord.html", Read("site", "vite.config.ts"));
        var pages = Directory.GetFiles(Path.Combine(Repo(), "site"), "*.html").Concat(Directory.GetFiles(Path.Combine(Repo(), "src", "EqlPathfinder.App", "web"), "*.html", SearchOption.AllDirectories));
        foreach (var f in pages)
            Assert.DoesNotContain("opens soon", File.ReadAllText(f));
    }

    [Fact]
    public void TheAppOpensOnlyEqlpathfinderAddresses()
    {
        var web = Path.Combine(Repo(), "src", "EqlPathfinder.App");
        var help = Read("src", "EqlPathfinder.App", "web", "index.html");
        Assert.Contains("Pathfinder online", help);
        foreach (var url in new[] { Discord, GitHub, Website })
            Assert.Contains($"href=\"{url}\"", help);
        Assert.Contains("target=\"_blank\"", help);

        var settings = Read("src", "EqlPathfinder.App", "web", "js", "settings.js");
        Assert.Contains("Open source", settings);
        Assert.Contains("MIT license. Made by Venun.", settings);
        foreach (var url in new[] { Discord, GitHub, Website })
            Assert.Contains(url, settings);

        var tour = Read("src", "EqlPathfinder.App", "web", "welcome.html");
        Assert.Contains("Join the", tour);
        Assert.Contains(Discord, tour);
        Assert.Contains(GitHub, tour);

        var tray = Read("src", "EqlPathfinder.App", "TrayIcon.cs");
        Assert.Contains("Pathfinder online", tray);
        Assert.Contains("Source on GitHub", tray);
        foreach (var url in new[] { Discord, GitHub, Website })
            Assert.Contains(url, tray);

        foreach (var f in Directory.EnumerateFiles(web, "*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".html") || f.EndsWith(".js") || f.EndsWith(".cs")) && !f.Contains(@"\bin\") && !f.Contains(@"\obj\")))
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("discord.gg", text);
            Assert.DoesNotContain("github.com/venun", text);
        }
    }

    [Fact]
    public void ReadmeLinksDiscordAndExplainsTheWindowsBox()
    {
        var readme = Read("README.md");
        Assert.Matches(@"\[Privacy\]\(https://eqlpathfinder\.com/privacy\)\s*·\s*\[Discord\]\(https://eqlpathfinder\.com/discord\)", readme);
        Assert.Contains("Windows protected your PC", readme);
        Assert.Contains("https://eqlpathfinder.com/#windows-warning", readme);
        Assert.Contains("[GitHub issues](https://eqlpathfinder.com/github/issues)", readme);
    }
}
