using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Estimates how much faster (or slower) a player levels now compared to earlier play, measured within each
/// place-and-level cell at rank 0 so location and level cancel out. Gear, upgrades, and AAs speed up play over time,
/// so <see cref="AskData.From"/> scales historical visit times toward today's pace.
/// </summary>
/// <param name="PerDay">Change in log minutes a level per day (negative: getting faster).</param>
/// <param name="Cells">Place-and-level cells played on dates at least a week apart, the evidence.</param>
/// <param name="Visits">Visits in those cells.</param>
public sealed record PaceDrift(double PerDay, int Cells, int Visits)
{
    /// <summary>Fewer cells or visits than this and there's no drift: too little to tell trend from noise.</summary>
    public const int MinCells = 5, MinVisits = 20;
    /// <summary>
    /// Clamp trend to at most 40% faster or 25% slower per 30 days, and per-visit scaling to 1.5× either way,
    /// preventing old short outlier visits from being extrapolated into unrealistic route legs.
    /// </summary>
    public static readonly double MaxFasterPerDay = Math.Log(0.6) / 30, MaxSlowerPerDay = Math.Log(1.25) / 30, MaxScale = 1.5;

    /// <summary>Percent change in minutes a level per 30 days (-24: a level takes 24% less time each month).</summary>
    public double PerMonthPercent => (Math.Exp(PerDay * 30) - 1) * 100;

    /// <summary>What a visit's time is multiplied by to be at the pace of <paramref name="now"/>.</summary>
    public double Scale(DateTime start, DateTime now) =>
        Math.Clamp(Math.Exp(PerDay * Math.Max(0, (now - start).TotalDays)), 1 / MaxScale, MaxScale);

    /// <summary>The drift in these visits, or null without enough evidence.</summary>
    public static PaceDrift? From(IEnumerable<LevelingSegment> visits)
    {
        var rows = visits.Where(v => v.Type != "open world" && v.LevelStart is >= 1 and < 50 && v.Levels >= 0.5 && v.ActiveMinutes >= Samples.MinFitMinutes)
            .Select(v => (Cell: $"{v.Place}|{(v.LevelStart!.Value - 1) / 3}", Y: Math.Log(v.ActiveMinutes / v.Levels / v.XpMultiplier),
                Day: (v.Start - DateTime.UnixEpoch).TotalDays))
            .GroupBy(r => r.Cell).Where(g => g.Count() >= 2 && g.Max(r => r.Day) - g.Min(r => r.Day) >= 7).ToList();
        int visitCount = rows.Sum(g => g.Count());
        if (rows.Count < MinCells || visitCount < MinVisits) return null;
        double sxy = 0, sxx = 0;
        foreach (var g in rows)
        {
            double mx = g.Average(r => r.Day), my = g.Average(r => r.Y);
            foreach (var r in g) { sxy += (r.Day - mx) * (r.Y - my); sxx += (r.Day - mx) * (r.Day - mx); }
        }
        if (sxx <= 0) return null;
        return new PaceDrift(Math.Clamp(sxy / sxx, MaxFasterPerDay, MaxSlowerPerDay), rows.Count, visitCount);
    }
}
