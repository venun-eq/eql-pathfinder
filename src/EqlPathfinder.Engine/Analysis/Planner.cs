using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

public sealed record Advice(string Kind, string Title, string Detail);

/// <summary>How an idea fares on one priority. Kind: ok, no (missing), none (untested) or penalty.</summary>
public sealed record IdeaTag(string Kind, string Text);

public sealed record TrioIdea(IReadOnlyList<string> Classes, int RankAfter, double XpMultiplier, double Score,
    IReadOnlyList<IdeaTag> Tags, string? Evidence, bool EvidenceGood, double? SimilarHours, string? SimilarFrom)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<string> Pros => Tags.Where(t => t.Kind == "ok").Select(t => t.Text);
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<string> Cons => Tags.Where(t => t.Kind is "no" or "penalty").Select(t => t.Text);
}

/// <summary>
/// What to reincarnate next and why. Facts used: the FAQ's XP rule (each rank makes every level need 10% more XP, capped
/// at +300%, and a trio and its race need the XP of their highest rank), the Completionist passive (every class reincarnated), and
/// your own finished journeys.
/// </summary>
public static class Planner
{
    public static double XpMultiplier(int rank) => 1 + Math.Min(3.0, 0.1 * rank);

    /// <summary>The XP penalty at each rank from 0 to the one where it stops growing, for the pages to read (the last holds beyond).</summary>
    public static IReadOnlyList<double> XpByRank { get; } = Enumerable.Range(0, 31).Select(XpMultiplier).ToList();

    public static List<Advice> Advise(EngineState s, IReadOnlyList<Journey> journeys, TrackerOptions o)
    {
        var advice = new List<Advice>();
        var roster = s.Roster;
        int Rank(string c) => roster.Classes.GetValueOrDefault(c)?.Rank ?? 0;

        var never = ClassNames.All.Where(c => Rank(c.Abbrev) == 0).ToList();
        if (never.Count > 0 && never.Count < 16)
            advice.Add(new("completionist",
                never.Count == 1 ? $"{never[0].Name} is the last class for Completionist" : $"{never.Count} classes left for Completionist",
                $"Completionist (melee damage, rank goes up each time every class has reincarnated) needs {string.Join(", ", never.Select(c => c.Name))}. " +
                "Reincarnating them first also keeps their relevel cheapest (each level needs +10% XP instead of +20%)."));
        else if (never.Count == 0)
        {
            int minRank = ClassNames.All.Min(c => Rank(c.Abbrev));
            var behind = ClassNames.All.Where(c => Rank(c.Abbrev) == minRank).Select(c => c.Name).ToList();
            advice.Add(new("completionist", $"Every class has reincarnated {minRank}×",
                $"For the next Completionist rank: {string.Join(", ", behind)} {(behind.Count == 1 ? "is" : "are")} still at rank {minRank}."));
        }

        var leveling = journeys.FirstOrDefault(j => !j.Done);
        if (leveling != null)
            advice.Add(new("current", $"Finish {leveling.Trio} first: level {leveling.LevelNow} of {o.LevelCap}",
                leveling.EtaHours is { } eta ? $"About {eta:0.0} active hours to go ({leveling.EtaNote})." : "Not enough finished trios yet to estimate the rest."));

        // The XP rule, made concrete for this roster.
        var ranks = ClassNames.All.GroupBy(c => Rank(c.Abbrev)).OrderBy(g => g.Key)
            .Select(g => $"rank {g.Key}: {string.Join(" ", g.Select(c => c.Abbrev))}").ToList();
        advice.Add(new("rule", "A relevel needs the XP of its most-reincarnated class or race",
            $"Each reincarnation makes every level need 10% more XP (up to +300%). Mobs give the same XP; the bar is bigger. " +
            $"A trio and its race level together, so all four need the XP of the highest rank among them. " +
            $"Group classes of the same rank so none drags the others. Now: {string.Join(" · ", ranks)}."));

        // What finished journeys say.
        var done = journeys.Where(j => j.Done && j.ActiveHours > 1).OrderBy(j => j.ActiveHours).ToList();
        if (done.Count >= 2)
        {
            string Fmt(Journey j) => $"{j.Trio} {j.Race} {j.ActiveHours:0.0} h";
            var carry = done.Where(j => HasCarry(j.TrioKey)).ToList();
            var noCarry = done.Where(j => !HasCarry(j.TrioKey)).ToList();
            string carryNote = carry.Count > 0 && noCarry.Count > 0
                ? $" Trios with a pet, bow or charm carry averaged {carry.Average(j => j.ActiveHours):0.0} h; without one, {noCarry.Average(j => j.ActiveHours):0.0} h."
                : "";
            advice.Add(new("evidence", $"Fastest relevel so far: {Fmt(done[0])}",
                $"All finished: {string.Join(" · ", done.Select(Fmt))}.{carryNote}"));
        }

        var neverRaces = ClassNames.Races.Where(r => (roster.Races.GetValueOrDefault(r)?.Rank ?? 0) == 0).ToList();
        if (neverRaces.Count > 0)
            advice.Add(new("races", $"{neverRaces.Count} races not reincarnated yet",
                $"{string.Join(", ", neverRaces)}. A race needs to be unlocked (max faction with its 3 city factions) before you can use it."));
        return advice;
    }

