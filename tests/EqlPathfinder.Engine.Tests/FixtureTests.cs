using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// What a checked-in log may hold (docs/quality.md, rule 6): the game's own lines about play, the made-up character
/// <see cref="TestLog.Character"/>, and a clock moved to start at noon. Never a player's name, a chat line, a guild
/// or the time someone really played. Every file in Fixtures/ is checked, so a new one is held to the same.
/// These read the checked-in folder, not the build's copy: a stale copy in bin proves nothing about what's in git.
/// </summary>
public class FixtureTests
{
    private const string Me = TestLog.Character;

    private static IReadOnlyList<string> Files([CallerFilePath] string here = "")
    {
        var files = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures")).Order(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(files);
        return files;
    }

    /// <summary>Each line without its stamp, with where it is. A line with no stamp comes whole, and no kind of line takes it.</summary>
    private static IEnumerable<(string At, string Text)> Lines(string name, IEnumerable<string> lines)
    {
        int n = 0;
        foreach (var raw in lines)
        {
            n++;
            var m = Stamp.Match(raw);
            yield return ($"{name} line {n}", m.Success ? raw[m.Length..] : raw);
        }
    }

    private static readonly Regex Stamp = new(@"^\[[A-Z][a-z]{2} [A-Z][a-z]{2} [ \d]\d \d\d:\d\d:\d\d \d{4}\] ");

    private static void NothingWrong(List<string> problems) =>
        Assert.True(problems.Count == 0, $"{problems.Count} in all. The first:\n" + string.Join("\n", problems.Take(8)));

    // ------------------------------------------------------------------ the file's name

    [Fact]
    public void AFixturesFileIsNamedForTheStandIn()
    {
        var name = new Regex($@"^eqlog_{Me}_[a-z]+_[a-z0-9-]+\.txt$");
        NothingWrong(Files().Select(f => Path.GetFileName(f)).Where(f => !name.IsMatch(f))
            .Select(f => $"{f}: a fixture is eqlog_{Me}_<server>_<what it holds>.txt").ToList());
    }

    // ------------------------------------------------------------------ names

    /// <summary>
    /// Names the game made that read like a player's by their capitals: a mob named in one word, or whose name ends
    /// (or, at the start of a line, begins) with a lone capitalised word. Each was checked to be a mob when it was
    /// added. A name that fails below goes here only if it's one of these; a player's is replaced in the fixture.
    /// </summary>
    private static readonly string[] MobsThatReadLikeAPlayer =
    [
        "Abomination of Ro", "Soldier of V`Zher", "Footman of V`Zher", "undead knight of Unrest",
        "ice boned skeleton", "reclusive ghoul magus", "lesser blade fiend",
        "Innoruuk, the Prince of Hate", "Innoruuk`s Chosen", "Targin the Rock",
    ];

    private static readonly HashSet<string> NotAName = ["You", "Your", "A", "An", "The", "Groupmate"];

    /// <summary>The places a line puts a player's or a pet's name. "`s" is the game's own mark for whose pet it is ("X`s warder").</summary>
    private static readonly (Regex Rx, bool OnlyMe, string Where)[] NameSlots =
    [
        (new(@"^(?<n>[A-Z][a-z]+)(?<pet>`s [a-z]+)? [a-z]"), false, "who the line is about"),
        (new(@"(?<=\b[a-z]+ )(?<n>[A-Z][a-z]+)(?<pet>`s [a-z]+)? for \d+ points? of "), false, "who was hit"),
        (new(@"slain by (?<n>[A-Z][a-z]+)(?<pet>`s [a-z]+)?!$"), false, "who made the kill"),
        (new(@"\bhealed (?<n>[A-Z][a-z]+)\b(?<pet>`s [a-z]+)?"), false, "who was healed"),
        (new(@"^Player (?<n>\S+) creating instance"), true, "whose instance"),
        (new(@"^\[[^\]]*\] (?<n>[^\s(]+) \("), true, "whose /who line"),
        (new(@"(?<n>[A-Z][a-z]+)`s "), true, "whose pet"),
    ];

    /// <summary>
    /// The page data built from a fixture shows its names (the fight meter's rows, the crawl's kills), and the next
    /// thing built from it is a page test. So wherever the game writes who did something, it's the stand-in, one of
    /// the stand-in's own pets (it said "Attacking ... Master." to you in the same log) or the scrubber's "Groupmate".
    /// </summary>
    [Fact]
    public void AFixtureNamesNoPlayerButTheStandIn() => NothingWrong(EveryFixture(NameProblems));

    private static List<string> EveryFixture(Func<string, IReadOnlyList<string>, List<string>> check) =>
        Files().SelectMany(f => check(Path.GetFileName(f), File.ReadAllLines(f))).ToList();

    /// <summary>What a fixture (or a scrubber's output, named <paramref name="name"/>) names that it shouldn't.</summary>
    internal static List<string> NameProblems(string name, IReadOnlyList<string> raw)
    {
        var problems = new List<string>();
        {
            var lines = Lines(name, raw).ToList();
            var pets = lines.Select(l => Regex.Match(l.Text, @"^(?<n>[A-Z][a-z]+) told you, 'Attacking .+ Master\.'$"))
                .Where(m => m.Success).Select(m => m.Groups["n"].Value).ToHashSet();
            foreach (var (at, text) in lines)
            {
                var line = MobsThatReadLikeAPlayer.Aggregate(text, (t, mob) => t.Replace(mob, "a mob", StringComparison.OrdinalIgnoreCase));
                foreach (var (rx, onlyMe, where) in NameSlots)
                    foreach (Match m in rx.Matches(line))
                    {
                        var n = m.Groups["n"].Value;
                        bool someonesPet = m.Groups["pet"].Success;
                        bool ok = n == Me || (!onlyMe && !someonesPet && (pets.Contains(n) || NotAName.Contains(n)));
                        if (!ok) problems.Add($"{at}: \"{n}\" ({where}) is not {Me} or one of {Me}'s pets: {text}");
                    }
            }
        }
        return problems;
    }

