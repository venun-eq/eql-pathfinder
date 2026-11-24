using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>A stretch of open-world hunting: kills outside any instance, no more than a few minutes apart, in one zone.</summary>
/// <param name="Level">Level at the first kill; <paramref name="LevelEnd"/> at the last (a ding during the stretch raises it).</param>
public sealed record FieldStretch(DateTime Start, DateTime End, double Minutes, double Levels, int Kills, int PartyKills, string? Zone, int? Level,
    int? LevelEnd = null, int? XpKills = null, int UnknownXpKills = 0, int PercentKills = 0)
{
    /// <summary>The same stopgap as <see cref="Visit.PlayedOnACapLoadout"/>.</summary>
    public bool PlayedOnACapLoadout(int levelCap) => CapLoadout.Played(Level, levelCap, UnknownXpKills, PercentKills);
}

/// <summary>
/// Instances are most of a relevel, not all of it: you hunt outside one when a ding is close, before your first
/// crawl, or with friends. These stretches count toward a relevel's active hours and levels.
/// </summary>
public static class FieldHunts
{
    /// <summary>Kills more than <paramref name="gapMinutes"/> apart, or in another zone, start a new stretch; the first kill counts a minute for its pull. A stretch played on a loadout at the cap (<see cref="FieldStretch.PlayedOnACapLoadout"/>) is left out.</summary>
    public static List<FieldStretch> From(IEnumerable<KillRecord> kills, int levelCap, double gapMinutes = 4)
    {
        var result = new List<FieldStretch>();
        var cur = new List<KillRecord>();
        void Flush()
        {
            if (cur.Count == 0) return;
            var stretch = new FieldStretch(cur[0].Time, cur[^1].Time, ActiveTime.OpenWorldMinutes(cur[0].Time, cur[^1].Time),
                cur.Sum(k => k.Percent) / 100, cur.Count, cur.Count(k => k.Party), cur[0].Zone, cur[0].Level, cur[^1].Level,
                cur.Count(k => !k.XpUnknown), cur.Count(k => k.XpUnknown), cur.Count(k => !k.XpUnknown && k.Percent > 0));
            if (!stretch.PlayedOnACapLoadout(levelCap)) result.Add(stretch);   // the stopgap until swap-zones: not a leveling hunt, not shared
            cur = new List<KillRecord>();
        }
        foreach (var k in kills.Where(k => k.VisitId == null && k.Level is { } l && l < levelCap).OrderBy(k => k.Time))
        {
            if (cur.Count > 0 && ((k.Time - cur[^1].Time).TotalMinutes > gapMinutes || k.Zone != cur[^1].Zone)) Flush();
            cur.Add(k);
        }
        Flush();
        return result;
    }
}
