namespace EqlPathfinder.Engine.Tracking;

/// <summary>
/// The idle rule, on seconds from a visit's start, so the app (<see cref="Visit.IdleSeconds"/>) and the community job
/// (from a shared crawl's facts) work it out the same way.
/// </summary>
public static class ActiveTime
{
    /// <summary>
    /// Idle beyond the threshold: every gap longer than it counts only the threshold. The gaps run from zone-in (0) to
    /// the first kill, between kills, and from the last kill to <paramref name="end"/>, with time outside the instance
    /// (<paramref name="pauses"/>) left out of each. No XP kills: nothing to measure activity by, so no idle.
    /// </summary>
    /// <param name="kills">XP kills, in order, as seconds from the start.</param>
    public static double IdleSeconds(IReadOnlyList<double> kills, double end, IReadOnlyList<(double From, double To)> pauses, double threshold)
    {
        if (kills.Count == 0) return 0;
        double idle = 0, prev = 0;
        foreach (var t in kills.Append(Math.Max(end, kills[^1])))
        {
            double gap = Counted(prev, t, pauses);
            if (gap > threshold) idle += gap - threshold;
            if (t > prev) prev = t;
        }
        return idle;
    }

    /// <summary>Seconds from one moment to another with the time spent outside the instance taken out.</summary>
    private static double Counted(double from, double to, IReadOnlyList<(double From, double To)> pauses)
    {
        double s = Math.Max(0, to - from);
        foreach (var (pf, pt) in pauses)
        {
            double a = Math.Max(pf, from), b = Math.Min(pt, to);
            if (b > a) s -= b - a;
        }
        return Math.Max(0, s);
    }

    /// <summary>A visit's active minutes: time inside minus idle. The one place that works it out for the app.</summary>
    public static double Minutes(Visit v, double thresholdSeconds, DateTime now) =>
        Math.Max(0, v.CrawlSeconds(now) - v.IdleSeconds(thresholdSeconds, now)) / 60;

    /// <summary>
    /// An open-world stretch's minutes: first kill to last, plus a minute for the pull (there is no time inside to measure it
    /// by, and no idle: stretches already end at a gap).
    /// </summary>
    public static double OpenWorldMinutes(DateTime firstKill, DateTime lastKill) => (lastKill - firstKill).TotalMinutes + 1;
}
