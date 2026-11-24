using System.Globalization;
using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Parsing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.IO;

/// <summary>
/// Cuts a log down to the lines the engine uses, then makes them safe to check in (docs/quality.md, rule 6): no chat, no
/// guild tags, no other players' names, the character's name replaced by a stand-in, and every time moved by one amount so
/// the first line reads 12:00:00 (the same amount, so nothing measured changes). For test fixtures: nothing else should
/// carry a real log's lines.
/// </summary>
public static partial class LogScrubber
{
    /// <summary>The name a fixture's character has (the tests' <c>TestLog.Character</c>).</summary>
    public const string StandIn = "Standin";

    /// <summary>Writes the scrubbed lines to <paramref name="output"/>; returns how many were kept.</summary>
    public static int Scrub(string character, IEnumerable<string> rawLines, TextWriter output, string standIn = StandIn)
    {
        var kept = new List<string>();
        var engine = new PathfinderEngine(character, new TrackerOptions(), new NullSink());
        engine.EventLine = (raw, e) => kept.Add(Anonymize(raw, e, engine.State));
        foreach (var l in rawLines) engine.ProcessLine(l);

        var nameRx = new Regex(@"\b" + Regex.Escape(character) + @"\b");
        TimeSpan? shift = null;
        foreach (var line in kept)
        {
            var text = nameRx.Replace(line, standIn);
            if (LogLine.TryParseTime(text, out var t))
            {
                shift ??= t.Date.AddHours(12) - t;
                text = "[" + (t + shift.Value).ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture) + "]" + text[26..];
            }
            output.WriteLine(text);
        }
        return kept.Count;
    }

    private static string Anonymize(string raw, LogEvent e, EngineState s) => e switch
    {
        WhoSelf => GuildRx().Replace(raw, ""),
        InstanceInvite i => raw.Replace(i.Inviter + " has asked", "Groupmate has asked"),
        MobSlain m when !m.ByYou && m.Killer != null => raw.Replace("slain by " + m.Killer + "!", "slain by Groupmate!"),
        _ => raw,
    };

    [GeneratedRegex(@" <[^>]+>")]
    private static partial Regex GuildRx();

    private sealed class NullSink : ITrackerSink;
}
