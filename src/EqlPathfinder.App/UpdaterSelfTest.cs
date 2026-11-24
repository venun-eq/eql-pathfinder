using System.IO;
using System.Text.Json;
using Velopack.Locators;

namespace EqlPathfinder.App;

/// <summary>
/// Developer check of the beta channel rules (EQLPathfinder.exe --updater-test &lt;report file&gt;): hand-made feeds in a
/// temp folder, and Velopack's test locator standing in for an installed copy at a given version. Only the choice of
/// release is checked; nothing is downloaded.
/// </summary>
internal static class UpdaterSelfTest
{
    private sealed record Case(string Name, string Current, bool Beta, string[] Stable, string[]? BetaFeed, string? Expect);

    private static readonly Case[] Cases =
    [
        new("regular copy ignores the beta", "0.8.1", false, ["0.8.1"], ["0.9.0-beta.1"], null),
        new("joining the beta finds it", "0.8.1", true, ["0.8.1"], ["0.9.0-beta.1"], "0.9.0-beta.1@beta"),
        new("left the beta: no step back to an older regular release", "0.9.0-beta.1", false, ["0.8.2"], ["0.9.0-beta.1"], null),
        new("left the beta: the regular release that passes it", "0.9.0-beta.1", false, ["0.9.0"], ["0.9.0-beta.1"], "0.9.0@win"),
        new("in the beta: a regular release newer than the beta wins", "0.9.0-beta.1", true, ["0.9.0"], ["0.9.0-beta.1"], "0.9.0@win"),
        new("in the beta: 0.8.2 beats 0.8.2-beta.1", "0.8.1", true, ["0.8.2"], ["0.8.2-beta.1"], "0.8.2@win"),
        new("in the beta: the next beta", "0.9.0-beta.1", true, ["0.8.2"], ["0.9.0-beta.1", "0.9.0-beta.2"], "0.9.0-beta.2@beta"),
        new("in the beta before any beta is published", "0.8.0", true, ["0.8.1"], null, "0.8.1@win"),
        new("regular update as before", "0.7.5", false, ["0.7.5", "0.8.1"], null, "0.8.1@win"),
    ];

    public static void Run(string reportPath)
    {
        var lines = new List<string>();
        int failed = 0;
        foreach (var c in Cases)
        {
            var dir = Path.Combine(Path.GetTempPath(), "eqlp-updater-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            try
            {
                Feed(dir, "win", c.Stable);
                if (c.BetaFeed != null) Feed(dir, "beta", c.BetaFeed);
                var packages = Directory.CreateDirectory(Path.Combine(dir, "installed")).FullName;
                var updater = new Updater(dir, new TestVelopackLocator("EQLPathfinder", c.Current, packages, null)) { Beta = c.Beta };
                var found = updater.FindAsync().GetAwaiter().GetResult();
                string? got = found is var (info, from) ? $"{info.TargetFullRelease.Version}@{(ReferenceEquals(from, null) ? "?" : Channel(dir, info.TargetFullRelease.Version.ToString()))}" : null;
                bool ok = got == c.Expect;
                if (!ok) failed++;
                lines.Add($"{(ok ? "ok  " : "FAIL")} {c.Name}: on {c.Current}, beta {(c.Beta ? "on" : "off")} -> {got ?? "stay"} (expected {c.Expect ?? "stay"})");
            }
            catch (Exception ex)
            {
                failed++;
                lines.Add($"FAIL {c.Name}: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }
        lines.Add(failed == 0 ? $"all {Cases.Length} passed" : $"{failed} of {Cases.Length} failed");
        File.WriteAllLines(reportPath, lines);
    }

    private static readonly Dictionary<string, string> Channels = new();

    private static string Channel(string dir, string version) => Channels.GetValueOrDefault(dir + "|" + version, "?");

    private static void Feed(string dir, string channel, string[] versions)
    {
        var assets = versions.Select(v =>
        {
            Channels[dir + "|" + v] = channel;
            return new
            {
                PackageId = "EQLPathfinder",
                Version = v,
                Type = "Full",
                FileName = $"EQLPathfinder-{v}-full.nupkg",
                SHA1 = new string('0', 40),
                SHA256 = new string('0', 64),
                Size = 1L,
                NotesMarkdown = "",
                NotesHTML = "",
            };
        });
        File.WriteAllText(Path.Combine(dir, $"releases.{channel}.json"), JsonSerializer.Serialize(new { Assets = assets }));
    }
}
