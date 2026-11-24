using System.Numerics;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <param name="Source">
/// "who" (a /who during the visit), "spells" (carried or inferred, and the spells cast fit it),
/// "partial" (inferred from spells, but other trios fit as well: <see cref="Known"/> holds the classes they all share),
/// "carried" (from the last /who or reincarnation, no spells to check it against) or "none".
/// </param>
public sealed record TrioCall(string? Trio, string? Race, string Source, IReadOnlyList<string> Known);

/// <summary>
/// Which trio played a visit when nobody typed /who. The log never says a loadout changed (a level that jumps is the only
/// hint), so a trio carried from the last /who can be wrong for days. Every spell and song belongs to certain classes
/// (spells_us.txt), so the spells cast in a visit either fit the carried trio or show it changed, and then say which
/// three classes it changed to. /who stays the authority; spells check and fill everything between.
/// </summary>
public static class TrioInference
{
    /// <summary>
    /// A spell counts as evidence once it was cast this many times in the visit. Only casts count ("You begin casting"):
    /// damage lines also come from procs, and reincarnation passives proc class spells (a PAL/MNK/ENC relevel right after
    /// SHD and WIZ reincarnated showed Lifedraw and Ignite damage with no cast). Twice keeps a one-off item click out.
    /// </summary>
    public const int MinCasts = 2;

    private static readonly string[] Abbrevs = ClassNames.All.Select(c => c.Abbrev).ToArray();
    private static readonly int[] Combos = BuildCombos();

    private static int[] BuildCombos()
    {
        var list = new List<int>();
        int n = Abbrevs.Length;
        for (int a = 0; a < n; a++) for (int b = a + 1; b < n; b++) for (int c = b + 1; c < n; c++) list.Add((1 << a) | (1 << b) | (1 << c));
        return list.ToArray();
    }

    public static int Mask(string? trio)
    {
        int m = 0;
        foreach (var c in trio?.Split('/') ?? [])
        {
            int i = Array.IndexOf(Abbrevs, c);
            if (i >= 0) m |= 1 << i;
        }
        return m;
    }

    public static string Trio(int mask) => string.Join('/', Enumerable.Range(0, Abbrevs.Length).Where(i => (mask & (1 << i)) != 0).Select(i => Abbrevs[i]));

    /// <summary>A trio is kept, or accepted, when the classes in it can cast at least this share of the visit's casts.</summary>
    public const double Explains = 0.85;
    /// <summary>Trios within this share of the best fit are equally good: the classes they share are known, the rest isn't.</summary>
    public const double Tie = 0.03;
    /// <summary>A /who this soon after a visit whose spells fit its trio says that's the loadout you'd swapped to.</summary>
    public static readonly TimeSpan SoonAfter = TimeSpan.FromHours(6);
    /// <summary>A spell most classes can cast (Rampage, Earthquake: abilities anyone gets) says nothing about the trio.</summary>
    private const int MaxClasses = 9;

    /// <summary>
    /// The spells cast at least <see cref="MinCasts"/> times in a visit, as (class mask, casts). Spells the file doesn't
    /// know, and ones nearly every class can cast, are left out.
    /// </summary>
    public static List<(int Mask, int Casts)> Evidence(Visit v, IReadOnlyDictionary<string, int> spellClasses)
    {
        var result = new List<(int Mask, int Casts)>();
        foreach (var (spell, casts) in v.Casts)
        {
            if (casts < MinCasts || !spellClasses.TryGetValue(spell, out int mask) || mask == 0 || Bits(mask) > MaxClasses) continue;
            result.Add((mask, casts));
        }
        return result;
    }

    /// <summary>The share of the casts a trio's classes can cast (1 when there's nothing to judge by).</summary>
    public static double Score(int trio, IReadOnlyList<(int Mask, int Casts)> evidence)
    {
        double all = 0, fit = 0;
        foreach (var (mask, casts) in evidence) { all += casts; if ((mask & trio) != 0) fit += casts; }
        return all > 0 ? fit / all : 1;
    }

