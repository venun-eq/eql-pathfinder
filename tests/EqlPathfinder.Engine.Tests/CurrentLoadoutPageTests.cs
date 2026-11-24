using System.Text.Json.Nodes;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Home for a loadout the app hasn't met, or whose classes changed (docs/specs/current-loadout.md, "What the player
/// sees"): four starts of the browser at once, each the fixture's player as the engine would send them.
/// </summary>
public sealed class CurrentLoadoutPages : IAsyncLifetime
{
    internal PageRun New { get; private set; } = null!;
    internal PageRun NewDinged { get; private set; } = null!;
    internal PageRun ClassesChanged { get; private set; } = null!;
    internal PageRun FarmingClassesChanged { get; private set; } = null!;
    internal PageRun FarmingNew { get; private set; } = null!;

    /// <summary>
    /// Sets a value the engine doesn't send yet (the loadout) or sends as another kind (a level the engine leaves out),
    /// where PageMessage's With holds a test to what the message already has.
    /// </summary>
    private static JsonObject Set(JsonObject m, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        JsonNode at = m;
        foreach (var p in parts[..^1]) at = at[p]!;
        at[parts[^1]] = value;
        return m;
    }

    private static JsonObject Loadout(int slot, string state) => new() { ["slot"] = slot, ["state"] = state };

    /// <summary>Below the cap, on slot 6: the engine's level (null until a ding) and no trio or race.</summary>
    private static async Task<JsonObject> OnSlot6(string mode, string state, int? level, string? race)
    {
        var m = (await PageMessage.FromFixture()).With("reinc.homeNow.mode", mode);
        Set(m, "reinc.homeNow.loadout", Loadout(6, state));
        Set(m, "reinc.homeNow.level", level);
        Set(m, "reinc.currentLevel", level);
        Set(m, "snapshot.level", level);
        Set(m, "snapshot.trio", null);
        Set(m, "reinc.currentTrio", null);
        Set(m, "snapshot.race", race);
        return m;
    }

    public async Task InitializeAsync()
    {
        var newLoadout = await OnSlot6("newloadout", "new", null, null);
        var dinged = await OnSlot6("newloadout", "new", 11, null);
        var classes = await OnSlot6("classes", "classes", 10, "Barbarian");
        var farming = await PageMessage.FarmingAtTheCap();
        Set(farming, "reinc.homeNow.loadout", Loadout(6, "classes"));
        Set(farming, "snapshot.trio", null);
        Set(farming, "reinc.currentTrio", null);
        var farmingNew = await PageMessage.FarmingAtTheCap();
        Set(farmingNew, "reinc.homeNow.loadout", Loadout(6, "new"));
        Set(farmingNew, "snapshot.trio", null);
        Set(farmingNew, "reinc.currentTrio", null);
        var opened = await PageRuns.AtOnce(
            h => h.Open("home", newLoadout.ToJsonString()),
            h => h.Open("home", dinged.ToJsonString()),
            h => h.Open("home", classes.ToJsonString()),
            h => h.Open("home", farming.ToJsonString()),
            h => h.Open("home", farmingNew.ToJsonString()));
        (New, NewDinged, ClassesChanged, FarmingClassesChanged, FarmingNew) = (opened.Runs[0], opened.Runs[1], opened.Runs[2], opened.Runs[3], opened.Runs[4]);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class CurrentLoadoutPageTests(CurrentLoadoutPages pages) : IClassFixture<CurrentLoadoutPages>
{
    [Fact]
    public void HomeAsksForOneWhoOnALoadoutNotMetAndNamesItsSlot()
    {
        var hero = pages.New.Text("homeHero");
        Assert.Contains("New loadout", hero, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Type /who in game once", hero);
        Assert.Contains("loadout 6", hero);
        Assert.DoesNotContain("Swapped loadouts", hero, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("23 / 50", hero);   // the fixture's level is the loadout left's
    }

    [Fact]
    public void HomeShowsTheLevelOfADingOnALoadoutNotMet()
    {
        var hero = pages.NewDinged.Text("homeHero");
        Assert.Contains("New loadout", hero, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("11 / 50", hero);
        Assert.Contains("loadout not known yet", hero);
    }

    [Fact]
    public void HomeSaysClassesChangedAndKeepsTheSlotsLevelAndRace()
    {
        var hero = pages.ClassesChanged.Text("homeHero");
        Assert.Contains("Classes changed", hero, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Type /who in game", hero);
        Assert.Contains("loadout 6", hero);
        Assert.Contains("10 / 50", hero);
        Assert.Contains("Barbarian", hero);
        Assert.DoesNotContain("New loadout", hero, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeKeepsFarmingAtTheCapAndSaysTheClassesAreNotKnown()
    {
        var hero = pages.FarmingClassesChanged.Text("homeHero");
        Assert.Contains("Auto: you're at", hero);   // the farming card, as today
        Assert.Contains("Classes not known yet", hero);
        Assert.Contains("You changed a class on loadout 6", hero);
    }

    [Fact]
    public void HomeKeepsFarmingAtTheCapOnALoadoutNotMetAndAsksForTheWho()
    {
        var hero = pages.FarmingNew.Text("homeHero");
        Assert.Contains("Auto: you're at", hero);
        Assert.Contains("Loadout not known yet", hero);
        Assert.Contains("Pathfinder hasn't met loadout 6. Type /who in game and Home follows it.", hero);
    }
}
