using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Upgrades;

namespace EqlPathfinder.Engine.Tests;

public class UpgradePlannerTests
{
    private static int[] Tiers(params (MoteTier Tier, int Count)[] xs)
    {
        var a = new int[11];
        foreach (var (t, n) in xs) a[(int)t] = n;
        return a;
    }

    private static UpgradePlan Gear(int from, int goal, int[]? motes = null, int[]? frags = null, int vtps = 0, long progress = 0) =>
        UpgradePlanner.Plan(new UpgradeRequest(UpgradeTarget.Gear, from, progress, goal, motes ?? new int[11], frags ?? new int[11], vtps));

    /// <summary>Every unit of every step goes in while the item is still under its cap.</summary>
    private static void AssertCapsHold(UpgradePlan p)
    {
        foreach (var s in p.Steps)
        {
            long lastStart = Potential.PointsToReach(s.StartTier) + s.StartProgress + (long)(s.Count - 1) * s.PointsEach;
            Assert.True(lastStart <= (1L << (int)s.Tier) - 2, $"{s.Kind} of {s.Tier} put in past its cap");
        }
    }

    [Fact]
    public void ZeroToFiveIsSevenMajors()
    {
        // 31 points: 6 Majors make 30 and the 7th still fits at +4, which beats finishing with a Greater (2 T5-eq).
        var p = Gear(0, 5);
        Assert.Equal(7, p.CostT5, 6);
        var step = Assert.Single(p.Steps);
        Assert.Equal((LootKind.Mote, MoteTier.Major, 7), (step.Kind, step.Tier, step.Count));
        Assert.Equal(4, p.Overshoot);
        Assert.Equal(7, p.FarmT5, 6);
    }

    [Fact]
    public void FiveToSixFromTheBankCondensesMajors()
    {
        // +5 takes Greater and up: six Greaters (36 points for 12 T5-eq) cover the 32.
        var p = Gear(5, 6, Tiers((MoteTier.Major, 64)));
        Assert.Equal(12, p.CostT5, 6);
        Assert.Equal(6, p.Steps.Single().Count);
        Assert.Equal(MoteTier.Greater, p.Steps.Single().Tier);
        Assert.Equal(new CondenseStep(MoteTier.Major, 12), Assert.Single(p.Condense));
        Assert.Equal(12, p.MotesFromBank[(int)MoteTier.Major]);
        Assert.Equal(0, p.FarmT5);
    }

    [Fact]
    public void CudgelFiveToTenReachesTheGoalWithinCaps()
    {
        var p = Gear(5, 10);
        Assert.Equal(10, p.EndTier);
        Assert.Equal(1024 - 32, p.PointsNeeded);
        AssertCapsHold(p);
        Assert.Equal(p.CostT5, p.FarmT5, 6); // nothing in the bank: everything is farmed
        Assert.True(p.CostByVtps.Zip(p.CostByVtps.Skip(1)).All(x => x.First > x.Second), "each VTP saves something");
    }

    [Fact]
    public void AFragmentIsChargedAtAnAscendantIntoAnEightItem()
    {
        // Fragments condense 2:1 (11-17 patch), and the recommendation feeds gear to +9 with VTPs saved for +9 → +10 (user,
        // 11-17). So every tier's best use is condensing up to Ascendant into a +8 item (32 gear points, about 56.9 Majors),
        // and a fragment of tier r is charged that / 2^(9 - r): a Major fragment 3.56, against 2 on a spell. An Infinite is
        // priced as the two Ascendants it's made from.
        double ascendant = Potential.GearMajors(LootKind.Fragment, MoteTier.Ascendant, 8)!.Value;
        for (var t = MoteTier.Lesser; t <= MoteTier.Infinite; t++)
            Assert.Equal(ascendant * Math.Pow(2, (int)t - 9), UpgradePlanner.FragmentCharge(t), 6);
        Assert.Equal(Potential.RecommendedFragmentValue, UpgradePlanner.FragmentCharge(MoteTier.Major), 2);
        Assert.Equal((9, 3.56), (Potential.RecommendedGoal, Potential.RecommendedFragmentValue));
        // The two lowest are worth more on a +0 or +1 item (2 and 3 points, at 5 a Major) than 256 or 128 to an Infinite.
        Assert.Equal(0.4, UpgradePlanner.FragmentCharge(MoteTier.Infinitesimal), 6);
        Assert.Equal(0.6, UpgradePlanner.FragmentCharge(MoteTier.Minor), 6);
    }

