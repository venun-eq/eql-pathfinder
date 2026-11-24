using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Each row of Your combos (docs/specs/campaign-your-combos.md, Decisions): held, partly kept, not kept, broken by a hand edit or by a
/// pin, and the runs a rebuild with the Musts gives (<see cref="Campaign.ComboRows"/>).
/// </summary>
public class CampaignComboRowsTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);

    private static CampaignPlan Planned(CampaignPlan p)
    {
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        return p;
    }

    private static CampaignPlan Goal(string cls, int rank, params CampaignCombo[] combos) =>
        new() { Classes = new(StringComparer.OrdinalIgnoreCase) { [cls] = rank }, Combos = [.. combos] };

    private static (IReadOnlyList<CampaignComboRow>? Rows, int? Floor, bool? Cost, string? Why, string? CostWhat) Rows(CampaignPlan p, IReadOnlyList<ReincarnationEvent>? events = null) =>
        Campaign.ComboRows(p, Campaign.Evaluate(p, NoClasses, NoRaces, events ?? []), NoClasses, NoRaces, events ?? [], null);

    private static CampaignCombo Trio(string a, string b, string c, bool must) => new() { Classes = [a, b, c], Times = 3, Must = must };
    private static CampaignCombo Pair(string a, string b, bool must) => new() { Classes = [a, b], Must = must };

    [Fact]
    public void AFreeMustIsHeldAndAPreferKeptInSomeRunsIsPartial()
    {
        var held = Rows(Planned(Goal("WAR", 3, Pair("PAL", "MNK", must: true)))).Rows!;
        Assert.Equal("held", held[0].State);
        Assert.Equal(0, held[0].Price.Delta.Runs);

        var partial = Rows(Planned(Goal("WAR", 1, Trio("WAR", "CLR", "MNK", must: false)))).Rows!;
        Assert.Equal("partial", partial[0].State);
        Assert.Equal((1, 3), (partial[0].Price.Kept, partial[0].Price.Of));
        Assert.Equal(2, partial[0].Price.AsMust?.Runs);
    }

    [Fact]
    public void APreferNoRunCanKeepForFreeIsNotKept()
    {
        var rows = Rows(Planned(Goal("WAR", 1, Trio("SHD", "ROG", "SHM", must: false)))).Rows!;
        Assert.Equal("notKept", rows[0].State);
        Assert.Equal(0, rows[0].Price.Kept);
        Assert.Equal(3, rows[0].Price.AsMust?.Runs);
    }

    /// <summary>A hand edit puts MNK out of a run that earns PAL's rank: the Must says where, and "hand"; a pinned loadout there says "pin".</summary>
    [Fact]
    public void AMustBrokenByAHandEditOrAPinSaysWhereAndWhich()
    {
        var edited = Planned(Goal("WAR", 3, Pair("PAL", "MNK", must: true)));
        edited.Classes["PAL"] = 3;
        edited.Classes["MNK"] = 3;
        edited = Planned(edited);
        int at = edited.Loadouts.FindIndex(l => l.Classes.Contains("PAL") && l.Classes.Contains("MNK"));
        Assert.True(at >= 0, "no run has PAL and MNK");
        string swapIn = ClassNames.All.Select(c => c.Abbrev).First(c => c is not ("PAL" or "MNK") && !edited.Loadouts[at].Classes.Contains(c));
        edited.Loadouts[at].Classes[edited.Loadouts[at].Classes.IndexOf("MNK")] = swapIn;

        var hand = Rows(edited).Rows![0];
        Assert.Equal("hand", hand.State);
        Assert.NotNull(hand.Run);
        Assert.Null(hand.Loadout);

        edited.Loadouts[at].Pinned = true;
        var pin = Rows(edited).Rows![0];
        Assert.Equal(("pin", at), (pin.State, pin.Loadout ?? -1));
    }

    [Fact]
    public void TheRunsAMustCostsAreTheFloorWithYourCombos()
    {
        var p = Planned(Goal("WAR", 1, Trio("WAR", "CLR", "MNK", must: true)));
        Assert.Equal(3, Rows(p).Floor);                                   // the goal alone is one run: the Must is +2
        Assert.Equal(1, Campaign.Floor(p, NoClasses, NoRaces).Runs);       // today's floor, unchanged
        Assert.Null(Rows(Planned(Goal("WAR", 1, Trio("WAR", "CLR", "MNK", must: false)))).Floor);
        Assert.Null(Rows(Planned(Goal("WAR", 1))).Rows);
    }

    private static CampaignPlan AllThree(params CampaignCombo[] combos) =>
        new() { Classes = new(ClassNames.All.ToDictionary(c => c.Abbrev, _ => 3), StringComparer.OrdinalIgnoreCase), Combos = [.. combos] };

    /// <summary>A Prefer trio played once is kept (played runs count the same for the row and the rebuild), never "changed by hand".</summary>
    [Fact]
    public void APreferTrioPlayedOnceIsStillHeldNotChangedByHand()
    {
        var p = Planned(AllThree(new CampaignCombo { Classes = ["SHD", "ROG", "SHM"], Times = 1 }));
        p.Since = new DateTime(2026, 11, 1);
        var run = p.Loadouts.First(l => ClassNames.TrioKey(l.Classes) == ClassNames.TrioKey(["SHD", "ROG", "SHM"]));
        var played = new ReincarnationEvent(new DateTime(2026, 11, 2), run.Race, [.. run.Classes]);
        var row = Rows(p, [played]).Rows![0];
        Assert.Equal("held", row.State);
        Assert.Null(row.Run);
    }

    /// <summary>"Free" is the engine's: no run, no XP to level in all and no rank later. A Must that costs runs, and the Musts, say so.</summary>
    [Fact]
    public void FreeAndWhetherTheMustsCostAreTheEnginesToSay()
    {
        var free = Planned(Goal("WAR", 3, Pair("PAL", "MNK", must: true)));
        var r = Rows(free);
        Assert.True(r.Rows![0].Price.Free);
        Assert.False(r.Cost);
        Assert.Equal((r.Rows[0].Price.RunsWith, r.Rows[0].Price.RunsWithout), (r.Rows[0].Price.RunsWithout, r.Rows[0].Price.RunsWith));

        var costs = Rows(Planned(Goal("WAR", 1, Trio("WAR", "CLR", "MNK", must: true))));
        Assert.False(costs.Rows![0].Price.Free);
        Assert.True(costs.Cost);
        Assert.Equal((3, 1), (costs.Rows[0].Price.RunsWith, costs.Rows[0].Price.RunsWithout));
        Assert.Equal("WAR / CLR / MNK 3 times", costs.Why);
        Assert.Null(Rows(Planned(Goal("WAR", 1))).Cost);
    }

    /// <summary>The Musts' cost is a combo's Free (runs, XP to level at its rounding, a rank later), never a looser or stricter test of its own.</summary>
    [Fact]
    public void TheMustsCostIsTheSameTestAsAComboBeingFree()
    {
        var rng = new Random(5);
        var abbrevs = ClassNames.All.Select(c => c.Abbrev).ToList();
        for (int i = 0; i < 80; i++)
        {
            var trio = abbrevs.OrderBy(_ => rng.Next()).Take(3).ToList();
            var p = Planned(Goal(abbrevs[rng.Next(abbrevs.Count)], rng.Next(1, 4), new CampaignCombo { Classes = trio, Times = rng.Next(1, 4), Must = true }));
            var r = Rows(p);
            Assert.Equal(!r.Rows![0].Price.Free, r.Cost);
            Assert.Equal(r.Cost == true, r.CostWhat != null);
            var d = r.Rows[0].Price.Delta;
            if (r.Cost == true && d.Runs <= 0 && d.Xp <= 0) Assert.Contains("later", r.CostWhat);
        }
    }

    /// <summary>A Must trio played outside the plan is not seated again by a fix (add the runs it lacks, rebuild the rest).</summary>
    [Fact]
    public void AFixDoesNotSeatAMustTrioPlayedOutsideThePlan()
    {
        var trio = new CampaignCombo { Classes = ["SHD", "ROG", "SHM"], Times = 1, Must = true };
        var p = new CampaignPlan { Classes = new(StringComparer.OrdinalIgnoreCase) { ["WAR"] = 3, ["CLR"] = 3 }, Since = new DateTime(2026, 11, 1), Combos = [trio] };
        var played = new ReincarnationEvent(new DateTime(2026, 11, 2), "Iksar", ["SHD", "ROG", "SHM"]);
        var (done, off) = Campaign.DoneCounts(p, [played]);
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces, done, null, off);
        Assert.DoesNotContain(p.Loadouts, l => ClassNames.TrioKey(l.Classes) == ClassNames.TrioKey(trio.Classes));
        p.Loadouts.RemoveAt(p.Loadouts.Count - 1);   // a goal now missed: the fixes put runs back
        var fixes = Campaign.Fixes(p, NoClasses, NoRaces, [played], null);
        Assert.NotEmpty(fixes);
        Assert.All(fixes, f => Assert.DoesNotContain(f.Loadouts, l => ClassNames.TrioKey(l.Classes) == ClassNames.TrioKey(trio.Classes)));
    }

    /// <summary>Two Musts that need the same class: the clash names the class and the runs it has left.</summary>
    [Fact]
    public void ATwoMustClashNamesTheClassAndItsRunsLeft()
    {
        var p = Planned(Goal("WAR", 3, Trio("WAR", "CLR", "MNK", must: true)));
        var price = Campaign.ComboPrice(p, Pair("PAL", "MNK", must: true), NoClasses, NoRaces, [], null);
        Assert.Equal(("MNK", 3), (price.Clash?.Class, price.Clash?.Runs));
    }
}
