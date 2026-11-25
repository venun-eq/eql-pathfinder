namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// The XP a level needs against the usual (<see cref="Planner.XpMultiplier"/>): one number for every level, or one by level when the penalty
/// differs past the first expansion's cap (<see cref="Tracking.Incarnation.RankAt"/>). A number converts to it.
/// </summary>
public readonly record struct Xp(Func<int, double>? ByLevel, double Flat = 1)
{
    public static implicit operator Xp(double flat) => new(null, flat);

    /// <summary>The multiplier of the bar at <paramref name="level"/>.</summary>
    public double At(int level) => ByLevel?.Invoke(level) ?? (Flat == 0 ? 1 : Flat);

    /// <summary>By level from the count at each: <paramref name="rankAt"/>.</summary>
    public static Xp Of(Func<int, int> rankAt) => new(L => Planner.XpMultiplier(rankAt(L)));
}