    [Fact]
    public void FragmentsAreSavedForTheHighestGear()
    {
        // A Grand fragment at +7 is worth 22 Majors, under the 36 it makes condensed to Infinite: kept.
        Assert.DoesNotContain(Gear(7, 8, frags: Tiers((MoteTier.Grand, 1)), progress: 2).Steps, s => s.Kind == LootKind.Fragment);
        // Major fragments on a low item: 1.6 each there, 4.5 kept. Seven Majors, as with no fragments.
        var low = Gear(0, 5, frags: Tiers((MoteTier.Major, 10)));
        Assert.Equal(7, low.CostT5, 6);
        Assert.DoesNotContain(low.Steps, s => s.Kind == LootKind.Fragment);
    }

    [Fact]
    public void MajorFragmentsCondenseIntoInfinitesForANineItem()
    {
        // No VTP for +9 → +10 here, so fragments go there, where they're worth most. 64 Major fragments condense into 2
        // Fragments of Infinite (45 points each at +9). Priced as the 32 Major fragments each is made from (3.56 apiece,
        // their worth at +8 → +9), one costs about 114 Majors against 144 for the 45 points in Infinite motes: both are
        // used, with 43 motes (520 points, 8 over).
        var p = Gear(9, 10, frags: Tiers((MoteTier.Major, 64)));
        Assert.Contains(p.Steps, s => s.Kind == LootKind.Fragment && s.Tier == MoteTier.Infinite && s.Count == 2);
        Assert.Equal(64, p.FragmentsFromBank[(int)MoteTier.Major]);
        Assert.Equal(
            [new CondenseStep(MoteTier.Major, 64, LootKind.Fragment), new CondenseStep(MoteTier.Greater, 32, LootKind.Fragment),
             new CondenseStep(MoteTier.Superior, 16, LootKind.Fragment), new CondenseStep(MoteTier.Grand, 8, LootKind.Fragment),
             new CondenseStep(MoteTier.Ascendant, 4, LootKind.Fragment)],
            p.Condense.Where(c => c.Kind == LootKind.Fragment));
        Assert.Equal(2 * UpgradePlanner.FragmentCharge(MoteTier.Infinite) + 43 * 32, p.CostT5, 6);
        AssertCapsHold(p);
    }

    [Fact]
    public void WithAVtpForTheTopTierFragmentsGoIntoEightToNine()
    {
        // The recommendation: a VTP does +9 → +10, so fragments do their best at +8 → +9, condensed to Ascendant (about 56.9
        // Majors each there, exactly their charge). 128 Major fragments make 8 Fragments of Ascendant, 32 points each: the
        // +8 → +9 tier's 256 points with none over, where 29 Ascendant motes (464) go 5 over. Priced as before 11-17 (72 each,
        // kept for a +10 the VTP does) they'd have stayed in the bank.
        var p = Gear(8, 10, vtps: 1, frags: Tiers((MoteTier.Major, 128)));
        Assert.Equal(1, p.VtpsUsed);
        Assert.Contains(p.Steps, s => s.Kind == LootKind.Fragment && s.Tier == MoteTier.Ascendant && s.Count == 8);
        Assert.DoesNotContain(p.Steps, s => s.Kind == LootKind.Mote);
        Assert.Equal(128, p.FragmentsFromBank[(int)MoteTier.Major]);
        AssertCapsHold(p);
    }

    [Fact]
    public void AnInfiniteFragmentIsNotSpentLow()
    {
        // Charged at its best use (+9, about 144 Majors), it's never worth putting into a +0 item.
        var p = Gear(0, 5, frags: Tiers((MoteTier.Infinite, 1)));
        Assert.DoesNotContain(p.Steps, s => s.Kind == LootKind.Fragment);
    }

    [Fact]
    public void VtpsTakeTheTopTiers()
    {
        var p = Gear(7, 10, vtps: 1);
        Assert.Equal(1, p.VtpsUsed);
        Assert.Equal(512 - 128, p.PointsNeeded); // +7 to +9 by points, +9 to +10 by the VTP
        Assert.Equal(10, p.EndTier);
        Assert.Equal(p.CostByVtps[1], p.CostT5, 6);
    }

