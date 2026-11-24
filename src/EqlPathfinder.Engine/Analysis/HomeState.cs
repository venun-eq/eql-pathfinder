using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// An open relevel as Home lists it: its place among every relevel by when it started, its loadout's level (from the roster),
/// and its journey's index in <c>reinc.journeys</c> (null when it hasn't been played).
/// </summary>
public sealed record HomeRelevel(string Trio, string TrioKey, string? Race, DateTime Reincarnated, int Number, int Level, int? JourneyIndex);

/// <summary>
/// Which of Home's five states to show: "farming" (at the cap), "relevel" (below it, on a trio and race with a relevel
/// open), "leveling", "swapped" (below the cap, the loadout not known, a relevel open) and "nolevel" (no level line yet).
/// <see cref="Now"/> is the relevel on now; <see cref="Parked"/> the other open ones (all of them when none is on now);
/// <see cref="NextNumber"/> the number a relevel started next would have.
/// </summary>
public sealed record HomeNow(string Mode, int? Level, int Cap, HomeRelevel? Now, IReadOnlyList<HomeRelevel> Parked, int NextNumber)
{
    /// <summary>The loadout slot on and how it is known, when the game's files have named one (current-loadout).</summary>
    public LoadoutOn? Loadout { get; init; }
}

public static class HomeState
{
    /// <summary>
    /// The same, knowing the loadout slot on: one no /who has named is "newloadout" (whatever relevels are open, and with the
    /// level only a ding gave), one whose classes changed is "classes" below the cap, and at the cap Home stays on "farming".
    /// </summary>
    public static HomeNow Of(int? level, string? trio, string? race, int cap, Roster roster, IReadOnlyList<Journey> journeys,
        LoadoutOn? loadout)
    {
        var home = Of(level, trio, race, cap, roster, journeys);
        if (loadout == null || loadout.State == "known") return home with { Loadout = loadout };
        var open = home.Now != null ? home.Parked.Prepend(home.Now).ToList() : home.Parked;
        string mode = loadout.State == "new" ? "newloadout" : level is { } l && l >= cap ? "farming" : "classes";
        return mode == "farming" ? home with { Loadout = loadout } : home with { Mode = mode, Now = null, Parked = open, Loadout = loadout };
    }

    /// <summary>
    /// Derives Home state from what the log reports about the active character. Never guesses a releveling state:
    /// only an open relevel matching the active trio and race counts (never the first open one or a level coincidence).
    /// When the race is unknown, a relevel counts only if it is the sole open relevel for that trio.
    /// </summary>
    public static HomeNow Of(int? level, string? trio, string? race, int cap, Roster roster, IReadOnlyList<Journey> journeys)
    {
        var all = Relevels.Of(roster, journeys, cap);
        var open = all.Where(r => !r.Done).ToList();
        Relevel? now = null;
        if (level is { } lv && lv < cap && trio != null)
        {
            var same = open.Where(r => r.TrioKey == Model.ClassNames.TrioKey(trio)).ToList();
            var exact = race == null ? [] : same.Where(r => r.Race == race).ToList();
            now = exact.Count > 0 ? exact[^1] : same.Count == 1 && (race == null || same[0].Race == null) ? same[0] : null;
        }
        HomeRelevel Row(Relevel r) => new(r.Trio, r.TrioKey, r.Race, r.Reincarnated, r.Number, r.Level, r.JourneyIndex);
        string mode = level is not { } l ? "nolevel" : l >= cap ? "farming" : now != null ? "relevel"
            : trio == null && open.Count > 0 ? "swapped" : "leveling";
        return new HomeNow(mode, level, cap, now != null ? Row(now) : null, open.Where(r => r != now).Select(Row).ToList(), all.Count + 1);
    }
}
