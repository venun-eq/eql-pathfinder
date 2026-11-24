using EqlPathfinder.Engine.IO;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The checked-in logs (Fixtures/) and the made-up character they belong to. No test names a fixture's file itself:
/// it asks here, so the stand-in's name is written once. <see cref="FixtureTests"/> holds what a fixture may contain.
/// </summary>
internal static class TestLog
{
    /// <summary>The character every fixture is about. Made up: nobody's name goes into a checked-in log (docs/quality.md, rule 6).</summary>
    public const string Character = "Standin";
    public const string Server = "oggok";

    /// <summary>The name the game would give this character's live log, for a test's copy in a temp folder.</summary>
    public static string LiveName => $"eqlog_{Character}_{Server}.txt";

    /// <summary>Four Befallen crawls and one in Unrest, level 2 to 23 (MAG/NEC/BST Erudite): GoldenTests has its hand-checked numbers.</summary>
    public static string BefallenUnrest => Find("befallen-unrest");

    /// <summary>A stretch at level 50: a raid in the Plane of Hate, Nagafen's Lair at the cap (a finished crawl with a groupmate's and a pet's kills) and open-world play; CapGoldenTests has its hand-checked numbers.</summary>
    public static string CapNagafen => Find("cap-nagafen");

    /// <summary>The character's logs when <paramref name="live"/> is the only file.</summary>
    public static CharacterLogs Logs(string live) => new(Character, Server, live, [live]);

    /// <summary>What the store files this character's rows under.</summary>
    public static string Key => Logs("").Key;

    private static string Find(string what)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", $"eqlog_{Character}_{Server}_{what}.txt");
        Assert.True(File.Exists(path), $"no fixture Fixtures/{Path.GetFileName(path)}: every checked-in log is named for {Character}");
        return path;
    }
}
