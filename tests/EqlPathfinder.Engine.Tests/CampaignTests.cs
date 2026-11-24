using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class CampaignTests
{
    private static Dictionary<string, int> Zero(IEnumerable<string> keys) => keys.ToDictionary(k => k, _ => 0);
    private static Dictionary<string, int> NoClasses => Zero(ClassNames.All.Select(c => c.Abbrev));
    private static Dictionary<string, int> NoRaces => Zero(ClassNames.Races);

    private static CampaignView Play(CampaignPlan p, Dictionary<string, int>? classes = null, Dictionary<string, int>? races = null)
    {
        classes ??= NoClasses;
        races ??= NoRaces;
        p.Loadouts = Campaign.Generate(p, classes, races);
        return Campaign.Evaluate(p, classes, races, []);
    }

    [Fact]
    public void SixteenRacesAndThreeRanks()
    {
        Assert.Equal(16, ClassNames.Races.Count);
        Assert.DoesNotContain("Drakkin", ClassNames.Races);
        Assert.Contains("Kerran", ClassNames.Races);
        Assert.Contains("Half Dark Elf", ClassNames.Races);
    }

    /// <summary>The run where every class goal is met (0 when they already are), or -1 when the plan never gets there.</summary>
    private static int ClassesDoneAt(CampaignView v, Dictionary<string, int> classes)
    {
        var rank = new Dictionary<string, int>(classes);
        bool Met() => v.Goals.Where(g => g.Kind == "class").All(g => rank[g.Key] >= g.Target);
        if (Met()) return 0;
        foreach (var r in v.Runs)
        {
            foreach (var c in r.Classes) rank[c]++;
            if (Met()) return r.N;
        }
        return -1;
    }

    private static int CompletionistAt(CampaignView v, int rank) => v.Runs.First(r => r.Gains.Any(g => g is { Kind: "completionist" } && g.Rank == rank)).N;

    [Fact]
    public void EverythingTakesFortyEightRunsAndEndsAtRankNine()
    {
        var v = Play(Campaign.Preset("all"));
        Assert.Equal(48, v.Left);   // every race needs three runs of its own
        Assert.All(v.Goals, g => Assert.True(g.AtEnd >= g.Target, $"{g.Key} {g.AtEnd}/{g.Target}"));
        // Ranks kept even, the cheapest XP: every class ends at 9.
        Assert.All(v.Goals.Where(g => g.Kind == "class"), g => Assert.Equal(9, g.AtEnd));
        Assert.Equal(3, v.CompletionistAtEnd);
        Assert.Equal(Planner.XpMultiplier(9), v.LastXp, 3);
    }

    [Fact]
    public void ClassesComeFirst()
    {
        // Completionist counts only classes: every class reaches 1, 2 and 3 as soon as it can (48 class ranks, three a
        // run), and the race runs come after.
        var v = Play(Campaign.Preset("all"));
        Assert.Equal(16, ClassesDoneAt(v, NoClasses));
        Assert.Equal(6, CompletionistAt(v, 1));
        Assert.Equal(11, CompletionistAt(v, 2));
        Assert.Equal(16, CompletionistAt(v, 3));
    }

    [Fact]
    public void EveryClassFromScratchIsSixteenRuns()
    {
        var v = Play(Campaign.Preset("classes"));
        Assert.Equal(16, v.Left);
        Assert.All(v.Goals, g => Assert.Equal(3, g.AtEnd));
        Assert.All(v.Runs, r => Assert.Empty(r.Past));   // no class raised past 3: no XP spent on nothing
    }

    [Fact]
    public void EveryClassFromOneEachIsElevenRuns()
    {
        // 32 class ranks to earn, three a run: 11 runs. (The builder before 0.9.37 took 12 here: it ran each new loadout
        // as many times as the biggest need left, so the last class needing two ranks got two runs with two classes
        // already at 3.)
        var classes = ClassNames.All.ToDictionary(c => c.Abbrev, _ => 1);
        var v = Play(Campaign.Preset("classes"), classes);
        Assert.Equal(11, v.Left);
        Assert.Equal(Campaign.FewestClassRuns(v.Goals.Select(g => g.Target - g.Now)), v.Left);
        Assert.Equal(1, v.Runs.Sum(r => r.Past.Count));   // 33 class ranks for 32 needed: one goes past 3
        Assert.True(v.TotalXp <= 13.9 + 1e-9, $"{v.TotalXp} relevels of XP");
    }

    [Fact]
    public void TheFloorSaysWhyAPlanIsTheCheapest()
    {
        var ones = ClassNames.All.ToDictionary(c => c.Abbrev, _ => 1);
        var classes = Play(Campaign.Preset("classes"), ones);
        Assert.Equal(new CampaignFloor(11, 11, "32 class ranks to earn, three a run"), classes.Floor);
        Assert.Equal(classes.Floor.Runs, classes.Left);
        Assert.Equal(11, classes.ClassesDoneAt);

        var all = Play(Campaign.Preset("all"));
        Assert.Equal(new CampaignFloor(48, 16, "48 race ranks to earn, one a run"), all.Floor);
        Assert.Equal(16, all.ClassesDoneAt);   // as early as the class goals alone allow

        var main = new CampaignPlan();
        main.Classes["MAG"] = 3;
        Assert.Equal("MAG needs 3 ranks, one a run", Play(main).Floor.Why);

        var bard = new CampaignPlan();   // BRD 1/3 to 2/3: one rank, one run
        bard.Classes["BRD"] = 2;
        Assert.Equal(new CampaignFloor(1, 1, "1 class rank to earn"), Play(bard, ones).Floor);
    }

    /// <summary>A hand-made plan that leaves MNK at 2/3 and takes CLR past 3/3 (the design mock-up's sample).</summary>
    private static (CampaignPlan Plan, Dictionary<string, int> Classes) ShortOfMonk()
    {
        var classes = NoClasses;
        foreach (var c in ClassNames.All) classes[c.Abbrev] = c.Abbrev is "CLR" or "SHM" or "MAG" or "NEC" or "WAR" ? 2 : 1;
        var p = Campaign.Preset("classes");
        p.Loadouts =
        [
            new CampaignLoadout { Race = "Troll", Classes = ["SHD", "DRU", "BST"], Times = 2 },
            new CampaignLoadout { Race = "Iksar", Classes = ["NEC", "MAG", "CLR"], Times = 1 },
            new CampaignLoadout { Race = "Ogre", Classes = ["WAR", "SHM", "ENC"], Times = 1 },
            new CampaignLoadout { Race = "Dark Elf", Classes = ["ROG", "WIZ", "PAL"], Times = 2 },
            new CampaignLoadout { Race = "Barbarian", Classes = ["BER", "BRD", "RNG"], Times = 2 },
            new CampaignLoadout { Race = "Halfling", Classes = ["MNK", "ENC", "CLR"], Times = 1 },
        ];
        return (p, classes);
    }

    [Fact]
    public void APlanThatMissesAGoalGetsItsCheapestFixesFirst()
    {
        var (p, classes) = ShortOfMonk();
        var v = Campaign.Evaluate(p, classes, NoRaces, []);
        Assert.Contains(v.Goals, g => g is { Key: "MNK", AtEnd: 2 });
        Assert.Equal(9, v.Floor.Runs);
        var fixes = Campaign.Fixes(p, classes, NoRaces, [], Campaign.Quality(p.Prefer));
        Assert.NotEmpty(fixes);
        Assert.All(fixes, f => { Assert.Contains("class:MNK", f.Delta.Fixes); Assert.Empty(f.Delta.Breaks); });
        // Cheapest first: no extra run (a class traded in, or the rest rebuilt), before the ones that add a run.
        Assert.Equal(0, fixes[0].Delta.Runs);
        Assert.True(fixes.Zip(fixes.Skip(1)).All(x => x.First.Delta.Runs <= x.Second.Delta.Runs));
        var fixedPlan = new CampaignPlan { Classes = p.Classes, Loadouts = [.. fixes[0].Loadouts] };
        Assert.All(Campaign.Evaluate(fixedPlan, classes, NoRaces, []).Goals, g => Assert.True(g.AtEnd >= g.Target));
    }

    [Fact]
    public void RunsToSpareGetTheirCheapestDropFirst()
    {
        var ones = ClassNames.All.ToDictionary(c => c.Abbrev, _ => 1);
        var p = Campaign.Preset("classes");
        p.Loadouts = Campaign.Generate(p, ones, NoRaces);
        p.Loadouts.Add(new CampaignLoadout { Race = "Gnome", Classes = ["WAR", "CLR", "PAL"], Times = 1 });
        var v = Campaign.Evaluate(p, ones, NoRaces, []);
        Assert.Equal(v.Floor.Runs + 1, v.Left);
        var fixes = Campaign.Fixes(p, ones, NoRaces, [], null);
        // The plan already had a WAR / CLR / PAL run: dropping either one saves the run and its XP.
        Assert.Equal(-1, fixes[0].Delta.Runs);
        Assert.StartsWith("Drop", fixes[0].Label);
        Assert.Contains("WAR / CLR / PAL", fixes[0].Label);
        Assert.True(fixes[0].Delta.Xp < 0);
    }

    [Fact]
    public void OptionsShowEveryChoiceAndTheCheapestSwaps()
    {
        var (p, classes) = ShortOfMonk();
        int iksar = p.Loadouts.FindIndex(l => l.Race == "Iksar");
        var o = Assert.IsType<CampaignOptions>(Campaign.Options(p, classes, NoRaces, [], Campaign.Quality(p.Prefer), iksar));
        Assert.Equal(16, o.Races.Count);
        Assert.Single(o.Races, c => c.State == "current");
        var clrSlot = o.Classes[2];
        Assert.Equal(16, clrSlot.Count);
        Assert.Single(clrSlot, c => c.State == "current");
        Assert.Equal(2, clrSlot.Count(c => c.State == "trio"));
        // MNK in CLR's place reaches the goal the plan misses, with no extra run: the best pick.
        var mnk = clrSlot.Single(c => c.Value == "MNK");
        Assert.Equal("best", mnk.State);
        Assert.Contains("class:MNK", mnk.Delta!.Fixes);
        Assert.Equal(0, mnk.Delta.Runs);
        Assert.All(o.Swaps, s => Assert.Empty(s.Delta.Breaks));
        Assert.Null(Campaign.Options(p, classes, NoRaces, [], null, 99));
    }

    [Fact]
    public void MostTriosGetACarryAndHealsWhenYouPreferThem()
    {
        var p = Campaign.Preset("all");
        Assert.Equal(["carry", "heals"], p.Prefer);
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces, null, Campaign.Quality(p.Prefer));
        var runs = Campaign.Evaluate(p, NoClasses, NoRaces, []).Runs;
        int carry = runs.Count(r => r.Classes.Any(ClassNames.Carries.ContainsKey));
        int heals = runs.Count(r => r.Classes.Any(ClassNames.IsHealer));
        Assert.True(carry >= 43, $"carry in {carry} of {runs.Count} runs");
        Assert.True(heals >= 34, $"heals in {heals} of {runs.Count} runs");
    }

    [Fact]
    public void WhatYouPreferOnlyChoosesBetweenLoadoutsThatCostTheSame()
    {
        // The same goal with no preference and with the default (a carry, heals): the same runs, no more XP.
        var none = Campaign.Preset("all");
        none.Prefer = [];
        none.Loadouts = Campaign.Generate(none, NoClasses, NoRaces, null, Campaign.Quality(none.Prefer));
        var pref = Campaign.Preset("all");
        pref.Loadouts = Campaign.Generate(pref, NoClasses, NoRaces, null, Campaign.Quality(pref.Prefer));
        var a = Campaign.Evaluate(none, NoClasses, NoRaces, []);
        var b = Campaign.Evaluate(pref, NoClasses, NoRaces, []);
        Assert.Equal(a.Left, b.Left);
        Assert.True(b.TotalXp <= a.TotalXp + 1e-9, $"{b.TotalXp} relevels of XP with the preference, {a.TotalXp} without");
        Assert.Equal(b.Runs.Where(r => r.State is "next" or "planned").Sum(r => r.Xp), b.TotalXp, 2);
        int Carries(CampaignPlan p) => p.Loadouts.Count(l => l.Classes.Any(ClassNames.Carries.ContainsKey));
        Assert.True(Carries(pref) > Carries(none), $"carry in {Carries(pref)} with the preference, {Carries(none)} without");
    }

    [Fact]
    public void PreferencesKeepOnlyWhatTheCampaignKnows()
    {
        Assert.Equal(["heals"], new CampaignPlan { Prefer = ["heals", "pet", "heals"] }.Normalized().Prefer);
        Assert.Empty(new CampaignPlan { Prefer = [] }.Normalized().Prefer);
        Assert.Equal(["carry", "heals"], new CampaignPlan { Prefer = null! }.Normalized().Prefer);
    }

    [Fact]
    public void WhatYouWantFirstComesFirst()
    {
        var p = Campaign.Preset("all");
        p.First = ["class:MAG", "race:Iksar"];
        var v = Play(p);
        Assert.Equal("Iksar", v.Plan.Loadouts[0].Race);
        Assert.Contains("MAG", v.Plan.Loadouts[0].Classes);
        // MAG and Iksar reach 3/3 in the first three runs.
        Assert.Contains(v.Runs[2].Gains, g => g is { Kind: "class", Key: "MAG", Rank: 3 });
        Assert.Contains(v.Runs[2].Gains, g => g is { Kind: "race", Key: "Iksar", Rank: 3 });
    }

    [Fact]
    public void JustYourMainIsThreeRuns()
    {
        var p = new CampaignPlan();
        p.Classes["MAG"] = 3;
        var v = Play(p);
        Assert.Equal(3, v.Left);
        Assert.All(v.Runs, r => Assert.Contains("MAG", r.Classes));
        // Its partners never relevel above MAG, so they add no XP: the three runs need +10%, +20% and +30%.
        Assert.Equal([1.1, 1.2, 1.3], v.Runs.Select(r => Math.Round(r.Xp, 2)));
    }

    [Fact]
    public void ItStartsFromYourRanks()
    {
        var classes = NoClasses;
        classes["MAG"] = 2;
        var races = NoRaces;
        races["Iksar"] = 3;
        var p = new CampaignPlan();
        p.Classes["MAG"] = 3;
        p.Races["Iksar"] = 3;
        var v = Play(p, classes, races);
        Assert.Equal(1, v.Left);
        Assert.NotEqual("Iksar", v.Plan.Loadouts[0].Race); // already 3/3: the filler race is one still short of it
        Assert.Equal(1, v.Plan.Loadouts[0].Times);
    }

    [Fact]
    public void LockedRacesGoLast()
    {
        var p = Campaign.Preset("all");
        p.Locked = ["Human"];
        var v = Play(p);
        Assert.Equal("Human", v.Plan.Loadouts[^1].Race);
        Assert.Contains(v.Loadouts[^1].Warnings, w => w.Contains("isn't unlocked"));
    }

    [Fact]
    public void TheLogTicksRunsOff()
    {
        var p = Campaign.Preset("all");
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        p.Since = new DateTime(2026, 11, 17, 12, 0, 0);
        var first = p.Loadouts[0];
        var events = new List<ReincarnationEvent>
        {
            new(new DateTime(2026, 11, 16), first.Race, first.Classes),            // before the plan: not counted
            new(new DateTime(2026, 11, 18), first.Race, first.Classes.AsEnumerable().Reverse().ToList()),
            new(new DateTime(2026, 11, 19), "Ogre", ["WAR", "CLR", "MAG"]),       // not in any loadout like this
        };
        var classes = NoClasses;
        foreach (var c in first.Classes) classes[c] = 1;
        var races = NoRaces;
        races[first.Race] = 1;
        var (_, relevels) = Seen([Open(first.Race, first.Classes, new DateTime(2026, 11, 18), 20, null, 3)],
            (first.Race, first.Classes, new DateTime(2026, 11, 16), 50), (first.Race, first.Classes, new DateTime(2026, 11, 18), 20), ("Ogre", ["WAR", "CLR", "MAG"], new DateTime(2026, 11, 19), 50));
        var v = Campaign.Evaluate(p, classes, races, events, relevels);
        Assert.Equal(1, v.Done);
        Assert.Equal("leveling", v.Runs[0].State);
        Assert.Equal("next", v.Runs[1].State);
        // The next run relevels from your ranks now, the done run's included.
        var next = v.Runs[1];
        Assert.Equal(Math.Max(next.Classes.Max(c => classes[c]), races[next.Race]) + 1, next.RankAfter);
        Assert.Single(v.OffPlan);
        Assert.Equal("Ogre", v.OffPlan[0].Race);
    }

    /// <summary>The log's side of a relevel: reincarnations as the roster holds them (the race and its three classes in one burst, the classes at that level) and the relevels built from them and the journeys, as the app does.</summary>
    private static (List<ReincarnationEvent> Events, List<Relevel> Relevels) Seen(IReadOnlyList<Journey> journeys, params (string Race, IReadOnlyList<string> Classes, DateTime At, int Level)[] reincs)
    {
        var r = new Roster();
        foreach (var (race, classes, at, level) in reincs)
        {
            r.Race(race).Seen.Add(at);
            foreach (var c in classes) { var i = r.Class(c); i.Seen.Add(at.AddSeconds(1)); i.Level = level; }
        }
        return (Campaign.Events(r), Relevels.Of(r, journeys, 50));
    }

    /// <summary>
    /// Feedback #6 on the player's own log: six relevels before the plan, then Kerran (7th, played to 32, so it has a journey)
    /// and Barbarian (8th, reincarnated and never played: no visits, so no journey). Both are leveling, numbered 7 and 8,
    /// and the next run to reincarnate is 9 - though both came before the plan began.
    /// </summary>
    [Fact]
    public void RelevelsOpenFromBeforeThePlanAreLevelingEvenWithNoJourney()
    {
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0);
        var reincs = Enumerable.Range(0, 6).Select(i => ("Human", (IReadOnlyList<string>)["PAL", "MNK", "SHM"], t0.AddDays(i), 50)).ToList();
        reincs.Add(("Kerran", (IReadOnlyList<string>)["CLR", "RNG", "WAR"], t0.AddDays(10), 32)); // the roster sees them in another order than Home writes the trio
        reincs.Add(("Barbarian", ["BRD", "DRU", "NEC"], t0.AddDays(11), 1));
        var kerran = Open("Kerran", ["WAR", "CLR", "RNG"], t0.AddDays(10).AddSeconds(1), 32, 6.2, 9);
        var (events, relevels) = Seen([kerran], [.. reincs]);
        var p = Campaign.Preset("all");
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        p.Since = t0.AddDays(12);
        var v = Campaign.Evaluate(p, NoClasses, NoRaces, events, relevels);
        Assert.Equal(6, v.Before);
        Assert.Equal((2, 2), (v.Done, v.Leveling));
        Assert.Equal(["leveling", "leveling", "next"], v.Runs.Take(3).Select(r => r.State));
        Assert.Equal([-1, -1], v.Runs.Take(2).Select(r => r.Loadout));
        Assert.Equal(("Kerran", 32, 6.2, 9), (v.Runs[0].Race, v.Runs[0].Level!.Value, v.Runs[0].EtaHours!.Value, v.Runs[0].Crawls!.Value));
        Assert.Equal(("Barbarian", 1, null, 0), (v.Runs[1].Race, v.Runs[1].Level!.Value, v.Runs[1].EtaHours, v.Runs[1].Crawls!.Value));
        Assert.Equal(["WAR", "CLR", "RNG"], v.Runs[0].Classes); // Home's order (the journey's trio), not the roster's
        Assert.Equal(["BRD", "DRU", "NEC"], v.Runs[1].Classes); // never played: the sorted key
        Assert.Equal(7, v.Before + v.Runs[0].N);
        Assert.Equal(9, v.Before + v.Runs.First(r => r.State == "next").N);
        Assert.Equal(p.Loadouts.Sum(l => l.Times), v.Left);
        Assert.Equal(p.Loadouts.Sum(l => l.Times) + 2, v.Runs.Count);
        // They aren't in the played-before list, and back at the cap they are history again.
        Assert.Equal(6, v.Played!.Count);
        var (_, back) = Seen([kerran with { Done = true, LevelNow = 50 }], [.. reincs.Select(r => (r.Item1, r.Item2, r.Item3, r.Item1 == "Barbarian" ? 50 : r.Item4 == 32 ? 50 : r.Item4))]);
        var w = Campaign.Evaluate(p, NoClasses, NoRaces, events, back);
        Assert.Equal((8, 0), (w.Before, w.Leveling));
        Assert.Equal("next", w.Runs[0].State);
    }

    /// <summary>An off-plan relevel still open, reincarnated after the plan began, is numbered by when it happened: after the plan's earlier run, not ahead of it.</summary>
    [Fact]
    public void AnOffPlanOpenRelevelAfterThePlanBeganIsNumberedByTimeWithTheMatchedRuns()
    {
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0);
        var p = Campaign.Preset("all");
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        p.Since = t0;
        var a = p.Loadouts[0];
        var (events, relevels) = Seen([], (a.Race, a.Classes, t0.AddDays(1), 50), ("Ogre", ["WAR", "CLR", "MAG"], t0.AddDays(2), 20));
        var v = Campaign.Evaluate(p, NoClasses, NoRaces, events, relevels);
        Assert.Equal(["done", "leveling", "next"], v.Runs.Take(3).Select(r => r.State));
        Assert.Equal([0, -1], v.Runs.Take(2).Select(r => r.Loadout));
        Assert.Equal([1, 2], v.Runs.Take(2).Select(r => r.N));
    }

    /// <summary>
    /// Three reincarnated runs share a class (now at 3/3): the first, a done run in the plan, took it to 1, the next to 2 and the
    /// last to 3, so each has its rank at the time, not today's, and the done run has its date.
    /// </summary>
    [Fact]
    public void ReincarnatedRunsHaveTheirRanksAtTheTimeAndADoneRunItsDate()
    {
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0);
        var p = Campaign.Preset("all");
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        p.Since = t0;
        var a = p.Loadouts[0];
        var shared = a.Classes[0];
        var others = ClassNames.All.Select(c => c.Abbrev).Where(c => !a.Classes.Contains(c)).Take(4).ToList();
        var (events, relevels) = Seen([Open("Ogre", [shared, others[0], others[1]], t0.AddDays(2).AddSeconds(1), 20, null, 3), Open("Troll", [shared, others[2], others[3]], t0.AddDays(3).AddSeconds(1), 20, null, 3)],
            (a.Race, a.Classes, t0.AddDays(1), 50),
            ("Ogre", [shared, others[0], others[1]], t0.AddDays(2), 20),
            ("Troll", [shared, others[2], others[3]], t0.AddDays(3), 20));
        var classes = NoClasses;
        classes[shared] = 3;
        var v = Campaign.Evaluate(p, classes, NoRaces, events, relevels);
        Assert.Equal(["done", "leveling", "leveling"], v.Runs.Take(3).Select(r => r.State));
        int Rank(int i) => v.Runs[i].Gains.Single(g => g.Kind == "class" && g.Key == shared).Rank;
        Assert.Equal([1, 2, 3], new[] { Rank(0), Rank(1), Rank(2) });
        Assert.Equal(t0.AddDays(1), v.Runs[0].At);
        Assert.Equal(1, v.Runs[0].RankAfter);
    }
    private static Journey Open(string race, IReadOnlyList<string> trio, DateTime at, int level, double? eta, int crawls, bool done = false) =>
        new(string.Join(" / ", trio), ClassNames.TrioKey(trio), race, at, at, at, 1, level, done, 0, 0, crawls, 0, 0, 0, 0, 0, [], eta, null, null, []);

    /// <summary>
    /// Feedback #6 (0.10.7): reincarnated runs read done at once and the big number jumped on, while the player was still
    /// leveling them. A run is leveling while its relevel is open, any number of them, numbered by when they happened.
    /// </summary>
    [Fact]
    public void EveryReincarnatedRunBelowTheCapIsLevelingAndNumberedByWhenItHappened()
    {
        var p = Campaign.Preset("all");
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        p.Since = new DateTime(2026, 11, 17, 12, 0, 0);
        var a = p.Loadouts[0]; var b = p.Loadouts[3];
        var events = new List<ReincarnationEvent>
        {
            new(new DateTime(2026, 11, 18), b.Race, b.Classes),   // the later loadout first
            new(new DateTime(2026, 11, 19), a.Race, a.Classes),
        };
        var journeys = new List<Journey>   // a was reincarnated and never played: Journeys.Build makes no journey for it
        {
            Open(b.Race, b.Classes, new DateTime(2026, 11, 18), 32, 6.2, 9),
        };
        var reincs = new (string, IReadOnlyList<string>, DateTime, int)[] { (b.Race, b.Classes, new DateTime(2026, 11, 18), 32), (a.Race, a.Classes, new DateTime(2026, 11, 19), 1) };
        var v = Campaign.Evaluate(p, NoClasses, NoRaces, events, Seen(journeys, reincs).Relevels);
        Assert.Equal(2, v.Done);
        Assert.Equal(["leveling", "leveling", "next"], v.Runs.Take(3).Select(r => r.State));
        Assert.Equal(b.Race, v.Runs[0].Race);
        Assert.Equal((32, 6.2, 9), (v.Runs[0].Level!.Value, v.Runs[0].EtaHours!.Value, v.Runs[0].Crawls!.Value));
        Assert.Equal((1, null, 0), (v.Runs[1].Level!.Value, v.Runs[1].EtaHours, v.Runs[1].Crawls!.Value));
        Assert.Equal(p.Loadouts.Sum(l => l.Times) - 2, v.Left);
        Assert.Equal(p.Loadouts.Sum(l => l.Times), v.Runs.Count);
        // The same runs with the relevels finished: done, and the next run is run 3.
        var finished = journeys.Select(j => j with { Done = true, LevelNow = 50 }).ToList();
        Assert.Equal(["done", "done", "next"], Campaign.Evaluate(p, NoClasses, NoRaces, events, Seen(finished, reincs.Select(r => (r.Item1, r.Item2, r.Item3, 50)).ToArray()).Relevels).Runs.Take(3).Select(r => r.State));
    }

    [Fact]
    public void ARunTickedOffByHandIsDoneWithNoLevel()
    {
        var p = Campaign.Preset("all");
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        p.Loadouts[0].Marked = 1;
        p.Since = new DateTime(2026, 11, 17, 12, 0, 0);
        var l = p.Loadouts[0];
        var open = Open(l.Race, l.Classes, new DateTime(2026, 11, 18), 12, 3, 4);   // a relevel the log saw, but no run of the plan reincarnated it
        var v = Campaign.Evaluate(p, NoClasses, NoRaces, [], Relevels.Of(new Roster(), [open], 50));
        Assert.Equal("done", v.Runs[0].State);
        Assert.Null(v.Runs[0].Level);
    }

    [Fact]
    public void RunsYouPlayedComeFirstAndThePlanCountsOnFromThem()
    {
        // Two reincarnations before the plan, and two more runs your entered counts hold that the log never saw: the plan's
        // first run is run 5, and the campaign is 4 runs longer than the plan.
        var p = Campaign.Preset("classes");
        var events = new List<ReincarnationEvent>
        {
            new(new DateTime(2026, 11, 10), "Ogre", ["WAR", "SHD", "ROG"]),
            new(new DateTime(2026, 11, 12), "Human", ["WAR", "CLR", "RNG"]),
        };
        var classes = NoClasses;
        foreach (var c in new[] { "WAR", "SHD", "ROG", "CLR", "RNG", "WAR", "MNK", "BRD", "ENC", "NEC", "WIZ", "MAG" }) classes[c]++;
        var races = NoRaces;
        races["Ogre"] = 1; races["Human"] = 1; races["Iksar"] = 1; races["Froglok"] = 1;
        p.Loadouts = Campaign.Generate(p, classes, races);
        p.Since = new DateTime(2026, 11, 17);
        var v = Campaign.Evaluate(p, classes, races, events);
        Assert.Equal((2, 4), (v.Unseen, v.Before));
        Assert.Equal(1, v.Runs[0].N);   // the plan's own numbering; the page adds Before
        Assert.Equal([3, 4], v.Played!.Select(x => x.N));
        // What each earned then: WAR's first rank, then its second (the unseen runs were other classes).
        Assert.Equal(["class:WAR:1", "class:SHD:1", "class:ROG:1", "race:Ogre:1"], v.Played![0].Gains.Select(g => $"{g.Kind}:{g.Key}:{g.Rank}"));
        Assert.Contains(new CampaignGain("class", "WAR", 2), v.Played![1].Gains);
        // A reincarnation since the plan began with no loadout for it is played too, at its place in time.
        var (c2, r2) = (new Dictionary<string, int>(classes), new Dictionary<string, int>(races));
        foreach (var c in new[] { "BER", "BST", "DRU" }) c2[c]++;
        r2["Troll"]++;
        var later = Campaign.Evaluate(p, c2, r2, [.. events, new(new DateTime(2026, 11, 18), "Troll", ["BER", "BST", "DRU"])]);
        Assert.True(later.Played![^1].OffPlan);
        Assert.Equal(5, later.Before);
        // No plan yet: everything the log saw is history.
        Assert.Equal(4, Campaign.Evaluate(new CampaignPlan(), classes, races, events).Before);
    }

    [Fact]
    public void RebuildingKeepsWhatsDoneAndWhatsPinned()
    {
        var p = Campaign.Preset("all");
        p.Loadouts =
        [
            new CampaignLoadout { Race = "Iksar", Classes = ["NEC", "MAG", "CLR"], Times = 3 },
            new CampaignLoadout { Race = "Ogre", Classes = ["WAR", "SHM", "ENC"], Times = 3 },
            new CampaignLoadout { Race = "Troll", Classes = ["SHD", "DRU", "BST"], Times = 3 },
        ];
        var pinned = p.Loadouts[2];
        pinned.Pinned = true;
        pinned.Classes = ["WAR", "MNK", "ROG"];
        var classes = NoClasses;
        foreach (var c in p.Loadouts[0].Classes) classes[c] = 2;
        var races = NoRaces;
        races[p.Loadouts[0].Race] = 2;
        var rebuilt = Campaign.Generate(p, classes, races, [2]);
        Assert.Equal(2, rebuilt[0].Times);                       // its two done runs
        Assert.Equal(["WAR", "MNK", "ROG"], rebuilt[1].Classes);   // pinned, as you left it
        var v = Campaign.Evaluate(new CampaignPlan { Classes = p.Classes, Races = p.Races, Loadouts = rebuilt }, classes, races, []);
        Assert.All(v.Goals, g => Assert.True(g.AtEnd >= g.Target, $"{g.Key} {g.AtEnd}/{g.Target}"));
    }

    [Fact]
    public void RunsPastThreeEarnNothing()
    {
        var classes = NoClasses;
        classes["MAG"] = 3;
        var p = new CampaignPlan { Loadouts = [new CampaignLoadout { Race = "Iksar", Classes = ["MAG", "CLR", "WAR"], Times = 1 }] };
        var v = Campaign.Evaluate(p, classes, NoRaces, []);
        Assert.Contains("MAG", v.Runs[0].Past);
        Assert.Equal(4, v.Runs[0].RankAfter);
        Assert.DoesNotContain(v.Runs[0].Gains, g => g.Key == "MAG");
    }

    [Fact]
    public void ReincarnationsComeFromTheRostersTimes()
    {
        var r = new Roster();
        var t = new DateTime(2026, 11, 11, 20, 0, 0);
        r.Race("Half Dark Elf").Seen.Add(t);
        foreach (var c in new[] { "Cleric", "MAG", "NEC" }) r.Class(ClassNames.Abbrev(c)).Seen.Add(t.AddSeconds(1));
        r.Class("WAR").Seen.Add(t.AddDays(-3)); // a different reincarnation's class
        var e = Assert.Single(Campaign.Events(r));
        Assert.Equal("Half Dark Elf", e.Race);
        Assert.Equal("CLR/MAG/NEC", e.TrioKey);
    }

    [Fact]
    public void HandEditedPlansAreCleanedUp()
    {
        var p = new CampaignPlan
        {
            Classes = new() { ["mag"] = 7, ["XXX"] = 3 },
            Races = new() { ["iksar"] = 2, ["Drakkin"] = 3 },
            First = ["class:mag", "race:nowhere"],
            Loadouts = [new CampaignLoadout { Race = "Iksar", Classes = ["MAG", "MAG", "CLR"] }, new CampaignLoadout { Race = "ogre", Classes = ["mag", "clr", "war"], Times = 9 }],
            Since = DateTime.Now,
        }.Normalized();
        Assert.Equal(3, p.Classes["MAG"]);
        Assert.False(p.Classes.ContainsKey("XXX"));
        Assert.Equal(2, p.Races["Iksar"]);
        Assert.False(p.Races.ContainsKey("Drakkin"));
        Assert.Equal(["class:MAG"], p.First);
        var l = Assert.Single(p.Loadouts);
        Assert.Equal("Ogre", l.Race);
        Assert.Equal(3, l.Times);
    }

    /// <summary>Each run says its round (docs/specs/campaign-run-list.md): the lowest class rank it raises, or 0 for a run that raises none (the page's Races).</summary>
    [Fact]
    public void EveryRunToPlaySaysItsRound()
    {
        var v = Play(Campaign.Preset("all"));
        var toPlay = v.Runs.Where(r => r.State is "next" or "planned").ToList();
        Assert.All(toPlay, r => Assert.Equal(r.Gains.Where(g => g.Kind == "class").Select(g => g.Rank).DefaultIfEmpty(0).Min(), r.Round));
        Assert.Equal(1, toPlay[0].Round);
        Assert.Equal(0, toPlay[^1].Round);
        Assert.Equal([0, 1, 2, 3], toPlay.Select(r => r.Round).Distinct().Order());
    }

    /// <summary>A leveling run's line has pips, gold names and an XP to level like a run to play: from the ranks you have now, which its reincarnation raised.</summary>
    [Fact]
    public void ALevelingRunSendsItsGainsAndItsXpToLevel()
    {
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0);
        var kerran = Open("Kerran", ["WAR", "CLR", "RNG"], t0.AddSeconds(1), 32, 6.2, 9);
        var (events, relevels) = Seen([kerran], ("Kerran", ["CLR", "RNG", "WAR"], t0, 32));
        var p = Campaign.Preset("all");
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        p.Since = t0.AddDays(1);
        var classes = NoClasses;
        classes["WAR"] = 1; classes["CLR"] = 4; classes["RNG"] = 2;   // CLR is past 3/3
        var races = NoRaces;
        races["Kerran"] = 3;
        var run = Campaign.Evaluate(p, classes, races, events, relevels).Runs[0];
        Assert.Equal("leveling", run.State);
        Assert.Equal([("class", "WAR", 1), ("class", "RNG", 2), ("race", "Kerran", 3)], run.Gains.Select(g => (g.Kind, g.Key, g.Rank)));
        Assert.Equal(["CLR"], run.Past);
        Assert.Equal(4, run.RankAfter);
        Assert.Equal(Planner.XpMultiplier(4), run.Xp);
        Assert.Equal(1, run.Round);
    }

    /// <summary>Rule 22: the app doesn't flag a trio the player chose, and says nothing about a race already at 3/3 (the page's grey name and key say it).</summary>
    [Fact]
    public void NoLoadoutWarnsAboutTwoPetsOrARacePassiveThatIsNotEarned()
    {
        var races = NoRaces;
        foreach (var r in races.Keys.ToList()) races[r] = 3;
        var v = Play(Campaign.Preset("all"), races: races);
        Assert.All(v.Loadouts, row => Assert.All(row.Warnings, w => { Assert.DoesNotContain("Two pets", w); Assert.DoesNotContain("race passive", w); }));
        Assert.Contains(v.Plan.Loadouts, l => l.Classes.Count(c => c is "MAG" or "NEC" or "BST") >= 2);
    }
}
