using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Which places the overlay offers between crawls, with plain inputs and no service: Ask's data (the instance tables), the
/// level, the rates in use and the label of the place just played. <c>TrackerService</c> judges the last crawl, holds the
/// cache and calls these. Ask, Home and the overlay read the same tables, so they give one answer.
/// </summary>
public static class NextRanking
{
    /// <summary>
    /// Farming: the best two places since the patch (fragments arrived with it) with at least 3 crawls; with none, the best two
    /// from before it. Levels an hour is for the XP your trio needs (<paramref name="xp"/>).
    /// </summary>
    public static List<NextOption> Farms(AskData d, int cap, double xp, string lastLabel)
    {
        var opts = Ask.Farms(d, cap, sincePatch: true).Where(x => x.Crawls >= 3).Take(2).ToList();
        if (opts.Count == 0) opts = Ask.Farms(d, cap, sincePatch: false).Where(x => x.Crawls >= 3).Take(2).ToList();
        return opts.Select(x => new NextOption(x.Place, x.LevelsPerHour / xp, x.T5PerHour, null, x.Crawls, null, x.Place == lastLabel, x.HighTierPerHour)).ToList();
    }

    /// <summary>
    /// Leveling at <paramref name="level"/>: the two best places open here with at least two visits behind them, ranked by cost
    /// against farming when <paramref name="motes"/> (the player is playing for motes), else by speed; the route Ask would take
    /// decides the first (it weighs changing place too); and where that route goes next when it changes place within 2 levels.
    /// </summary>
    public static List<NextOption> Leveling(AskData d, int level, int cap, bool motes, double farmRate, Xp xp, string lastLabel)
    {
        // Each place open at this level, read as Ask reads it (with the levels beside it).
        NextOption? At(PlaceLevels p, int lv, int? from = null)
        {
            if (!ZoneGates.Open(p.Place, lv) || p.Window(lv) is not { Visits: >= 2 } w) return null;
            double lph = 60 / (w.Minutes * xp.At(lv)), t5 = w.T5PerHour;
            return new NextOption(p.Place, lph, t5, RouteAnalysis.CostPerLevel(farmRate, t5, lph), w.Visits, from, from == null && p.Place == lastLabel);
        }
        var here = d.Places.Select(p => At(p, level)).OfType<NextOption>()
            .OrderBy(x => motes ? x.Cost ?? double.MaxValue : -x.LevelsPerHour).ToList();
        var route = Ask.Chain(d, level, Math.Min(cap, level + 6), motes, farmRate, xp);
        if (route.Legs.Count > 0 && here.FindIndex(x => x.Label == route.Legs[0].Place) is var i and > 0) { var first = here[i]; here.RemoveAt(i); here.Insert(0, first); }
        var choices = here.Take(2).ToList();
        if (route.Legs.Count > 1 && route.Legs[1].From - level <= 2 && d.Places.FirstOrDefault(p => p.Place == route.Legs[1].Place) is { } nextPlace
            && At(nextPlace, route.Legs[1].From, route.Legs[1].From) is { } up)
            choices.Add(up);
        return choices;
    }
}