    // ------------------------------------------------------------------ chat, guilds, and lines nobody has looked at

    /// <summary>
    /// Every kind of line a fixture has held, each looked at for what it can carry. A new kind fails here until it's
    /// added, with its names' places in <see cref="NameSlots"/>: that's the moment to ask what a player wrote in it.
    /// </summary>
    private static readonly Regex[] KindsOfLine =
    [
        new(@"^.+ for \d+ points? of damage\.( \([A-Za-z ]+\))*$"),                                   // a hit
        new(@"^.+ for \d+ points? of [a-z]+ damage by .+\.( \([A-Za-z ]+\))*$"),                      // a spell's hit
        new(@"^.+ for \d+ points? of non-melee damage[.!]$"),                                         // a damage shield, a proc
        new(@"^.+ ha(s|ve) taken \d+ damage from .+\.$"),                                             // a spell over time
        new(@"^.+ told you, 'Attacking .+ Master\.'$"),                                               // your pet, the only speech
        new(@"^You gain (party )?experience( \(with a bonus\))?! \([\d.]+%\)$"),
        new(@"^You gained reward experience from the Dungeon Crawl!$"),
        new(@"^You have gained a level! Welcome to level \d+!$"),
        new(@"^You have slain .+!$"),
        new(@"^.+ has been slain by .+!$"),
        new(@"^You looted (an?|\d+) (Mote|Fragment)s? of ([A-Za-z]+ )?Potential from .+ and stored it in your currency$"),
        new(@"^You have entered [^<>]+\.$"),
        new(@"^Player \S+ creating instance .+ \d+\.$"),
        new(@"^\[\d+ [A-Z]{3}(/[A-Z]{3})*\] \S+ \([A-Za-z ]+\) ZONE: [^<>]+$"),                       // your /who line, with no guild
        new(@"^You begin casting [A-Za-z`' ]+\.$"),                                                   // your own spell
        new(@"^.+ has taken \d+ damage from your .+\.( \([A-Za-z ]+\))*$"),                           // your spell over time
        new(@"^You have been given: [A-Za-z\- ]+$"),                                                  // a quest NPC's gift
        new(@"^You offered \d+ Fragments? of [A-Za-z ]+ to [A-Za-z ]+\.$"),                           // a hand-in
        new(@"^Groupmate has asked you to join the instance: [^<>]+\.$"),                             // an invite, the scrubber's stand-in for the inviter
        new(@"^You have completed the Dungeon Crawl and earned reward loot!$"),
        new(@"^You earned a refund of your instance charge\.$"),
    ];

    [Fact]
    public void AFixtureHoldsOnlyWhatTheGameWroteAboutPlay() => NothingWrong(EveryFixture(KindProblems));

    internal static List<string> KindProblems(string name, IReadOnlyList<string> raw)
    {
        var problems = new List<string>();
        foreach (var (at, text) in Lines(name, raw))
        {
            if (text.Contains('<') || text.Contains('>')) problems.Add($"{at}: a guild tag, or something else in angle brackets: {text}");
            else if (text.Contains(", '") && !KindsOfLine[4].IsMatch(text)) problems.Add($"{at}: somebody's words: {text}");
            else if (!KindsOfLine.Any(k => k.IsMatch(text))) problems.Add($"{at}: a kind of line no fixture has held (KindsOfLine): {text}");
        }
        return problems;
    }

    // ------------------------------------------------------------------ the clock

    /// <summary>
    /// A log's times say when someone was at the keyboard. A fixture's are moved, all by the same amount so nothing
    /// measured changes, until its first line reads 12:00:00: an excerpt dropped in as it was cut doesn't start there.
    /// </summary>
    [Fact]
    public void AFixturesClockStartsAtNoon() => NothingWrong(EveryFixture(ClockProblems));

    internal static List<string> ClockProblems(string name, IReadOnlyList<string> raw)
    {
        var problems = new List<string>();
        {
            var stamps = raw.Select(l => LogLine.TryParseTime(l, out var t) ? t : (DateTime?)null).ToList();
            if (stamps.Count == 0 || stamps[0] == null) { problems.Add($"{name}: no time on the first line"); return problems; }
            if (stamps[0]!.Value.TimeOfDay != TimeSpan.FromHours(12)) problems.Add($"{name}: starts at {stamps[0]:HH:mm:ss}, not 12:00:00");
            // The reader doesn't look at the weekday, so nothing else would notice a moved date that kept its old one.
            int bad = stamps.FindIndex(s => s == null);
            if (bad < 0) bad = Enumerable.Range(0, raw.Count).FirstOrDefault(i => raw[i].Substring(1, 3) != stamps[i]!.Value.ToString("ddd", CultureInfo.InvariantCulture), -1);
            if (bad >= 0) problems.Add($"{name} line {bad + 1}: not a time the game would write (the weekday goes with the date): {raw[bad][..Math.Min(26, raw[bad].Length)]}");
            for (int i = 1; i < stamps.Count; i++)
                if (stamps[i] < stamps[i - 1]) { problems.Add($"{name} line {i + 1}: goes back in time"); break; }
        }
        return problems;
    }
}
