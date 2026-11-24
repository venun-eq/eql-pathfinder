using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// The dominant difficulty of a relevel or Ask route, so Home compares like with like:
/// a D4 relevel loots more and levels slower than D0, so their hours are not directly comparable.
/// More than half of total hours at one difficulty returns e.g. "D4"; otherwise "mixed".
/// Open world and non-crawl instances count in total hours with no difficulty.
/// </summary>
public static class DifficultyMix
{
    public const string Mixed = "mixed";

    /// <summary>A finished or open relevel, by its crawls' active minutes out of all its active hours.</summary>
    public static string? Of(Journey j) =>
        Word(j.Route.Where(l => l.Kind == "crawl" && l.Difficulty != null).Select(l => (l.Difficulty, l.ActiveMinutes)), j.ActiveHours * 60);

    /// <summary>Ask's route, by each leg's minutes. Null with no legs: there is no route, which isn't a mixed one.</summary>
    public static string? OfRoute(IReadOnlyList<AskLeg> legs)
    {
        if (legs.Count == 0) return null;
        // The place name carries the difficulty ("Befallen D1"); an open-world leg carries none.
        var parts = legs.Where(l => !l.OpenWorld).Select(l => (Diff: Regex.Match(l.Place, @" D(\d)$") is { Success: true } m ? (int?)int.Parse(m.Groups[1].Value) : null, l.Minutes));
        return Word(parts, legs.Sum(l => l.Minutes));
    }

    /// <summary>
    /// True when a relevel was played primarily for motes: its dominant difficulty is farmed,
    /// or it is mixed and farmed difficulties account for over half of its hours.
    /// </summary>
    public static bool Farmed(IEnumerable<(int? Difficulty, double Minutes)> parts, double whole, IReadOnlyCollection<int> farmed) =>
        whole > 0 && parts.Where(p => p.Difficulty is { } d && farmed.Contains(d)).Sum(p => p.Minutes) * 2 > whole;

    /// <summary>
    /// The relevel Home reads its kind from: the one on now (this trio's, not done) once it has crawl minutes, else the latest finished
    /// relevel with some; null when no relevel has crawls (open world alone is no difficulty). <paramref name="journeys"/> newest first.
    /// </summary>
    public static (Journey? Kind, string? Source) HomeKind(IReadOnlyList<Journey> journeys, string? trio)
    {
        var now = trio == null ? null : journeys.FirstOrDefault(j => !j.Done && j.TrioKey == Model.ClassNames.TrioKey(trio) && j.CrawlMinutes > 0);
        if (now != null) return (now, "now");
        var last = journeys.FirstOrDefault(j => j.Done && j.CrawlMinutes > 0);
        return last != null ? (last, "last") : (null, null);
    }

    public static bool Farmed(Journey j, IReadOnlyCollection<int> farmed) =>
        Farmed(j.Route.Where(l => l.Kind == "crawl" && l.Difficulty != null).Select(l => (l.Difficulty, l.ActiveMinutes)), j.ActiveHours * 60, farmed);

    private static string Word(IEnumerable<(int? Difficulty, double Minutes)> parts, double whole)
    {
        var top = parts.Where(p => p.Difficulty != null).GroupBy(p => p.Difficulty!.Value).Select(g => (D: g.Key, Minutes: g.Sum(p => p.Minutes)))
            .OrderByDescending(x => x.Minutes).FirstOrDefault();
        return whole > 0 && top.Minutes * 2 > whole ? $"D{top.D}" : Mixed;
    }
}
