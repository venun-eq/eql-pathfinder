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
    public static double Minutes(Visit v, double thresholdSeconds, DateTime now)
    {
        double S(DateTime t) => (t - v.Start).TotalSeconds;
        return Minutes(v.CrawlSeconds(now), v.KillTimes.Select(S).ToList(), S(v.IdleEnd(now)),
            v.Pauses.Select(p => (S(p.From), S(p.To))).ToList(), thresholdSeconds);
    }

    /// <summary>The same rule on seconds from the start: the time inside less idle (the community job reads a shared crawl this way).</summary>
    public static double Minutes(double crawlSeconds, IReadOnlyList<double> kills, double end, IReadOnlyList<(double From, double To)> pauses, double thresholdSeconds) =>
        Math.Max(0, crawlSeconds - Math.Min(IdleSeconds(kills, end, pauses, thresholdSeconds), crawlSeconds)) / 60;

    /// <summary>
    /// An open-world stretch's minutes: first kill to last, plus a minute for the pull (there is no time inside to measure it
    /// by, and no idle: stretches already end at a gap).
    /// </summary>
    public static double OpenWorldMinutes(DateTime firstKill, DateTime lastKill) => OpenWorldMinutes(0, (lastKill - firstKill).TotalSeconds);

    /// <summary>The same on seconds from any start.</summary>
    public static double OpenWorldMinutes(double firstKill, double lastKill) => (lastKill - firstKill) / 60 + 1;
}
