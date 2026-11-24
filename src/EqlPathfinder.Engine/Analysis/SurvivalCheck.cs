namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Determines whether the campaign planner checks trio survivability, based on the character's dominant leveling
/// difficulty (<see cref="DifficultyMix"/>) from the current relevel (once it has crawl minutes) or latest finished relevel.
/// State: "checked" (levels at a farmed difficulty), "quiet" (levels at a non-farmed difficulty),
/// "spread" (mixed: no one difficulty over half), "norelevel" (no relevel with crawls yet) or "off" (farms no difficulty).
/// Never asked of the player.
/// </summary>
/// <param name="Difficulty">The difficulty with over half the active hours of that relevel, when one has.</param>
/// <param name="Relevels">How many relevels that was read from: 0 or 1.</param>
public sealed record SurvivalCheck(string State, int? Difficulty, int Relevels)
{
    public bool Checks => State is "checked" or "spread" or "norelevel";

    /// <param name="journeys">The character's relevels, newest first (the one being played counts, by its hours so far).</param>
    public static SurvivalCheck Of(IReadOnlyList<Journey> journeys, IReadOnlyCollection<int> farmed)
    {
        var read = journeys.FirstOrDefault(j => !j.Done && j.CrawlMinutes > 0) ?? journeys.FirstOrDefault(j => j.Done && j.CrawlMinutes > 0);
        if (farmed.Count == 0) return new("off", null, read == null ? 0 : 1);
        if (read == null) return new("norelevel", null, 0);
        if (read.Mostly is not { } m || m == DifficultyMix.Mixed) return new("spread", null, 1);
        int d = int.Parse(m[1..]);
        return new(farmed.Contains(d) ? "checked" : "quiet", d, 1);
    }
}
