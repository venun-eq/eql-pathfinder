using System.Globalization;
using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Combat;

/// <summary>Someone else's hit: a group member, their pet, or a mob hitting one of them.</summary>
public sealed record OtherDamage(DateTime Time, string Actor, string Target, int Amount, string? Ability, bool Critical) : LogEvent(Time);

/// <summary>A heal. <see cref="Actor"/> is "You" for yours; <see cref="Target"/> is "you" when it landed on you.</summary>
public sealed record HealLanded(DateTime Time, string Actor, string Target, int Amount, string Ability) : LogEvent(Time);

/// <summary>A mob's spell hitting you ("a gnoll hit you for 252 points of magic damage by Shock of Spikes.").</summary>
public sealed record SpellHitYou(DateTime Time, string From, int Amount, string Ability) : LogEvent(Time);

/// <summary>
/// A swing that did no damage (a miss, dodge, parry, riposte or block) or a resisted spell: no numbers, but it shows the
/// fight is on. <see cref="Actor"/> is "You" for yours; <see cref="Target"/> is "YOU" when it was at you.
/// </summary>
public sealed record Swing(DateTime Time, string Actor, string Target) : LogEvent(Time);

/// <summary>
/// The fight meter's extra lines, tried only when <see cref="LineParser"/> found nothing: other fighters' damage and
/// every heal. Formats observed 2026-11 (docs/log-formats.md).
/// </summary>
public static partial class CombatParser
{
    private const StringComparison O = StringComparison.Ordinal;
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public static LogEvent? Parse(in LogLine line)
    {
        var b = line.Body;
        if (b.Length < 20 || b.Contains(", '", O)) return null;   // chat
        if (b.Contains(" points of ", O) || b.Contains(" point of ", O))
        {
            if (b.Contains(" damage by ", O))
            {
                var m = SpellRx().Match(b);
                if (!m.Success) return null;
                string actor = LineParser.NormalizeMob(m.Groups["a"].Value), target = m.Groups["t"].Value;
                var spell = LineParser.StripRank(m.Groups["s"].Value);
                if (target is "you" or "YOU") return new SpellHitYou(line.Time, actor, Int(m), spell);
                return new OtherDamage(line.Time, actor, LineParser.NormalizeMob(target), Int(m), spell, Crit(m));
            }
            if (b.Contains(" points of damage", O) || b.Contains(" point of damage", O))
            {
                var m = MeleeRx().Match(b);
                if (!m.Success) return null;
                return new OtherDamage(line.Time, LineParser.NormalizeMob(m.Groups["a"].Value), LineParser.NormalizeMob(m.Groups["t"].Value),
                    Int(m), LineParser.MeleeSkill(m.Groups["v"].Value), Crit(m));
            }
            return null;
        }
        if (b.Contains(", but ", O))
        {
            // "You try to crush a zombie, but miss!" / "A zombie tries to hit YOU, but YOU dodge!" (not "…, but you are protected.")
            var m = SwingRx().Match(b);
            return m.Success ? new Swing(line.Time, LineParser.NormalizeMob(m.Groups["a"].Value), LineParser.NormalizeMob(m.Groups["t"].Value)) : null;
        }
        if (b.EndsWith('!') && b.Contains(" resist", O))
        {
            // "A gnoll resisted your Ignite!" / "A gnoll resisted Labtik's Strike III!" / "You resist a gnoll's Root!"
            var m = ResistedRx().Match(b);
            if (m.Success) return new Swing(line.Time, m.Groups["a"].Value == "your" ? "You" : m.Groups["a"].Value, LineParser.NormalizeMob(m.Groups["t"].Value));
            m = YouResistRx().Match(b);
            return m.Success ? new Swing(line.Time, LineParser.NormalizeMob(m.Groups["a"].Value), "YOU") : null;
        }
        if (b.Contains(" healed ", O))
        {
            var m = HealRx().Match(b);
            if (!m.Success) return null;
            string actor = LineParser.NormalizeMob(m.Groups["a"].Value), target = m.Groups["t"].Value;
            if (target is "himself" or "herself" or "itself" or "yourself") target = actor;
            return new HealLanded(line.Time, actor, target, Int(m), LineParser.StripRank(m.Groups["s"].Value));
        }
        if (b.Contains(" has taken ", O))
        {
            // Someone else's damage over time: "X has taken 67 damage from Boil Blood by Hellsfury."
            var m = OtherDotRx().Match(b);
            if (m.Success)
                return new OtherDamage(line.Time, m.Groups["a"].Value, LineParser.NormalizeMob(m.Groups["t"].Value), Int(m),
                    LineParser.StripRank(m.Groups["s"].Value), Crit(m));
        }
        return null;
    }

    private static int Int(Match m) => int.Parse(m.Groups["n"].ValueSpan, NumberStyles.Integer, CI);

    private static bool Crit(Match m)
    {
        foreach (Capture c in m.Groups["tag"].Captures)
            if (c.Value.Contains("Critical", O)) return true;
        return false;
    }

    [GeneratedRegex(@"^(?<a>.+?) (?<v>hits?|punch(?:es)?|bash(?:es)?|bites?|cleaves?|slash(?:es)?|kicks?|strikes?|claws?|crush(?:es)?|pierces?|slices?|backstabs?|frenzies(?= on)|reaves?|shoots?|smites?|smash(?:es)?|mauls?|gores?|stings?|slams?|rends?)(?:(?<=frenzies) on)? (?<t>.+?) for (?<n>\d+) points? of damage\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex MeleeRx();

    [GeneratedRegex(@"^(?<a>.+?) hits? (?<t>.+?) for (?<n>\d+) points? of [a-z]+ damage by (?<s>.+?)\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex SpellRx();

    [GeneratedRegex(@"^(?<a>.+?) healed (?<t>.+?)(?: over time)? for (?<n>\d+)(?: \(\d+\))? hit points by (?<s>.+?)\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex HealRx();

    [GeneratedRegex(@"^(?<t>.+?) has taken (?<n>\d+) damage from (?<s>.+?) by (?<a>[A-Z][a-z]+)\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex OtherDotRx();

    [GeneratedRegex(@"^(?<a>.+?) tr(?:y|ies) to [a-z]+ (?:on )?(?<t>.+?), but (?:miss|.+ (?:dodges?|parr(?:y|ies)|ripostes?|blocks?)!)", RegexOptions.CultureInvariant)]
    private static partial Regex SwingRx();

    [GeneratedRegex(@"^(?<t>.+?) resisted (?:(?<a>your)|(?<a>[A-Z][a-z]+)'s) .+!$", RegexOptions.CultureInvariant)]
    private static partial Regex ResistedRx();

    [GeneratedRegex(@"^You resist (?<a>.+?)'s .+!$", RegexOptions.CultureInvariant)]
    private static partial Regex YouResistRx();
}