    private static UpgradePlan Copies(int from, int goal, int[] copies, bool merge, int vtps = 0) =>
        UpgradePlanner.Plan(new UpgradeRequest(UpgradeTarget.Gear, from, 0, goal, new int[11], new int[11], vtps, copies.Select(t => new ItemCopy(t)).ToArray(), merge));

    [Fact]
    public void ACopysProgressCounts()
    {
        // A +7 copy with 127 of 128 points in has 127 + 127; merged into a +7 item (127) it brings those plus 1: 382 is +8 127/256.
        var p = UpgradePlanner.Plan(new UpgradeRequest(UpgradeTarget.Gear, 7, 0, 8, new int[11], new int[11],
            Copies: [new ItemCopy(7, 127)]));
        Assert.Empty(p.Steps);
        var m = Assert.Single(p.Merges!);
        Assert.Equal((7, 127L, 8, 127L), (m.CopyTier, m.CopyProgress, m.EndTier, m.EndProgress));
    }

    [Fact]
    public void CopiesMergeUnlessAskedToKeepThem()
    {
        var p = UpgradePlanner.Plan(new UpgradeRequest(UpgradeTarget.Gear, 5, 0, 8, new int[11], new int[11], Copies: [new ItemCopy(5)]));
        Assert.Single(p.Merges!);
    }

    [Fact]
    public void TwoCopiesAtFiveMergeAtSevenNotOnGrandMotes()
    {
        // Alone, +7 to +8 is 16 Grand motes (128 T5-eq). Two copies both go to +7 on Greater and Superior motes, and
        // merging them is the +8.
        var alone = Copies(5, 8, [5], merge: false);
        var merged = Copies(5, 8, [5], merge: true);
        Assert.Contains(alone.Steps, s => s.Tier == MoteTier.Grand);
        Assert.DoesNotContain(merged.Steps, s => s.Tier >= MoteTier.Grand);
        Assert.True(merged.CostT5 < alone.CostT5 - 70, $"merged {merged.CostT5}, alone {alone.CostT5}");
        Assert.Equal(merged.CostT5, alone.CostOtherWay!.Value, 6);
        Assert.Equal(alone.CostT5, merged.CostOtherWay!.Value, 6);
        Assert.Empty(alone.Merges!);

        var m = Assert.Single(merged.Merges!);
        // Units are lumpy, so one copy ends a little ahead (+7 3/128 and +6 61/64), and together they make +8 exactly.
        Assert.InRange(m.StartTier, 6, 7);
        Assert.InRange(m.CopyTier, 6, 7);
        Assert.Equal((8, 0L), (merged.EndTier, merged.EndProgress));
        Assert.Contains(merged.Steps, s => s.Copy == 1);
        AssertCapsHold(merged);
    }

    [Fact]
    public void AMergeAddsAllTheCopysPointsPlusOne()
    {
        // A +7 copy into a +7 item is exactly +8: nothing to put in.
        var p = Copies(7, 8, [7], merge: true);
        Assert.Empty(p.Steps);
        Assert.Equal(0, p.CostT5);
        var m = Assert.Single(p.Merges!);
        Assert.Equal((7, 0L, 8, 0L), (m.StartTier, m.StartProgress, m.EndTier, m.EndProgress));
        Assert.Equal(0, p.Overshoot);
    }

    [Fact]
    public void CopiesAtAnyTiersReachTheGoalWithinCaps()
    {
        foreach (var (from, copies, goal) in new[] { (2, new[] { 7 }, 9), (6, new[] { 0, 3, 5 }, 10), (4, new[] { 4, 4 }, 9), (0, new[] { 9 }, 10) })
        {
            var p = Copies(from, goal, copies, merge: true, vtps: 1);
            AssertCapsHold(p);
            Assert.Equal(copies.Length, p.Merges!.Count);
            Assert.True(p.EndTier >= goal, $"+{from} with {string.Join(",", copies)} ends at +{p.EndTier}");
            Assert.True(p.CostT5 <= p.CostOtherWay!.Value + 1e-6);
        }
    }

    [Fact]
    public void SpellsHaveNoCap()
    {
        // 31 points to rank 5: two Majors (16 each), or one Greater (32), both 2 T5-eq.
        var p = UpgradePlanner.Plan(new UpgradeRequest(UpgradeTarget.Spell, 0, 0, 5, new int[11], new int[11]));
        Assert.Equal(2, p.CostT5, 6);
    }