    /// <summary>
    /// The trio per visit: the existing attribution (/who, carried, filled back from the next /who), checked against the
    /// spells cast. A carried trio that explains at least <see cref="Explains"/> of the casts stands (a stray click or an
    /// odd ability doesn't overturn it). Otherwise the trio that explains the most, over this visit and the ones that carry
    /// on from it, replaces it; that trio then carries forward in place of the old one. Spells often fit several trios
    /// equally (a monk casts next to nothing), so among those the trio a /who shows within <see cref="SoonAfter"/> wins,
    /// then the one the last /who showed, then a loadout a /who has ever shown (one you have, not one nobody ever saw),
    /// then the one that changes the fewest classes. Without the spell file this is the plain attribution.
    /// </summary>
    public static Dictionary<string, TrioCall> Attribute(IReadOnlyList<Visit> visits, IReadOnlyDictionary<string, int>? spellClasses)
    {
        var basic = Journeys.Attribute(visits);
        var ordered = visits.OrderBy(v => v.Start).ToList();
        var result = new Dictionary<string, TrioCall>();
        if (spellClasses is not { Count: > 0 })
        {
            foreach (var v in ordered)
            {
                var (t, r) = basic.GetValueOrDefault(v.Id);
                result[v.Id] = new TrioCall(t, r, SourceOf(v, t), t?.Split('/') ?? []);
            }
            return result;
        }

        var evidence = ordered.Select(v => Evidence(v, spellClasses)).ToList();
        // Loadouts confirmed by /who, with timestamp and race. Prefer nearby confirmed loadouts over carrying
        // an earlier trio when spells match multiple candidates; play frequency only breaks final ties.
        var shown = ordered.Where(v => v.Trio != null).GroupBy(v => Mask(v.Trio))
            .ToDictionary(g => g.Key, g => g.Select(v => (v.Start, v.Race)).ToList());
        string? RaceShown(int trio, DateTime t) => shown.GetValueOrDefault(trio)?.Where(x => x.Race != null)
            .OrderBy(x => Math.Abs((x.Start - t).TotalSeconds)).Select(x => x.Race).FirstOrDefault();
        string? replacedTrio = null;
        (int Mask, string? Race)? replacement = null;
        for (int i = 0; i < ordered.Count; i++)
        {
            var v = ordered[i];
            var (basicTrio, race) = basic.GetValueOrDefault(v.Id);
            var trio = basicTrio;
            if (v.TrioKnown && trio != null)
            {
                replacedTrio = null; replacement = null;
                result[v.Id] = new TrioCall(trio, race, v.TrioConfirmed ? "who" : "slot", trio.Split('/'));
                continue;
            }
            // Unmet loadout (no /who yet): do not carry a previous trio or guess one from spells.
            if (v.TrioFrom == "unmet" && !v.TrioConfirmed)
            {
                result[v.Id] = new TrioCall(null, v.Race, "unmet", []);
                continue;
            }
            if (replacement is { } rep && basicTrio == replacedTrio) (trio, race) = (Trio(rep.Mask), rep.Race);
            int carried = Mask(trio);
            var ev = evidence[i];
            if (ev.Count == 0 || (Bits(carried) == 3 && Score(carried, ev) >= Explains))
            {
                result[v.Id] = new TrioCall(trio, race, ev.Count > 0 ? "spells" : trio != null ? "carried" : "none", trio?.Split('/') ?? []);
                continue;
            }

            // Judge by this visit and the ones that carry on from it under the same carried trio (more casts, a clearer fit).
            var window = new List<(int Mask, int Casts)>(ev);
            int? expect = v.LevelEnd;
            for (int j = i + 1; j < ordered.Count && j <= i + 5; j++)
            {
                var n = ordered[j];
                if (n.TrioKnown || basic.GetValueOrDefault(n.Id).Trio != basicTrio) break;
                if (n.LevelStart is not { } ns || expect is not { } e || ns < e - 1 || ns > e + 1) break;
                window.AddRange(evidence[j]);
                expect = n.LevelEnd;
            }
            var scored = Combos.Select(c => (Mask: c, Score: Score(c, window))).ToList();
            double best = scored.Max(x => x.Score);
            if (best < Explains)
            {
                // No three classes cast most of this (a swap inside the visit, or spells the file assigns oddly): leave it.
                result[v.Id] = new TrioCall(trio, race, trio != null ? "carried" : "none", trio?.Split('/') ?? []);
                continue;
            }
            var near = scored.Where(x => x.Score >= best - Tie).Select(x => x.Mask).ToList();
            var nextWho = ordered.Skip(i + 1).FirstOrDefault(n => n.TrioKnown && n.Trio != null);
            int next = Mask(nextWho?.Trio);
            int soon = nextWho != null && nextWho.Start - v.Start <= SoonAfter ? next : 0;
            // The trio /who gave this visit (the last one, or the next before any), when an earlier visit's spells had swapped
            // it out but these fit it again (10-05: ranger spells after a guessed CLR/BRD/SHM went back to the RNG/BRD/ROG the
            // /who showed, not a made-up CLR/RNG/BRD).
            int lastWho = Mask(basicTrio);
            int pick = near.OrderByDescending(c => c == soon).ThenByDescending(c => c == lastWho).ThenByDescending(shown.ContainsKey)
                .ThenByDescending(c => Bits(c & carried)).ThenByDescending(c => Bits(c & next))
                .ThenByDescending(c => shown.GetValueOrDefault(c)?.Count ?? 0).ThenByDescending(c => Score(c, window)).ThenBy(c => c).First();
            int known = near.Aggregate(~0, (k, c) => k & c);
            // A different loadout can have a different race: the one its own /who showed nearest this visit, if any.
            var callRace = Bits(pick & carried) >= 2 ? race : RaceShown(pick, v.Start);
            result[v.Id] = new TrioCall(Trio(pick), callRace, near.Count == 1 ? "spells" : "partial",
                Trio(known).Split('/', StringSplitOptions.RemoveEmptyEntries));
            if (Trio(pick) != trio) { replacedTrio = basicTrio; replacement = (pick, callRace); }
        }
        return result;
    }

    /// <summary>How a trio is known when the spells aren't checked: a /who in the visit, the slot's last /who, a slot no /who named, or carried.</summary>
    private static string SourceOf(Visit v, string? trio) =>
        v.TrioConfirmed ? "who" : v.TrioFrom == "slot" ? "slot" : v.TrioFrom == "unmet" ? "unmet" : trio != null ? "carried" : "none";

    /// <summary><see cref="Attribute"/> as (trio, race) per visit, the shape <see cref="Journeys.Attribute"/> gives.</summary>
    public static Dictionary<string, (string? Trio, string? Race)> Pairs(IReadOnlyList<Visit> visits, IReadOnlyDictionary<string, int>? spellClasses) =>
        Attribute(visits, spellClasses).ToDictionary(x => x.Key, x => (x.Value.Trio, x.Value.Race));

    private static int Bits(int m) => BitOperations.PopCount((uint)m);
}
