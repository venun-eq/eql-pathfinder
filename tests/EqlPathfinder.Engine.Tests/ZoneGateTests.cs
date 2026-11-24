using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Tests;

/// <summary>The planes open at 46, and Ask never offers one below that (Model/ZoneGates.cs).</summary>
public class ZoneGateTests
{
    private static readonly DateTime Patch = new(2026, 11, 10, 12, 0, 0), After = Patch.AddDays(2);

    private static LevelingSegment V(string zone, int d, int level, double minutes, double levels, double t5 = 0) =>
        new($"{zone} D{d}", zone, d, After, level, minutes, levels, t5, 0, 0, 40, 0, 200);

    [Theory]
    [InlineData("The Plane of Hate", 46)]
    [InlineData("Plane of Hate D4", 46)]
    [InlineData("Plane of Hate - Solo D4", 46)]
    [InlineData("Plane of Fear · open world", 46)]
    [InlineData("Plane of Sky D2", 46)]
    [InlineData("Kedge Keep D4", 1)]
    [InlineData("The Hole D4", 1)]
    public void ThePlanesOpenAt46(string place, int level) => Assert.Equal(level, ZoneGates.MinLevel(place));

    [Fact]
    public void VisitsBelowAGateAreLeftOut()
    {
        // Plane of Fear at 35 was possible before the gate (Venun, 09-09); it can't be repeated, so Ask doesn't count it.
        var d = AskData.From([V("Plane of Fear", 4, 35, 30, 0.5), V("Plane of Fear", 4, 47, 30, 1)], Patch, After);
        var fear = d.Places.Single();
        Assert.Null(fear.At(35));
        Assert.NotNull(fear.At(47));
        Assert.DoesNotContain(d.Bands, b => b.Band == "31-40");
    }

    [Fact]
    public void TheRouteNeverStartsAPlaneBelow46()
    {
        // Plane of Hate is far quicker, and its first level (46) could otherwise stand in for 45.
        var d = AskData.From([
            V("Kedge Keep", 4, 43, 60, 2), V("Kedge Keep", 4, 43, 60, 2),
            V("Plane of Hate", 4, 46, 20, 2), V("Plane of Hate", 4, 46, 20, 2),
        ], Patch, After);
        // Without the gate, 45 would go to Plane of Hate (10 minutes a level against Kedge's 30).
        var r = Ask.Chain(d, 43, 48, countMotes: false, farmRate: 50);
        Assert.Equal([("Kedge Keep D4", 43, 46), ("Plane of Hate D4", 46, 48)], r.Legs.Select(l => (l.Place, l.From, l.To)));
        Assert.Empty(r.Gaps);
    }

    [Fact]
    public void FarmsAtALevelOnlyListOpenPlaces()
    {
        var d = AskData.From([
            V("Kedge Keep", 4, 44, 30, 0.5, t5: 10), V("Kedge Keep", 4, 44, 30, 0.5, t5: 10),
            V("Plane of Hate", 4, 47, 30, 0.5, t5: 40), V("Plane of Hate", 4, 47, 30, 0.5, t5: 40),
        ], Patch, After);
        Assert.Equal(["Kedge Keep D4"], Ask.Farms(d, 44, sincePatch: false).Select(b => b.Place));
        Assert.Equal("Plane of Hate D4", Ask.Farms(d, 47, sincePatch: false).First().Place);
    }
}
