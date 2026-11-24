using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The overlay's note while there's no usual says how many of the earlier visits it takes there are (docs/specs/overlay-levels-wording.md).
/// The user, 11-19: "'Three make one' is very poor grammar."
/// </summary>
public class NoUsualNoteTests
{
    [Fact]
    public void AFarmingVisitSaysHowManyOfTheThreeEarlierVisitsThereAre()
    {
        Assert.Equal("No usual here yet: 1 of 3 earlier visits like this one at levels 18–22.", FarmPace.NoUsualNote(1, FarmPace.LevelsOf(20)));
        Assert.Equal("No usual here yet: 0 of 3 earlier visits like this one at levels 18–22.", FarmPace.NoUsualNote(0, FarmPace.LevelsOf(20)));
        // At the cap the usual isn't made of levels, so the note names none.
        Assert.Equal("No usual here yet: 2 of 3 earlier visits like this one.", FarmPace.NoUsualNote(2, null));
    }

    [Fact]
    public void ALevelingRunSaysHowManyOfTheThreeEarlierRunsNearItsLevelThereAre()
    {
        Assert.Equal("No usual here yet: 2 of 3 earlier runs within 2 levels of 20", RunPace.NoUsualNote(2, 20));
        Assert.Equal("No usual here yet: 0 of 3 earlier runs within 2 levels of 20", RunPace.NoUsualNote(0, 20));
        Assert.Equal("No usual here yet: 1 of 3 earlier runs", RunPace.NoUsualNote(1, null));
    }
}
