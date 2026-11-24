using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Direct unit tests for <see cref="ComboPlacement"/>: slot scheduling (spread out vs back-to-back Unlock now),
/// ranked Unlock-now class grouping around Never and Every-run pairs, reserved Must trio capacity, and the unified
/// per-run rule shared by <see cref="Campaign.Generate"/>, <c>Improve</c>, and <see cref="Campaign.Broken"/>.
/// </summary>
public class ComboPlacementTests
{
    private static Dictionary<string, int> ZeroClasses() => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NeedsAllThree() => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 3);

    [Fact]
    public void HeldRunRuleMatchesNeverAndEveryRunPairsOnlyWhileBelowMaxRank()
    {
        var never = new CampaignCombo { Classes = ["WAR", "PAL", "SHD"], Apart = true };
        Assert.True(ComboPlacement.BreaksHeldRunRule(never, ["WAR", "SHD", "CLR"], _ => false));
        Assert.False(ComboPlacement.BreaksHeldRunRule(never, ["WAR", "CLR", "ENC"], _ => true));

        var mustPair = new CampaignCombo { Classes = ["MAG", "NEC"], Must = true, EveryRun = true };
        Assert.True(ComboPlacement.BreaksHeldRunRule(mustPair, ["MAG", "CLR", "ENC"], k => k == "MAG"));
        Assert.False(ComboPlacement.BreaksHeldRunRule(mustPair, ["MAG", "CLR", "ENC"], _ => false)); // MAG already 3/3
        Assert.False(ComboPlacement.BreaksHeldRunRule(mustPair, ["MAG", "NEC", "ENC"], _ => true));

        var preferPair = new CampaignCombo { Classes = ["MAG", "NEC"], Must = false, EveryRun = true };
        Assert.False(ComboPlacement.IsHeldRunRule(preferPair));
        Assert.False(ComboPlacement.BreaksHeldRunRule(preferPair, ["MAG", "CLR", "ENC"], _ => true));
    }

    [Fact]
    public void SpreadOutMustTrioSpacesSlotsAcrossClassRunsWhileUnlockNowQueuesBackToBack()
    {
        var pc = ZeroClasses();
        var dc = NeedsAllThree(); // 16 classes * 3 = 48 ranks -> 16 class runs
        var spread = new CampaignCombo { Classes = ["SHD", "ROG", "SHM"], Times = 3, Must = true };
        var unlock = new CampaignCombo { Classes = ["WAR", "CLR", "MNK"], Times = 2, Must = true, UnlockNow = true };

        var placement = ComboPlacement.From([unlock, spread], ranked: false, [], pc, dc, []);

        var spreadPositions = placement.Slots.Where(s => s.Classes.Contains("SHD")).Select(s => s.Pos).ToList();
        var unlockPositions = placement.Slots.Where(s => s.Classes.Contains("WAR")).Select(s => s.Pos).ToList();
        Assert.Equal([0, 5, 10], spreadPositions);
        Assert.Equal([0, 1], unlockPositions);

        // Taking an Unlock-now slot pins its second run (Pos = 1) ahead of the spread slot waiting at Pos = 0.
        var first = placement.TakeDueSlot(step: 0, hasClassGoals: true);
        Assert.NotNull(first);
        Assert.True(first.Unlock);
        Assert.Equal(["WAR", "CLR", "MNK"], first.Classes);

        var second = placement.TakeDueSlot(step: 1, hasClassGoals: true);
        Assert.NotNull(second);
        Assert.Equal(["WAR", "CLR", "MNK"], second.Classes);

        var third = placement.TakeDueSlot(step: 2, hasClassGoals: true);
        Assert.NotNull(third);
        Assert.Equal(["SHD", "ROG", "SHM"], third.Classes);
    }

    [Fact]
    public void AllowedReservesRemainingRanksForPendingMustTrio()
    {
        var pc = ZeroClasses();
        pc["SHD"] = 1; // 2 rank-earning runs left for SHD; ROG and SHM only have 1 each, so SHD carries the 2nd Must run
        pc["ROG"] = 2;
        pc["SHM"] = 2;
        var dc = NeedsAllThree();
        var mustTrio = new CampaignCombo { Classes = ["SHD", "ROG", "SHM"], Times = 2, Must = true };

        var placement = ComboPlacement.From([mustTrio], ranked: false, [], pc, dc, []);

        // Seating SHD without ROG/SHM drops the trio's max remaining rank-earning capacity from 2 to 1, short of the 2 pending Must runs.
        Assert.False(placement.Allowed(["SHD", "WAR", "CLR"], pc));
        Assert.True(placement.Allowed(["WAR", "CLR", "MNK"], pc));
    }

    [Fact]
    public void RankedUnlockNowClassesSplitIntoSeparateGroupsWhenSeparatedByNever()
    {
        var pc = ZeroClasses();
        var dc = NeedsAllThree();
        var never = new CampaignCombo { Classes = ["ROG", "BER"], Apart = true };
        var pair = new CampaignCombo { Classes = ["ROG", "SHM"], Must = true, EveryRun = true };
        var rankedClasses = new List<CampaignWant>
        {
            new() { Key = "ROG", Target = 3, UnlockNow = true },
            new() { Key = "BER", Target = 3, UnlockNow = true },
        };

        var placement = ComboPlacement.From([never, pair], ranked: true, rankedClasses, pc, dc, []);

        // ROG brings its Every-run partner SHM into group 1 (positions 0..2); BER splits into group 2 (positions 3..5) due to Never.
        Assert.Equal(6, placement.Slots.Count);
        Assert.All(placement.Slots.Take(3), s => Assert.Equal(["ROG", "SHM"], s.Classes));
        Assert.All(placement.Slots.Skip(3), s => Assert.Equal(["BER"], s.Classes));
    }

    [Fact]
    public void CountBrokenRunsCountsOnlyUncompletedRunsViolatingHeldRules()
    {
        var idx = ClassNames.All.Select((c, i) => (c.Abbrev, i)).ToDictionary(x => x.Abbrev, x => x.i);
        var startC = new int[ClassNames.All.Count];
        var combos = new List<CampaignCombo>
        {
            new() { Classes = ["WAR", "BER"], Apart = true },
            new() { Classes = ["PAL", "MNK"], Must = true, EveryRun = true },
        };
        var loadouts = new List<CampaignLoadout>
        {
            new() { Race = "Human", Classes = ["WAR", "BER", "CLR"], Times = 2 }, // 1 done, 1 remaining -> 1 Never break
            new() { Race = "Human", Classes = ["PAL", "CLR", "ENC"], Times = 1 }, // PAL without MNK below rank 3 -> 1 pair break
        };

        int broken = ComboPlacement.CountBrokenRuns(combos, loadouts, skip: [1, 0], startC, idx);
        Assert.Equal(2, broken);
    }
}