    /// <summary>Classes leveling now (not available) and the classes furthest behind (every idea must include them).</summary>
    public static (IReadOnlyList<string> Busy, IReadOnlyList<string> Must) Constraints(EngineState s, IReadOnlyList<Journey> journeys)
    {
        int Rank(string c) => s.Roster.Classes.GetValueOrDefault(c)?.Rank ?? 0;
        var busy = journeys.Where(j => !j.Done).SelectMany(j => j.TrioKey.Split('/')).Distinct().ToList();
        var all = ClassNames.All.Select(c => c.Abbrev).Where(c => !busy.Contains(c)).ToList();
        int minRank = all.Count > 0 ? all.Min(Rank) : 0;
        var must = all.Where(c => Rank(c) == minRank).ToList();
        return (busy, must.Count > 3 ? [] : must); // many tied: any combination works
    }

    /// <summary>Ranks trio ideas for the next reincarnation by your priorities. Must include the classes furthest behind.</summary>
    public static List<TrioIdea> Ideas(EngineState s, IReadOnlyList<Journey> journeys, int take = 5, PlanPrefs? prefs = null)
    {
        var (busy, must) = Constraints(s, journeys);
        var all = ClassNames.All.Select(c => c.Abbrev).Where(c => !busy.Contains(c)).ToList();
        var ideas = new List<TrioIdea>();
        foreach (var combo in Combinations(all, 3))
        {
            if (!must.All(combo.Contains)) continue;
            ideas.Add(Evaluate(combo, s, journeys, prefs));
        }
        return ideas.OrderByDescending(i => i.Score).ThenBy(i => string.Join("/", i.Classes), StringComparer.Ordinal).Take(take).ToList();
    }

    /// <summary>
    /// Scores a trio on your priorities: each role it covers adds that place's weight; "proven" adds up to its
    /// weight (or takes it away) from the finished relevel that shares two classes with it. Two pets or two
    /// melee classes cost a point each.
    /// </summary>
    public static TrioIdea Evaluate(IReadOnlyList<string> combo, EngineState s, IReadOnlyList<Journey> journeys, PlanPrefs? prefs = null)
    {
        prefs ??= PlanPrefs.Default;
        var roster = s.Roster;
        int rankAfter = combo.Max(c => (roster.Classes.GetValueOrDefault(c)?.Rank ?? 0) + 1);
        var finished = journeys.Where(j => j.Done && j.ActiveHours > 1).ToList();
        double avg = finished.Count > 0 ? finished.Average(j => j.ActiveHours) : 0;
        var tags = new List<IdeaTag>();
        double score = 0;
        string? evidence = null;
        bool evidenceGood = true;
        double? hours = null;
        string? from = null;

        for (int place = 0; place < prefs.Order.Count; place++)
        {
            string key = prefs.Order[place];
            double w = PlanPrefs.Weight(place);
            var role = PlanPrefs.Role(key);
            if (key == "proven")
            {
                var best = finished.Select(j => (j, shared: j.TrioKey.Split('/').Intersect(combo).ToList()))
                    .Where(x => x.shared.Count >= 2).OrderBy(x => x.j.ActiveHours).FirstOrDefault();
                if (best.j != null && finished.Count >= 2 && avg > 0)
                {
                    // 8% faster than your average earns the full weight.
                    double e = Math.Clamp((avg - best.j.ActiveHours) / avg * 12, -1, 1);
                    score += w * e;
                    hours = best.j.ActiveHours;
                    from = best.j.Trio;
                    evidenceGood = e >= 0;
                    evidence = $"Shares {string.Join(" and ", best.shared)} with {best.j.Trio} ({best.j.ActiveHours:0.0} h, " +
                               $"{(e >= 0 ? "faster" : "slower")} than your average {avg:0.0} h)";
                    tags.Add(new(e >= 0 ? "ok" : "no", "proven"));
                }
                else tags.Add(new("none", "untested"));
                continue;
            }
            var who = combo.Where(c => prefs.Members.GetValueOrDefault(key)?.Contains(c) == true).ToList();
            if (who.Count > 0)
            {
                score += w;
                tags.Add(new("ok", $"{role.Short}: " + string.Join(", ",
                    who.Select(c => key == "carry" && ClassNames.Carries.TryGetValue(c, out var d) ? d : c))));
            }
            else tags.Add(new("no", "no " + role.Short));
        }
        if (combo.Count(c => c is "MAG" or "NEC" or "BST") >= 2) { score -= 1; tags.Add(new("penalty", "one pet at a time until Kunark's Summoner arte")); }
        if (combo.Count(c => c is "WAR" or "MNK" or "ROG" or "BER" or "PAL" or "SHD") >= 2) { score -= 1; tags.Add(new("penalty", "melee-heavy")); }
        return new TrioIdea(combo.ToList(), rankAfter, XpMultiplier(rankAfter), Math.Round(score, 2), tags, evidence, evidenceGood, hours, from);
    }

    private static bool HasCarry(string trioKey) => trioKey.Split('/').Any(ClassNames.Carries.ContainsKey);

    private static IEnumerable<List<string>> Combinations(List<string> items, int k)
    {
        for (int a = 0; a < items.Count; a++)
            for (int b = a + 1; b < items.Count; b++)
                for (int c = b + 1; c < items.Count; c++)
                    yield return [items[a], items[b], items[c]];
    }
}
