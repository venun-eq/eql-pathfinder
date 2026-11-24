using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// What you want first (docs/specs/campaign-first-lane.md, branch A): ranked classes and races, each Spread out or Unlock now,
/// placed by the planner; races nobody ranked are don't care; and every word the page draws for them comes from the engine.
/// </summary>
public class CampaignFirstLaneTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);

    private static CampaignWant W(string key, int target = 3, bool now = false) => new() { Key = key, Target = target, UnlockNow = now };

    /// <summary>The plan as the new store holds it: the ranked lists, and the goals they are (a ranked row's pips are its goal).</summary>
    private static CampaignPlan Plan(string? preset, CampaignWant[] classes, CampaignWant[] races, params CampaignCombo[] combos)
    {
        var p = preset == null ? new CampaignPlan() : Campaign.Preset(preset);
        p.ClassOrder = classes.Length == 0 ? null : [.. classes];
        p.RaceOrder = races.Length == 0 ? null : [.. races];
        foreach (var c in classes) p.Classes[c.Key] = c.Target;
        foreach (var r in races) p.Races[r.Key] = r.Target;
        p.Combos = combos.Length == 0 ? null : [.. combos];
        return p;
    }

    private static CampaignCombo Must(string a, string b, string c) => new() { Classes = [a, b, c], Times = 3, Must = true };
    private static CampaignCombo Never(string a, string b) => new() { Classes = [a, b], Apart = true };

    private static CampaignView Planned(CampaignPlan p, Dictionary<string, int>? cr = null, Dictionary<string, int>? rr = null,
        List<ReincarnationEvent>? events = null)
    {
        p.Loadouts = Campaign.Generate(p, cr ?? NoClasses, rr ?? NoRaces);
        return Campaign.Evaluate(p, cr ?? NoClasses, rr ?? NoRaces, events ?? []);
    }

    private static CampaignFirstLane Lane(CampaignPlan p, Dictionary<string, int>? cr = null, Dictionary<string, int>? rr = null,
        List<ReincarnationEvent>? events = null)
    {
        var v = Planned(p, cr, rr, events);
        return Campaign.FirstLane(p, v, cr ?? NoClasses, rr ?? NoRaces, events ?? [], null);
    }

    private static string? Line(IReadOnlyList<CampaignFirstRow> rows, string key) => rows.FirstOrDefault(r => r.Key == key)?.Line;
    private static string Seated(CampaignView v) => string.Join("; ", v.Runs.Select(r => $"{r.N}:{r.Race} {string.Join("/", r.Classes)}"));
    private static List<int> RunsWith(CampaignView v, string cls) => v.Runs.Where(r => r.Classes.Contains(cls)).Select(r => r.N).ToList();
    private static string RunList(List<int> ns) => ns.Count == 1 ? $"run {ns[0]}" : "runs " + string.Join(", ", ns.Take(ns.Count - 1)) + " and " + ns[^1];

    // ------------------------------------------------------------------ the planner

    [Fact]
    public void RankedRacesThatFitInTheClassRunsCostNoMoreXpToLevel()
    {
        var without = Planned(Plan("classes", [], []));
        var v = Planned(Plan("classes", [], [W("Iksar"), W("Troll", 2)]));
        Assert.True(v.Goals.Where(g => g.Kind == "race").All(g => g.AtEnd >= g.Target), $"a ranked race misses its goal: {Seated(v)}");
        Assert.Equal(without.Left, v.Left);
        Assert.True(Math.Abs(without.TotalXp - v.TotalXp) < 0.005,
            $"ranking Iksar 3/3 and Troll 2/3 costs XP to level ({v.TotalXp} against {without.TotalXp}), though they fit in the class runs: {Seated(v)}");
    }

    [Fact]
    public void RankedRacesGoInThePlayersOrderWhenTheyCostTheSame()
    {
        var v = Planned(Plan(null, [], [W("Iksar", 1), W("Troll", 1), W("Human", 1)]));
        Assert.Equal("Iksar, Troll, Human", string.Join(", ", v.Runs.Select(r => r.Race)));
    }

    [Fact]
    public void UnlockNowRacesTakeTheNextRunsOneAfterAnotherInRankOrder()
    {
        var v = Planned(Plan("classes", [], [W("Troll", 2, now: true), W("Iksar", 2, now: true)]));
        Assert.True(string.Join(",", v.Runs.Take(4).Select(r => r.Race)) == "Troll,Troll,Iksar,Iksar",
            $"Troll then Iksar aren't the next four runs: {Seated(v)}");
    }

    [Fact]
    public void AnUnlockNowClassTakesTheNextRunsBackToBack()
    {
        var v = Planned(Plan("classes", [W("ROG", now: true)], []));
        Assert.True(RunsWith(v, "ROG").SequenceEqual([1, 2, 3]), $"ROG isn't in runs 1 to 3: {Seated(v)}");
    }

    [Fact]
    public void UpToThreeUnlockNowClassesShareRunsAndAFourthComesAfter()
    {
        var v = Planned(Plan("classes", [W("ROG", now: true), W("BER", now: true), W("SHM", now: true), W("WAR", now: true)], []));
        foreach (var c in new[] { "ROG", "BER", "SHM" })
            Assert.True(RunsWith(v, c).SequenceEqual([1, 2, 3]), $"{c} isn't in runs 1 to 3: {Seated(v)}");
        Assert.True(RunsWith(v, "WAR").SequenceEqual([4, 5, 6]), $"WAR isn't in runs 4 to 6, after ROG, BER and SHM: {Seated(v)}");
    }

    [Fact]
    public void AnUnlockNowClassInAMustTrioBringsTheTrioWithIt()
    {
        var v = Planned(Plan("classes", [W("ROG", now: true)], [], Must("SHD", "ROG", "SHM")));
        Assert.True(v.Runs.Take(3).All(r => new[] { "SHD", "ROG", "SHM" }.All(r.Classes.Contains)),
            $"SHD / ROG / SHM didn't move to runs 1 to 3 with ROG: {Seated(v)}");
    }

    [Fact]
    public void ANeverComboKeepsUnlockNowClassesApart()
    {
        var v = Planned(Plan("classes", [W("ROG", now: true), W("BER", now: true)], [], Never("ROG", "BER")));
        Assert.True(RunsWith(v, "ROG").SequenceEqual([1, 2, 3]), $"ROG isn't in runs 1 to 3: {Seated(v)}");
        Assert.True(RunsWith(v, "BER").SequenceEqual([4, 5, 6]), $"BER isn't in runs 4 to 6, after ROG: {Seated(v)}");
    }

    // ------------------------------------------------------------------ the engine's words

    [Fact]
    public void ARankedRaceAtItsTargetNeverEarnsARankPastIt()
    {
        var v = Planned(Plan("classes", [], [W("Iksar"), W("Troll", 2)]));
        Assert.Equal(2, v.Runs.Where(r => r.Race == "Troll").Count());
        Assert.Equal(3, v.Runs.Where(r => r.Race == "Iksar").Count());
    }

    [Fact]
    public void ARowAtItsTargetSaysItIsDone()
    {
        var rr = NoRaces;
        rr["Iksar"] = 3;
        Assert.Equal("3/3 already.", Line(Lane(Plan("classes", [], [W("Iksar")]), rr: rr).Races, "Iksar"));

        var cr = NoClasses;
        foreach (var c in new[] { "WAR", "CLR", "MNK" }) cr[c] = 3;
        var played = Enumerable.Range(1, 3).Select(d => new ReincarnationEvent(new DateTime(2026, 11, d), "Iksar", ["WAR", "CLR", "MNK"])).ToList();
        Assert.Equal("3/3: done at run 3.", Line(Lane(Plan(null, [], [W("Iksar")]), cr, rr, played).Races, "Iksar"));
    }

    [Fact]
    public void AnUnlockNowRowBelowThreeSaysTheActiveUnlocksAtThree()
    {
        var line = Line(Lane(Plan("classes", [W("ROG", 2, now: true)], [])).Classes, "ROG");
        Assert.StartsWith("Unlock now: runs 1–2, back to back, to 2/3. ROG's active unlocks at 3/3.", line ?? "(no line)");
    }

    [Fact]
    public void AnUnlockNowRowWithOneRunSaysItIsTheNextOneNotBackToBack()
    {
        var line = Line(Lane(Plan("classes", [], [W("Iksar", 1, now: true)])).Races, "Iksar");
        Assert.StartsWith("Unlock now: run 1, the next one", line ?? "(no line)");
        Assert.DoesNotContain("back to back", line ?? "");
    }

    [Fact]
    public void ASecondUnlockNowRaceSaysItComesAfterTheFirst()
    {
        var line = Line(Lane(Plan("classes", [], [W("Troll", 2, now: true), W("Iksar", 2, now: true)])).Races, "Iksar");
        Assert.StartsWith("Unlock now: runs 3–4, after Troll.", line ?? "(no line)");
    }

    [Fact]
    public void UnlockNowClassesTogetherSayWhoTheyRideWith()
    {
        var line = Line(Lane(Plan("classes", [W("ROG", now: true), W("BER", now: true)], [])).Classes, "BER");
        Assert.StartsWith("Unlock now: runs 1–3, with ROG.", line ?? "(no line)");
    }

    [Fact]
    public void ANeverComboSaysWhyAnUnlockNowClassWaits()
    {
        var line = Line(Lane(Plan("classes", [W("ROG", now: true), W("BER", now: true)], [], Never("ROG", "BER"))).Classes, "BER");
        Assert.StartsWith("Unlock now: runs 4–6, after ROG (your combo Never ROG with BER keeps them apart).", line ?? "(no line)");
    }

    [Fact]
    public void ALockedRankedRaceSaysItIsPlannedLast()
    {
        var p = Plan("classes", [], [W("Froglok")]);
        p.Locked = ["Froglok"];
        Assert.Equal("Froglok is locked, so it's planned last. Unlock it in the game, then tick it off in More options.", Line(Lane(p).Races, "Froglok"));
    }

    [Fact]
    public void ASpreadOutRowSaysWhereItLandsAndWhatUnlockNowWouldCost()
    {
        // ROG alone is pulled to runs 1 to 3 by its rank (a rank breaks ties, and every class run has a free seat), so it is the fifth of five ranked.
        var p = Plan("classes", [W("WAR"), W("CLR"), W("PAL"), W("RNG"), W("ROG")], []);
        var v = Planned(p);
        var lands = RunsWith(v, "ROG");
        Assert.False(lands.SequenceEqual([1, 2, 3]), $"ROG Spread out is already back to back, so this test proves nothing: {Seated(v)}");
        var line = Line(Campaign.FirstLane(p, v, NoClasses, NoRaces, [], null).Classes, "ROG");
        Assert.StartsWith($"It lands in {RunList(lands)}. Unlock now would play it in runs 1–3", line ?? "(no line)");
    }
}

public class CampaignFirstLaneCopyTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);

    /// <summary>A ranked plan with ROG Unlock now and Troll Unlock now, planned, not planned yet.</summary>
    private static CampaignPlan Short()
    {
        var p = Campaign.Preset("classes");
        p.ClassOrder = [new() { Key = "ROG", Target = 3, UnlockNow = true }];
        p.RaceOrder = [new() { Key = "Troll", Target = 2, UnlockNow = true }];
        p.Classes["ROG"] = 3; p.Races["Troll"] = 2;
        p.Loadouts = [];   // nothing planned yet: every run a fix gives is built from the copy
        return p;
    }

    [Fact]
    public void EveryPlanCopyKeepsTheRankedLists()
    {
        var p = Short();
        var changes = Campaign.Fixes(p, NoClasses, NoRaces, [], null);
        Assert.NotEmpty(changes);
        foreach (var c in changes)
        {
            var q = Campaign.Preset("classes");
            q.ClassOrder = p.ClassOrder; q.RaceOrder = p.RaceOrder; q.Classes["ROG"] = 3; q.Races["Troll"] = 2;
            q.Loadouts = [.. c.Loadouts];
            var v = Campaign.Evaluate(q, NoClasses, NoRaces, []);
            var rog = v.Runs.Where(r => r.Classes.Contains("ROG")).Select(r => r.N).ToList();
            Assert.True(rog.Take(3).SequenceEqual([1, 2, 3]), $"\"{c.Label}\" priced a plan without the lists: ROG is in runs {string.Join(",", rog)}");
            Assert.Equal("Troll", v.Runs[0].Race);
        }
    }
}

