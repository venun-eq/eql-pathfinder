using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// cap-fixture: <c>scrub</c> makes a log that may be checked in (docs/quality.md, rule 6): the stand-in's name for the
/// character's, the guild and the chat gone, every time moved by one amount so the first line reads 12:00:00. A made-up
/// raw log goes in and the result goes through the same checks every fixture is held to (<see cref="FixtureTests"/>).
/// </summary>
public class LogScrubberTests
{
    private const string Real = "Realname";

    private static readonly string[] Raw =
    [
        "[Sat Nov 14 21:30:07 2026] Realname tells the guild, 'who wants a raid tonight'",
        "[Sat Nov 14 21:30:10 2026] You have entered The Oasis of Marr.",
        "[Sat Nov 14 21:30:12 2026] [50 MAG/NEC/BST] Realname (Erudite) <The Real Guild> ZONE: The Oasis of Marr",
        "[Sat Nov 14 21:30:20 2026] Otherguy has asked you to join the instance: Nagafen's Lair.",
        "[Sat Nov 14 21:30:30 2026] Player Realname creating instance Nagafen's Lair 12.",
        "[Sat Nov 14 21:30:50 2026] You have entered Nagafen's Lair 4 (Refined).",
        "[Sat Nov 14 21:31:00 2026] Realname`s warder told you, 'Attacking a ghoul Master.'",
        "[Sat Nov 14 21:31:01 2026] You slash a ghoul for 50 points of damage.",
        "[Sat Nov 14 21:31:02 2026] A ghoul has been slain by Otherguy!",
        "[Sat Nov 14 21:31:05 2026] Otherguy tells you, 'nice one'",
        "[Sun Nov 15 00:10:10 2026] You have slain a ghoul!",   // past midnight: the date and weekday move with the time
    ];

    private static string[] Scrubbed(out int kept)
    {
        var w = new StringWriter();
        kept = LogScrubber.Scrub(Real, Raw, w);
        return w.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void AScrubbedLogHasNoRealNameGuildOrChat()
    {
        var lines = Scrubbed(out int kept);
        Assert.Equal(lines.Length, kept);
        var all = string.Join("\n", lines);
        Assert.DoesNotContain(Real, all);
        Assert.DoesNotContain("The Real Guild", all);
        Assert.DoesNotContain("Otherguy", all);
        Assert.DoesNotContain("tells", all);
        Assert.Contains($"Player {TestLog.Character} creating instance Nagafen's Lair 12.", all);
        Assert.Contains($"{TestLog.Character}`s warder told you", all);
        Assert.Contains("A ghoul has been slain by Groupmate!", all);
    }

    [Fact]
    public void OnlyTheWholeNameIsReplaced()
    {
        // A mob whose name has the character's inside it keeps its name: the scrubber matches whole words only.
        var w = new StringWriter();
        LogScrubber.Scrub(Real, ["[Sat Nov 14 21:30:10 2026] You have entered The Oasis of Marr.",
            "[Sat Nov 14 21:30:20 2026] You slash Realnameson for 50 points of damage."], w);
        Assert.Contains("You slash Realnameson for 50 points of damage.", w.ToString());
    }

    [Fact]
    public void ScrubbedTimesAreAllMovedByTheSameAmountToStartAtNoon()
    {
        var lines = Scrubbed(out _);
        // The first kept line is 21:30:10, so everything moves back 9:30:10, and a line is where it was relative to that one.
        Assert.StartsWith("[Sat Nov 14 12:00:00 2026] You have entered The Oasis of Marr.", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("[Sat Nov 14 12:00:20 2026] Player "));      // 21:30:30
        Assert.Contains(lines, l => l.StartsWith("[Sat Nov 14 14:40:00 2026] You have slain")); // 00:10:10 on the Sunday: 9:30:10 less is Saturday's 14:40:00
    }

    [Fact]
    public void AScrubbedLogPassesTheChecksEveryFixtureIsHeldTo()
    {
        var lines = Scrubbed(out _);
        const string name = "scrubbed.txt";
        var problems = FixtureTests.NameProblems(name, lines).Concat(FixtureTests.KindProblems(name, lines)).Concat(FixtureTests.ClockProblems(name, lines)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
