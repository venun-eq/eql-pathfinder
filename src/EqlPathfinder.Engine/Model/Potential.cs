namespace EqlPathfinder.Engine.Model;

/// <summary>Where a mote or fragment goes. Items and spells use the same tier costs but value motes differently.</summary>
public enum UpgradeTarget { Spell, Gear }

/// <summary>
/// What motes and fragments add to an item or a spell. The full definition, sources and open questions are in
/// docs/motes-and-fragments.md; keep the two in step.
/// </summary>
public static class Potential
{
    /// <summary>Motes and fragments both condense 2:1 into the next tier at Constructed Potential.</summary>
    public const int CondenseRatio = 2;

    /// <summary>Points to go from tier t to t+1 (items and spells alike): 2^t.</summary>
    public static long TierCost(int tier) => 1L << tier;

    /// <summary>Points from +0 to the start of tier t: 2^t - 1.</summary>
    public static long PointsToReach(int tier) => (1L << tier) - 1;

    /// <summary>Points still needed to reach <paramref name="goal"/> from <paramref name="tier"/> with <paramref name="progress"/> points into it.</summary>
    public static long PointsNeeded(int tier, long progress, int goal) =>
        Math.Max(0, PointsToReach(goal) - PointsToReach(tier) - progress);

    /// <summary>Adds points to an item or spell at a tier and progress, with no tier cap.</summary>
    public static (int Tier, long Progress) Add(int tier, long progress, long points)
    {
        progress += points;
        while (progress >= TierCost(tier)) { progress -= TierCost(tier); tier++; }
        return (tier, progress);
    }

    // Fragment points on gear (measured in-game on a +0 item), indexed by tier.
    // Infinitesimal (2) and Minor (3) aren't measured; they follow the pattern (exact powers of two at odd tiers, about x1.4 between).
    private static readonly int[] GearFragmentPoints = [0, 2, 3, 4, 6, 8, 11, 16, 22, 32, 45];

    /// <summary>Points one mote or fragment adds.</summary>
    /// <remarks>
    /// Spells: mote 2^(tier-1), fragment 2^tier (so a fragment equals one mote of the next tier).
    /// Gear: mote = its tier number, fragment from the measured table.
    /// </remarks>
    public static int Points(LootKind kind, MoteTier tier, UpgradeTarget target)
    {
        int r = (int)tier;
        return (target, kind) switch
        {
            (UpgradeTarget.Spell, LootKind.Mote) => 1 << (r - 1),
            (UpgradeTarget.Spell, LootKind.Fragment) => 1 << r,
            (UpgradeTarget.Gear, LootKind.Mote) => r,
            _ => GearFragmentPoints[r],
        };
    }

    /// <summary>
    /// The highest item tier a mote or fragment can upgrade: one below its own tier (e.g., +4 takes Major, +5 does not).
    /// </summary>
    public static int GearMaxTier(MoteTier tier) => (int)tier - 1;

    /// <summary>
    /// What one mote or fragment is worth on an item at <paramref name="itemTier"/>, in the Majors you'd otherwise
    /// condense to add the same points. Null when the item is above its cap.
    /// </summary>
    public static double? GearMajors(LootKind kind, MoteTier tier, int itemTier) =>
        itemTier > GearMaxTier(tier) ? null : Points(kind, tier, UpgradeTarget.Gear) / GearPointsPerMajor(itemTier);

    /// <summary>T5-equivalent on a spell: spell points / 16, so one Major mote = 1 and a Fragment of Major = 2.</summary>
    public static double SpellT5(LootKind kind, MoteTier tier) => Points(kind, tier, UpgradeTarget.Spell) / 16.0;

    /// <summary>
    /// Gear points one Major buys at an item tier, using the cheapest mote that still fits (condensed 2:1 from Majors).
    /// Majors can't be split into lower motes, so +4 and below all get a Major's 5.
    /// </summary>
    public static double GearPointsPerMajor(int itemTier)
    {
        int r = Math.Clamp(itemTier + 1, (int)MoteTier.Major, (int)MoteTier.Infinite);
        return r / Math.Pow(CondenseRatio, r - (int)MoteTier.Major);
    }

    /// <summary>
    /// Fragment value in same-tier motes for upgrading gear to <paramref name="goal"/>. Because fragments condense 2:1,
    /// the optimal ratio is the same at every tier (+10: 4.5, +9: 3.56, +8: 2.75, +7: 2.29; floored at spell value 2.0).
    /// where it goes when the gear stops at +6 or lower. Rounded to 2 places (docs/motes-and-fragments.md, The ideal split).
    /// </summary>
    public static double FragmentValue(int goal)
    {
        int item = Math.Clamp(goal, 1, 10) - 1;
        var frag = (MoteTier)Math.Clamp(item + 1, (int)MoteTier.Major, (int)MoteTier.Infinite);
        double perMajorFragment = GearMajors(LootKind.Fragment, frag, item)!.Value / Math.Pow(CondenseRatio, (int)frag - (int)MoteTier.Major);
        return Math.Round(Math.Max(SpellT5(LootKind.Fragment, MoteTier.Major), perMajorFragment), 2);
    }

    /// <summary>
    /// Default recommended gear goal for motes/fragments (+9), reserving Void-Touched Potentials for the costliest
    /// +9 → +10 step (512 points). Fragments then yield peak value on +8 → +9.
    /// </summary>
    public const int RecommendedGoal = 9;

    /// <summary>
    /// The fragment value for a player who isn't sure (Settings' default): gear fed to +9, 3.56, with VTPs doing +9 → +10
    /// (<see cref="RecommendedGoal"/>). It's how the Upgrades planner prices fragments. Change it here if the rules change.
    /// </summary>
    public static double RecommendedFragmentValue => FragmentValue(RecommendedGoal);

    /// <summary>The Settings choices: how high mote-fed gear goes, and the fragment value that makes.</summary>
    public static IReadOnlyList<(int Goal, double Value)> FragmentValueChoices { get; } =
        [.. new[] { 10, 9, 8, 7, 6 }.Select(g => (g, FragmentValue(g)))];
}