    [Fact]
    public void ProgressCounts()
    {
        Assert.Equal(16 - 10, Gear(4, 5, progress: 10).PointsNeeded);
        Assert.Equal(2, Gear(4, 5, progress: 10).CostT5, 6); // two Majors
    }
}

public class InventoryExportTests
{
    [Fact]
    public void ReadsTiersFromNamesAndZeroInEquipment()
    {
        var items = InventoryExport.ParseGear(
        [
            "Location\tName\tID\tCount\tSlots",
            "Equipment\tCudgel of the Fool +5\t177757",
            "Equipment\tSpear of Harmony\t27725",
            "Any Slot\tPyrosilk Robe +9\t181073\t1\t11",
            "General1-Slot3\tCloak of Flames +7\t1\t1\t0",
            "General1-Slot4\tBone Chips\t2\t20\t0",
            "Augmentation\tGamblers Gambit (Exaltation)\t181047",
            "Equipment\tCudgel of the Fool +5\t177757",
        ]);
        Assert.Equal(4, items.Count);
        Assert.Contains(new GearItem("Cudgel of the Fool", 5, "Equipment", 2), items);
        Assert.Contains(new GearItem("Spear of Harmony", 0, "Equipment", 1), items);
        Assert.Contains(new GearItem("Pyrosilk Robe", 9, "Worn", 1), items);
        Assert.Contains(new GearItem("Cloak of Flames", 7, "Bags", 1), items);
    }

    [Fact]
    public void ExportSitsBesideTheLogsFolder() =>
        Assert.Equal(Path.Combine(@"C:\Game", "Venun_oggok-Inventory.txt"), InventoryExport.PathFor(@"C:\Game\Logs\", "Venun", "oggok"));
}

public class MoteBankTests
{
    [Fact]
    public void LootAndCondensingAfterEntryAreApplied()
    {
        var at = new DateTime(2026, 11, 16, 20, 0, 0);
        var bank = new MoteBank { EnteredAt = at };
        bank.Motes[(int)MoteTier.Major] = 10;
        var (motes, frags, since) = bank.Current(
        [
            new BankEvent(at.AddMinutes(-5), LootKind.Mote, MoteTier.Major, 50, false), // before: already counted
            new BankEvent(at.AddMinutes(1), LootKind.Mote, MoteTier.Major, 3, false),
            new BankEvent(at.AddMinutes(2), LootKind.Mote, MoteTier.Major, 4, true),    // 4 Majors -> 2 Greater
            new BankEvent(at.AddMinutes(3), LootKind.Fragment, MoteTier.Grand, 1, false),
        ]);
        Assert.Equal(9, motes[(int)MoteTier.Major]);
        Assert.Equal(2, motes[(int)MoteTier.Greater]);
        Assert.Equal(1, frags[(int)MoteTier.Grand]);
        Assert.Equal(3, since.MotesLooted[(int)MoteTier.Major]);
        Assert.Equal(4, since.MotesOffered[(int)MoteTier.Major]);
    }

    [Fact]
    public void CondensedFragmentsMoveUpATier()
    {
        // 11-17: "You offered 2 Fragment of Major Potential to Constructed Potential." gave 1 Fragment of Greater.
        var at = new DateTime(2026, 11, 17, 16, 0, 0);
        var bank = new MoteBank { EnteredAt = at };
        bank.Fragments[(int)MoteTier.Major] = 308;
        var (motes, frags, since) = bank.Current([new BankEvent(at.AddMinutes(18), LootKind.Fragment, MoteTier.Major, 2, true)]);
        Assert.Equal(306, frags[(int)MoteTier.Major]);
        Assert.Equal(1, frags[(int)MoteTier.Greater]);
        Assert.Equal(0, motes.Sum());
        Assert.Equal(2, since.FragmentsOffered[(int)MoteTier.Major]);
        Assert.Equal(1, since.FragmentsMade[(int)MoteTier.Greater]);
    }

    [Fact]
    public void NothingIsAddedBeforeCountsAreEntered()
    {
        var (motes, _, _) = new MoteBank().Current([new BankEvent(DateTime.Now, LootKind.Mote, MoteTier.Major, 3, false)]);
        Assert.Equal(0, motes.Sum());
    }
}
