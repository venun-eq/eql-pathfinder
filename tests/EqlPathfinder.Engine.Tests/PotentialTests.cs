using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The mote and fragment definitions (docs/motes-and-fragments.md) against the in-game previews.</summary>
public class PotentialTests
{
    // One fragment previewed into a +0 spell, 11-16: (tier, shown tier, progress numerator). Denominator is 2^tier.
    [Theory]
    [InlineData(MoteTier.Lesser, 3, 1)]
    [InlineData(MoteTier.Potential, 4, 1)]
    [InlineData(MoteTier.Major, 5, 1)]
    [InlineData(MoteTier.Greater, 6, 1)]
    [InlineData(MoteTier.Grand, 8, 1)]
    [InlineData(MoteTier.Ascendant, 9, 1)]
    public void FragmentIntoLevel0Spell(MoteTier tier, int shownTier, long progress)
    {
        var points = Potential.Points(LootKind.Fragment, tier, UpgradeTarget.Spell);
        Assert.Equal((shownTier, progress), Potential.Add(0, 0, points));
    }

    // One fragment previewed into a +0 item, 11-16.
    [Theory]
    [InlineData(MoteTier.Lesser, 2, 1)]
    [InlineData(MoteTier.Potential, 2, 3)]
    [InlineData(MoteTier.Major, 3, 1)]
    [InlineData(MoteTier.Greater, 3, 4)]
    [InlineData(MoteTier.Superior, 4, 1)]
    [InlineData(MoteTier.Grand, 4, 7)]
    [InlineData(MoteTier.Ascendant, 5, 1)]
    [InlineData(MoteTier.Infinite, 5, 14)]
    public void FragmentIntoLevel0Item(MoteTier tier, int shownTier, long progress)
    {
        var points = Potential.Points(LootKind.Fragment, tier, UpgradeTarget.Gear);
        Assert.Equal((shownTier, progress), Potential.Add(0, 0, points));
    }

    [Fact]
    public void OnSpellsAFragmentEqualsOneMoteOfTheNextTier()
    {
        for (var t = MoteTier.Infinitesimal; t < MoteTier.Infinite; t++)
            Assert.Equal(Potential.Points(LootKind.Mote, t + 1, UpgradeTarget.Spell), Potential.Points(LootKind.Fragment, t, UpgradeTarget.Spell));
    }

    [Fact]
    public void SpellT5MatchesTheMoteScale()
    {
        for (var t = MoteTier.Infinitesimal; t <= MoteTier.Infinite; t++)
        {
            Assert.Equal(MoteTiers.T5Value(t), Potential.SpellT5(LootKind.Mote, t));
            Assert.Equal(2 * MoteTiers.T5Value(t), Potential.SpellT5(LootKind.Fragment, t));
        }
    }

    [Fact]
    public void TierCostsDouble()
    {
        Assert.Equal(512, Potential.TierCost(9));
        Assert.Equal(1023, Potential.PointsToReach(10));
        Assert.Equal(1024 - 128 - 3, Potential.PointsNeeded(7, 3, 10)); // +7 with 3 points in, to +10
    }

    [Theory]
    [InlineData(0, 5.0)]
    [InlineData(4, 5.0)]
    [InlineData(5, 3.0)]
    [InlineData(6, 1.75)]
    [InlineData(7, 1.0)]
    [InlineData(8, 0.5625)]
    [InlineData(9, 0.3125)]
    public void GearPointsPerMajorFallAsItemsClimb(int itemTier, double expected) =>
        Assert.Equal(expected, Potential.GearPointsPerMajor(itemTier));

    // A Major mote previewed into a +0 spell (Tier 4 at 1/16) and a +0 item (Tier 2 at 2/4), 11-16.
    [Fact]
    public void MajorMoteIntoLevel0SpellAndItem()
    {
        Assert.Equal((4, 1L), Potential.Add(0, 0, Potential.Points(LootKind.Mote, MoteTier.Major, UpgradeTarget.Spell)));
        Assert.Equal((2, 2L), Potential.Add(0, 0, Potential.Points(LootKind.Mote, MoteTier.Major, UpgradeTarget.Gear)));
    }

    [Fact]
    public void GearCapIsOneBelowTheTierForMotesAndFragments()
    {
        Assert.Equal(4, Potential.GearMaxTier(MoteTier.Major));
        Assert.Equal(9, Potential.GearMaxTier(MoteTier.Infinite));
        Assert.Null(Potential.GearMajors(LootKind.Fragment, MoteTier.Major, 5)); // refused in game, 11-16
        Assert.Equal(1.6, Potential.GearMajors(LootKind.Fragment, MoteTier.Major, 4));
    }

    // Each fragment at the highest item tier it fits, in Majors, against its spell value.
    [Theory]
    [InlineData(MoteTier.Major, 4, 1.6, 2)]
    [InlineData(MoteTier.Greater, 5, 11 / 3.0, 4)]
    [InlineData(MoteTier.Superior, 6, 16 / 1.75, 8)]
    [InlineData(MoteTier.Grand, 7, 22, 16)]
    [InlineData(MoteTier.Ascendant, 8, 32 / 0.5625, 32)]
    [InlineData(MoteTier.Infinite, 9, 144, 64)]
    public void FragmentAtItsTopItemTier(MoteTier tier, int itemTier, double gearMajors, double spellT5)
    {
        Assert.Equal(gearMajors, Potential.GearMajors(LootKind.Fragment, tier, itemTier)!.Value, 9);
        Assert.Equal(spellT5, Potential.SpellT5(LootKind.Fragment, tier));
    }

    [Theory]
    // How high mote-fed gear goes → what a fragment is worth in same-tier motes (docs: The ideal split). +6 and lower
    // fall back to a spell's 2.0: on gear at +5 a fragment beats a mote by only 1.83, at +4 by 1.6.
    [InlineData(10, 4.5)]
    [InlineData(9, 3.56)]
    [InlineData(8, 2.75)]
    [InlineData(7, 2.29)]
    [InlineData(6, 2.0)]
    [InlineData(5, 2.0)]
    public void AFragmentIsWorthItsBestRatioOverAMote(int goal, double value) =>
        Assert.Equal(value, Potential.FragmentValue(goal));
}
