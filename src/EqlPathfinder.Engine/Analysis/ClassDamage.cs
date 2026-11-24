using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>A crawl's (or several crawls') damage split by what dealt it. Spells are split evenly between the trio classes that can cast them.</summary>
public sealed record DamageSplit(IReadOnlyDictionary<string, double> Spells, long Pet, long Melee, long Other, long Total);

public static class ClassDamage
{
    /// <summary>
    /// Splits damage by source: each trio class's spells (from the game's spell file), pets, the character's melee
    /// (which the log can't split between melee classes), and the rest (procs, items, skills, unknown spells).
    /// </summary>
    public static DamageSplit Of(IEnumerable<Visit> crawls, IReadOnlyList<string> trio, IReadOnlyDictionary<string, int>? spellClasses)
    {
        var spells = trio.ToDictionary(c => c, _ => 0.0);
        long pet = 0, melee = 0, other = 0, total = 0;
        foreach (var v in crawls)
        {
            total += v.Damage.Total;
            foreach (var (ab, n) in v.Damage.ByAbility)
            {
                if (ab == "pet melee") { pet += n; continue; }
                // Named melee skills (Backstab, Kick, etc.) still count as melee damage here.
                if (ab == "melee" || Parsing.LineParser.MeleeSkills.Contains(ab)) { melee += n; continue; }
                var who = spellClasses != null && spellClasses.TryGetValue(ab, out int mask)
                    ? trio.Where(c => Index(c) is int i && i >= 0 && (mask & (1 << i)) != 0).ToList()
                    : [];
                if (who.Count == 0) { other += n; continue; }
                foreach (var c in who) spells[c] += (double)n / who.Count;
            }
        }
        return new DamageSplit(spells, pet, melee, other, total);
    }

    private static int Index(string abbrev)
    {
        for (int i = 0; i < ClassNames.All.Count; i++) if (ClassNames.All[i].Abbrev == abbrev) return i;
        return -1;
    }
}
