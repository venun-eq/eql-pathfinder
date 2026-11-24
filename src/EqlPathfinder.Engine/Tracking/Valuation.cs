using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tracking;

/// <summary>The one T5-eq value of loot: a mote is worth its tier's T5-eq, a fragment that times the fragment value.</summary>
public static class Valuation
{
    public static double T5(MoteTier tier, int count, bool fragment, double fragmentValue) =>
        count * MoteTiers.T5Value(tier) * (fragment ? fragmentValue : 1);
}
