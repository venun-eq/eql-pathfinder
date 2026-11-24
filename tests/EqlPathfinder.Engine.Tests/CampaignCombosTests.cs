using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Your combos (docs/specs/campaign-your-combos.md): a Must is held above the cheapest path at its price, a Prefer only
/// where it's free, a Never keeps classes apart, past 3/3 every combo is a Prefer, and the goal a Must goes past says so.
/// </summary>
public class CampaignCombosTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);

    private static CampaignCombo Trio(string a, string b, string c, int times = 3, bool must = true, bool unlock = false) =>
        new() { Classes = [a, b, c], Times = times, Must = must, UnlockNow = unlock };
    private static CampaignCombo Pair(string a, string b, bool must = true, bool everyRun = true) =>
        new() { Classes = [a, b], Must = must, EveryRun = everyRun };
    private static CampaignCombo Never(params string[] classes) => new() { Classes = [.. classes], Apart = true };

    private static CampaignPlan Goals(string preset, params CampaignCombo[] combos)
    {
        var p = Campaign.Preset(preset);
        p.Combos = [.. combos];
        return p;
    }

    private static (CampaignPlan Plan, CampaignView View) Planned(CampaignPlan p)
    {
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces);
        return (p, Campaign.Evaluate(p, NoClasses, NoRaces, []));
    }

    private static List<CampaignRun> RunsOf(CampaignView v, params string[] trio) =>
        v.Runs.Where(r => trio.All(r.Classes.Contains)).ToList();

    private static string Seated(CampaignView v) => string.Join("; ", v.Runs.Select(r => $"{r.N}:{string.Join("/", r.Classes)}"));

    private static CampaignComboPrice Price(CampaignPlan p, CampaignCombo c) => Campaign.ComboPrice(p, c, NoClasses, NoRaces, [], null);

    [Fact]
    public void AMustTrioIsPlayedAsManyTimesAsItSaysAndCostsNothingWhereItsFree()
    {
        var (_, without) = Planned(Goals("classes"));
        var (_, v) = Planned(Goals("classes", Trio("SHD", "ROG", "SHM")));
        Assert.True(RunsOf(v, "SHD", "ROG", "SHM").Count == 3, $"SHD / ROG / SHM isn't played 3 times: {Seated(v)}");
        Assert.Equal(without.Left, v.Left);
        Assert.Equal(without.TotalXp, v.TotalXp, 2);

        var (_, once) = Planned(Goals("classes", Trio("WAR", "CLR", "MNK", times: 1)));
        Assert.True(RunsOf(once, "WAR", "CLR", "MNK").Count >= 1, $"WAR / CLR / MNK isn't played once: {Seated(once)}");
    }

    [Fact]
    public void UnlockNowPlaysTheTrioBackToBackForTheSameRunsAndXp()
    {
        var (_, without) = Planned(Goals("classes"));
        var (_, v) = Planned(Goals("classes", Trio("SHD", "ROG", "SHM", unlock: true)));
        var ns = RunsOf(v, "SHD", "ROG", "SHM").Select(r => r.N).ToList();
        Assert.True(ns.Count == 3 && ns[1] == ns[0] + 1 && ns[2] == ns[1] + 1, $"SHD / ROG / SHM isn't back to back: {Seated(v)}");
        Assert.Equal(without.Left, v.Left);
        // The design's example costs no XP; whatever a trio's back-to-back runs do cost, its price says, before it's set.
        var (now, _) = Planned(Goals("classes"));
        Assert.Equal(v.TotalXp - without.TotalXp, Price(now, Trio("SHD", "ROG", "SHM", unlock: true)).Delta.Xp, 2);
        Assert.Equal([ns[0], ns[1], ns[2]], Price(now, Trio("SHD", "ROG", "SHM", unlock: true)).Runs);
    }

    /// <summary>A goal of WAR 1 is one run; WAR / CLR / MNK 3 times as a Must is three, and its price says +2 before it's added.</summary>
    [Fact]
    public void AMustThatCostsIsHeldAboveTheCheapestPathAndShowsItsPriceFirst()
    {
        var plan = new CampaignPlan { Classes = new(StringComparer.OrdinalIgnoreCase) { ["WAR"] = 1 } };
        var (now, without) = Planned(plan);
        var price = Price(now, Trio("WAR", "CLR", "MNK"));
        Assert.Equal(2, price.Delta.Runs);
        Assert.Equal(3, price.Kept);
        Assert.Equal(3, price.Of);

        var withIt = new CampaignPlan { Classes = new(plan.Classes, StringComparer.OrdinalIgnoreCase), Combos = [Trio("WAR", "CLR", "MNK")] };
        var (_, v) = Planned(withIt);
        Assert.Equal(without.Left + 2, v.Left);
        Assert.Equal(3, RunsOf(v, "WAR", "CLR", "MNK").Count);
    }

    [Fact]
    public void APreferIsKeptOnlyWhereFreeAndSaysWhatAMustWouldCost()
    {
        var goal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["WAR"] = 1 };
        var (now, without) = Planned(new CampaignPlan { Classes = new(goal, StringComparer.OrdinalIgnoreCase) });
        var price = Price(now, Trio("WAR", "CLR", "MNK", must: false));
        Assert.Equal(0, price.Delta.Runs);
        Assert.Equal(1, price.Kept);
        Assert.Equal(3, price.Of);
        Assert.Equal(2, price.AsMust?.Runs);

        var (_, v) = Planned(new CampaignPlan { Classes = new(goal, StringComparer.OrdinalIgnoreCase), Combos = [Trio("WAR", "CLR", "MNK", must: false)] });
        Assert.Equal(without.Left, v.Left);
        Assert.Single(RunsOf(v, "WAR", "CLR", "MNK"));
    }

    /// <summary>Every run that earns PAL's or MNK's ranks has both; past 3/3 (the races' 32 runs) it costs no run and no XP.</summary>
    [Fact]
    public void AMustPairSitsTogetherInEveryRunThatEarnsTheirRanksAndPastThreeOnlyWhereFree()
    {
        var (_, without) = Planned(Goals("all"));
        var (_, v) = Planned(Goals("all", Pair("PAL", "MNK")));
        var earning = v.Runs.Where(r => r.Gains.Any(g => g.Kind == "class" && g.Key is "PAL" or "MNK")).ToList();
        Assert.NotEmpty(earning);
        Assert.All(earning, r => Assert.True(r.Classes.Contains("PAL") && r.Classes.Contains("MNK"), $"run {r.N} splits PAL and MNK: {Seated(v)}"));
        Assert.Equal(without.Left, v.Left);
        Assert.Equal(without.TotalXp, v.TotalXp, 2);

        var (_, once) = Planned(Goals("classes", Pair("WIZ", "BER", everyRun: false)));
        Assert.True(RunsOf(once, "WIZ", "BER").Count >= 1, $"WIZ and BER never sit together: {Seated(once)}");
    }

    [Fact]
    public void ANeverKeepsTheClassesApartInEveryRun()
    {
        var (_, two) = Planned(Goals("all", Never("WAR", "BER")));
        Assert.Empty(RunsOf(two, "WAR", "BER"));
        var (_, three) = Planned(Goals("all", Never("CLR", "DRU", "SHM")));
        Assert.All(three.Runs, r => Assert.True(r.Classes.Count(c => c is "CLR" or "DRU" or "SHM") < 2, $"run {r.N} has two of CLR, DRU, SHM: {Seated(three)}"));
    }

    /// <summary>The user, 11-19: the combo wins, the goal is never raised, and the goal tile says which combo took the class past it.</summary>
    [Fact]
    public void AMustPastAGoalWinsAndTheGoalSaysSo()
    {
        var p = Goals("classes", Trio("SHD", "ROG", "SHM"));
        p.Classes["SHD"] = 2;
        p.Classes.Remove("ROG");                          // no goal: no line, its planned pips already show it
        var (plan, v) = Planned(p);
        Assert.Equal(3, RunsOf(v, "SHD", "ROG", "SHM").Count);
        Assert.Equal(2, plan.Classes["SHD"]);
        var shd = v.Goals.Single(g => g.Kind == "class" && g.Key == "SHD");
        Assert.Equal((2, 3), (shd.Target, shd.AtEnd));
        var past = Assert.Single(v.PastGoals ?? []);
        Assert.Equal(0, past.Combo);
        Assert.Equal(["SHD"], past.Classes);
    }

    [Fact]
    public void AClashIsFoundWhenTheComboIsAddedWithBothWaysOut()
    {
        // MNK has 3 runs to earn its ranks; this trio takes all of them, so PAL would never sit with MNK.
        var (pair, _) = Planned(Goals("classes", Pair("PAL", "MNK")));
        var clash = Price(pair, Trio("WAR", "CLR", "MNK")).Clash;
        Assert.Equal((0, (int?)null), (clash?.Combo, clash?.Loadout));   // the clash also names the class and its runs (CampaignComboRowsTests)
        Assert.Null(Price(pair, Trio("WAR", "CLR", "MNK", must: false)).Clash);   // "Add it as Prefer"

        var (never, _) = Planned(Goals("classes", Never("WAR", "BER")));
        Assert.Equal(new CampaignClash(0, null), Price(never, Trio("WAR", "CLR", "BER")).Clash);

        // A pinned loadout with its runs to go seats WAR in every run WAR has left.
        var pinned = new CampaignPlan
        {
            Classes = new(StringComparer.OrdinalIgnoreCase) { ["WAR"] = 3 },
            Loadouts = [new CampaignLoadout { Race = ClassNames.Races[0], Classes = ["WAR", "RNG", "SHM"], Times = 3, Pinned = true }],
        };
        Assert.Equal(new CampaignClash(null, 0), Price(pinned, Trio("WAR", "CLR", "MNK")).Clash);
    }

    [Fact]
    public void SwapNeverOffersAChangeThatBreaksAMust()
    {
        var (p, v) = Planned(Goals("classes", Trio("SHD", "ROG", "SHM")));
        int at = p.Loadouts.FindIndex(l => new[] { "SHD", "ROG", "SHM" }.All(l.Classes.Contains));
        Assert.True(at >= 0, $"no loadout holds the Must trio: {Seated(v)}");
        var options = Campaign.Options(p, NoClasses, NoRaces, [], null, at)!;
        Assert.All(options.Swaps, s => Assert.True(new[] { "SHD", "ROG", "SHM" }.All(s.Loadouts[at].Classes.Contains), $"\"{s.Label}\" breaks the Must"));
    }

    /// <summary>settings.json can be hand-edited: real classes once, 2 or 3 of them, a trio 1 to 3 times, no repeat; Unlock now is a Must.</summary>
    [Fact]
    public void CombosAreKeptWithThePlanAndAHandEditedListIsCleaned()
    {
        var p = Goals("classes",
            Trio("SHD", "ROG", "SHM"),
            Pair("PAL", "MNK", must: false),
            Pair("WAR", "XYZ"),
            new CampaignCombo { Classes = ["WAR"] },
            new CampaignCombo { Classes = ["WAR", "CLR", "MNK", "BER"] },
            Pair("mnk", "PAL", must: false),                         // the same combo again
            Trio("WAR", "CLR", "MNK", times: 7),
            new CampaignCombo { Classes = ["BER", "ROG"], Apart = true, UnlockNow = true },
            Trio("ENC", "BRD", "NEC", must: false, unlock: true));
        var kept = p.Normalized().Combos ?? [];
        Assert.Equal(["SHD/ROG/SHM", "PAL/MNK", "WAR/CLR/MNK", "BER/ROG", "ENC/BRD/NEC"], kept.Select(c => string.Join("/", c.Classes)));
        Assert.Equal(3, kept[2].Times);
        Assert.False(kept[3].UnlockNow);                                // apart has no runs to play back to back
        Assert.True(kept[4].Must);                                      // Unlock now is held like a Must
    }

    private sealed class Settings : IPlannerSettings
    {
        public Dictionary<string, CampaignPlan> Campaigns { get; set; } = new();
        public Dictionary<string, NewCampaignPlan> NewCampaigns { get; set; } = new();
    }

    private static CampaignPlan EveryClass3(params CampaignCombo[] combos) =>
        new() { Classes = new(ClassNames.All.ToDictionary(c => c.Abbrev, _ => 3), StringComparer.OrdinalIgnoreCase), Combos = [.. combos] };

    /// <summary>A Must trio added after an Unlock-now trio doesn't come between its runs.</summary>
    [Fact]
    public void AMustAddedAfterUnlockNowDoesNotComeBetweenItsRuns()
    {
        var unlock = new CampaignCombo { Classes = ["SHD", "ROG", "SHM"], Times = 3, UnlockNow = true, Must = true };
        var must = new CampaignCombo { Classes = ["WAR", "CLR", "PAL"], Times = 3, Must = true };
        var p = EveryClass3(unlock, must);
        var ls = Campaign.Generate(p, ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0), ClassNames.Races.ToDictionary(r => r, _ => 0));
        var seq = Campaign.Sequence(ls).Select(x => ClassNames.TrioKey(ls[x.Loadout].Classes)).ToList();
        string key = ClassNames.TrioKey(["SHD", "ROG", "SHM"]);
        int first = seq.IndexOf(key);
        Assert.Equal(3, seq.Count(k => k == key));
        Assert.Equal([key, key, key], seq.Skip(first).Take(3));
    }

    /// <summary>A Must trio "once" already played outside the plan isn't planned again.</summary>
    [Fact]
    public void AMustTrioAlreadyPlayedOutsideThePlanIsNotPlannedAgain()
    {
        var none = ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
        var races = ClassNames.Races.ToDictionary(r => r, _ => 0);
        var p = new CampaignPlan
        {
            Classes = new(StringComparer.OrdinalIgnoreCase) { ["WAR"] = 1 },
            Since = new DateTime(2026, 11, 1),
            Combos = [new CampaignCombo { Classes = ["SHD", "ROG", "SHM"], Times = 1, Must = true }],
        };
        var played = new ReincarnationEvent(new DateTime(2026, 11, 2), "Iksar", ["SHD", "ROG", "SHM"]);
        var (done, off) = Campaign.DoneCounts(p, [played]);
        Assert.Single(off);
        var ls = Campaign.Generate(p, none, races, done, null, off);
        Assert.DoesNotContain(ls, l => ClassNames.TrioKey(l.Classes) == ClassNames.TrioKey(["SHD", "ROG", "SHM"]));
    }

    /// <summary>Generate (classes first and the races after) never seats a Never's classes together, whatever the ranks, goals and other combos.</summary>
    [Fact]
    public void NoGeneratedRunSeatsTheClassesOfANever()
    {
        var rng = new Random(11);
        var abbrevs = ClassNames.All.Select(c => c.Abbrev).ToList();
        var bad = new List<string>();
        for (int i = 0; i < 120; i++)
        {
            var ranks = abbrevs.ToDictionary(c => c, _ => rng.Next(0, 4));
            var races = ClassNames.Races.ToDictionary(r => r, _ => rng.Next(0, 4));
            var nv = new[] { "RNG", "BST" }.Concat(i % 3 == 0 ? [abbrevs[rng.Next(abbrevs.Count)]] : []).Distinct().ToArray();
            var combos = new List<CampaignCombo> { Never(nv) };
            if (i % 2 == 0) combos.Add(Trio("SHD", "ROG", "SHM", unlock: i % 4 == 0));
            if (i % 5 == 0) combos.Add(Pair("PAL", "MNK"));
            if (i % 7 == 0) combos.Add(Trio("WAR", "CLR", "MNK", must: false));
            var p = Campaign.Preset(i % 3 == 0 ? "classes" : "all");
            p.Combos = combos;
            p.Loadouts = Campaign.Generate(p, ranks, races);
            foreach (var l in p.Loadouts)
                if (nv.Count(l.Classes.Contains) >= 2) bad.Add($"#{i} {string.Join("/", nv)} in {string.Join("/", l.Classes)}");
        }
        Assert.True(bad.Count == 0, string.Join("; ", bad.Take(8)));
    }

    /// <summary>On the start page (no runs yet) a combo already written isn't "changed by hand": there is nothing to break.</summary>
    [Fact]
    public void AComboWrittenBeforeThePlanHasRunsIsNotBroken()
    {
        var p = new CampaignPlan { Combos = [Trio("SHD", "ROG", "SHM"), Never("WAR", "BER"), Pair("PAL", "MNK")] };
        var v = Campaign.Evaluate(p, NoClasses, NoRaces, []);
        Assert.Empty(Campaign.Broken(p, v));
    }
}