public class CampaignFirstClashTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);

    private static CampaignPlan Pinned(bool second)
    {
        var p = Campaign.Preset("classes");
        p.ClassOrder = [new() { Key = "ROG", Target = 3 }];
        p.Classes["ROG"] = 3;
        p.Loadouts =
        [
            new() { Race = "Half Elf", Classes = ["WAR", "RNG", "ROG"], Times = 1, Pinned = true },
            new() { Race = "Human", Classes = ["CLR", "ENC", "MAG"], Times = 1, Pinned = second },
        ];
        return p;
    }

    [Fact]
    public void UnlockNowThatClashesWithAPinSaysWhichLoadout()
    {
        var want = new CampaignWant { Key = "ROG", Target = 3, UnlockNow = true };
        var both = Campaign.FirstPrice(Pinned(true), "class", want, NoClasses, NoRaces, [], null);
        Assert.Equal(0, both!.ClashLoadout);
        Assert.Equal("Clashes with your pinned loadout 1 (Half Elf · WAR / RNG / ROG): it seats ROG in run 1.", both.Clash);
        var one = Campaign.FirstPrice(Pinned(false), "class", want, NoClasses, NoRaces, [], null);
        Assert.Null(one!.Clash);
        Assert.Null(one.ClashLoadout);
    }
}
