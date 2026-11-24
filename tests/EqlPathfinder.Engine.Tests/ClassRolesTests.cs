using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The new planner's class roles (docs/specs/campaign-class-roles.md): between seatings that cost the same runs and XP,
/// every trio holds up where you level, one strong slow per trio, haste to the classes that hit with weapons, then your
/// carries in your order. The old planner plans exactly as before.
/// </summary>
public class ClassRolesTests
{
    private static Dictionary<string, int> NoClasses => ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
    private static Dictionary<string, int> NoRaces => ClassNames.Races.ToDictionary(r => r, _ => 0);

    private static readonly string[] HitWithWeapons = ["RNG", "MNK", "BER", "ROG"], GiveHaste = ["ENC", "SHM", "BRD"];
    private static readonly CampaignRoles Pathfinders = new(["MAG", "NEC", "ENC", "RNG"], true);

    /// <summary>
    /// The user's rule (11-19), written here apart from the engine's table: ENC or BRD holds a trio up alone; otherwise two
    /// of a tank (WAR, SHD, PAL), heals (CLR, DRU, SHM, PAL), a slow (SHM, ENC, BRD) and crowd control (ENC, BRD). BST's
    /// weak slows, ROG's poison, NEC's undead-only slow and charm and DRU's animal-only charm don't count.
    /// </summary>
    private static bool HoldsUp(IReadOnlyList<string> t)
    {
        if (t.Contains("ENC") || t.Contains("BRD")) return true;
        bool tank = t.Any(c => c is "WAR" or "SHD" or "PAL"), heals = t.Any(c => c is "CLR" or "DRU" or "SHM" or "PAL"),
            slow = t.Contains("SHM");
        return (tank ? 1 : 0) + (heals ? 1 : 0) + (slow ? 1 : 0) >= 2;
    }

    private static string Trios(CampaignPlan p) => string.Join("; ", p.Loadouts.Select(l => string.Join("/", l.Classes)));

    private static (CampaignPlan Plan, CampaignView View) Planned(string preset, Func<IReadOnlyList<string>, double> quality, List<string>? prefer = null)
    {
        var p = Campaign.Preset(preset);
        if (prefer != null) p.Prefer = prefer;
        p.Loadouts = Campaign.Generate(p, NoClasses, NoRaces, null, quality);
        return (p, Campaign.Evaluate(p, NoClasses, NoRaces, []));
    }

    /// <summary>
    /// A seating worked out by hand, to show the rule can be met at no cost: the 16 classes around a circle, and each run the
    /// next three of them, as the old planner seats them (every class three times, ranks kept even). "all" runs the same
    /// 16 trios twice more for the races.
    /// </summary>
    private static (CampaignPlan Plan, CampaignView View) ByHand(string preset, string circle)
    {
        var c = circle.Split(' ');
        Assert.Equal(16, c.Distinct().Count());
        var p = Campaign.Preset(preset);
        var trios = Enumerable.Range(0, 16).Select(k => new CampaignLoadout
        {
            Race = ClassNames.Races[k],
            Classes = [c[3 * k % 16], c[(3 * k + 1) % 16], c[(3 * k + 2) % 16]],
            Times = 1,
        }).ToList();
        p.Loadouts = preset == "all" ? [.. trios, .. trios.Select(l => { var x = l.Copy(); x.Times = 2; return x; })] : trios;
        return (p, Campaign.Evaluate(p, NoClasses, NoRaces, []));
    }

    /// <summary>Every trio holds up, SHM and ENC apart, each haste giver beside a weapon class, each carry in a trio of its own.</summary>
    private const string EveryRuleMet = "SHM WIZ MNK ENC BST BER BRD MAG ROG PAL NEC WAR CLR SHD DRU RNG";

    private static void CostsTheSame(CampaignView expected, CampaignView actual, string what)
    {
        Assert.Equal(expected.Left, actual.Left);
        Assert.True(Math.Abs(expected.TotalXp - actual.TotalXp) < 1e-9, $"{what}: {actual.TotalXp} XP to level against {expected.TotalXp}");
    }

    [Fact]
    public void ATrioHoldsUpWithEncOrBrdOrTwoOfTankHealsSlowAndCrowdControl()
    {
        var cases = new (string Trio, bool HoldsUp)[]
        {
            ("WAR/CLR/RNG", true),    // tank and heals: the user's own trio
            ("RNG/DRU/BER", false),   // heals only
            ("ENC/WIZ/MAG", true),    // area mez holds up to 30 mobs
            ("BRD/WIZ/MAG", true),    // a slow and crowd control
            ("SHM/WIZ/MAG", true),    // heals and a slow
            ("PAL/WIZ/MAG", true),    // tank and heals in one seat
            ("SHD/WIZ/MAG", false),   // lifetaps aren't heals
            ("WAR/BST/MAG", false),   // BST's slows are weak: not a slow
            ("WAR/ROG/MAG", false),   // poison isn't a slow
            ("WAR/NEC/MAG", false),   // NEC's slow and charm work on undead only
            ("CLR/DRU/WIZ", false),   // two healers are one role; DRU's charm works on animals only
            ("WAR/SHD/MNK", false),   // two tanks are one role
        };
        var wrong = cases.Where(x => ClassRoles.HoldsUp(x.Trio.Split('/')) != x.HoldsUp).Select(x => $"{x.Trio} should {(x.HoldsUp ? "" : "not ")}hold up");
        Assert.Empty(wrong);
    }

    /// <summary>
    /// With the switch off nothing changes (the user, 11-19): the old planner's plans, taken from main before this branch,
    /// with "A carry" and "Heals" (BST still a carry), with neither, and with "Heals" alone from ranks part of the way.
    /// </summary>
    [Fact]
    public void TheOldPlannerPlansExactlyAsBefore()
    {
        const string classes = "WAR/CLR/RNG; PAL/SHD/NEC; DRU/MNK/MAG; BRD/SHM/ENC; ROG/WIZ/BST; CLR/RNG/BER; WAR/PAL/NEC; SHD/DRU/MAG; " +
                               "MNK/SHM/ENC; BRD/ROG/BST; RNG/WIZ/BER; WAR/CLR/NEC; PAL/SHD/MAG; DRU/MNK/ENC; BRD/SHM/BST; ROG/WIZ/BER";
        var prefer = new List<string>(Campaign.DefaultPrefer);
        Assert.Equal(classes, Trios(Planned("classes", Campaign.Quality(prefer), prefer).Plan));
        Assert.Equal(classes + "; " + classes, Trios(Planned("all", Campaign.Quality(prefer), prefer).Plan));
        Assert.Equal("BST/CLR/PAL; RNG/SHD/DRU; MNK/BRD/ROG; SHM/NEC/WIZ; MAG/ENC/BER; WAR/CLR/BST; PAL/RNG/SHD; DRU/MNK/BRD; " +
                     "ROG/SHM/NEC; WIZ/MAG/ENC; WAR/BST/BER; CLR/PAL/RNG; SHD/DRU/MNK; BRD/ROG/SHM; NEC/WIZ/ENC; MAG/WAR/BER",
            Trios(Planned("classes", Campaign.Quality([]), []).Plan));

        var part = NoClasses;
        (part["WAR"], part["CLR"], part["RNG"], part["BST"], part["MAG"]) = (2, 1, 3, 1, 2);
        var p = Campaign.Preset("classes");
        p.Prefer = ["heals"];
        p.Loadouts = Campaign.Generate(p, part, NoRaces, null, Campaign.Quality(p.Prefer));
        Assert.Equal("ROG/SHD/DRU; MNK/BRD/SHM; PAL/NEC/WIZ; CLR/ENC/BER; ROG/SHD/DRU; MNK/BRD/SHM; PAL/NEC/WIZ; ENC/BST/BER; " +
                     "WAR/NEC/PAL; MAG/DRU/MNK; BRD/ROG/SHM; CLR/WIZ/ENC; SHD/BST/BER", Trios(p));
    }

    [Theory]
    [InlineData("classes")]
    [InlineData("all")]
    public void EveryTrioHoldsUpAtTheSameRunsAndXp(string preset)
    {
        var old = Planned(preset, Campaign.Quality(Campaign.DefaultPrefer)).View;
        CostsTheSame(old, ByHand(preset, EveryRuleMet).View, "the seating worked out by hand");

        var (plan, view) = Planned(preset, Campaign.RolesQuality(Pathfinders, check: true));
        CostsTheSame(old, view, "the new planner");
        Assert.Empty(plan.Loadouts.Where(l => !HoldsUp(l.Classes)).Select(l => string.Join("/", l.Classes)));
    }

    /// <summary>"One strong slow per trio" is on by default and works whether or not the check runs.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShmAndEncNeverShareATrio(bool check)
    {
        var old = Planned("classes", Campaign.Quality(Campaign.DefaultPrefer)).View;
        var (plan, view) = Planned("classes", Campaign.RolesQuality(Pathfinders, check));
        CostsTheSame(old, view, "the new planner");
        Assert.Empty(plan.Loadouts.Where(l => l.Classes.Contains("SHM") && l.Classes.Contains("ENC")).Select(l => string.Join("/", l.Classes)));
    }

    [Fact]
    public void EveryHasteGiverSitsWithAClassThatHitsWithWeapons()
    {
        var (plan, _) = Planned("classes", Campaign.RolesQuality(Pathfinders, check: true));
        var unhasted = plan.Loadouts.Where(l => l.Classes.Any(GiveHaste.Contains) && !l.Classes.Any(HitWithWeapons.Contains));
        Assert.Empty(unhasted.Select(l => string.Join("/", l.Classes)));
    }

    [Fact]
    public void EachOfYourCarriesGetsATrioOfItsOwn()
    {
        var (plan, _) = Planned("classes", Campaign.RolesQuality(Pathfinders, check: true));
        var shared = plan.Loadouts.Where(l => l.Classes.Count(Pathfinders.Carries.Contains) > 1);
        Assert.Empty(shared.Select(l => string.Join("/", l.Classes)));
        Assert.Equal(12, plan.Loadouts.Count(l => l.Classes.Any(Pathfinders.Carries.Contains)));
    }

    /// <summary>
    /// Carries break ties whether the check runs or not (the user: "Yes, always"), in the player's order: six carries can't
    /// all have a trio of their own in 16 runs, so the first one, WIZ here, keeps its own.
    /// </summary>
    [Fact]
    public void YourFirstCarryKeepsATrioOfItsOwnWhileTheCheckIsQuiet()
    {
        var roles = new CampaignRoles(["WIZ", "MAG", "NEC", "ENC", "RNG", "BER"], true);
        var old = Planned("classes", Campaign.Quality(Campaign.DefaultPrefer)).View;
        var byHand = ByHand("classes", "SHM NEC RNG ENC PAL BER BRD MAG MNK WAR CLR WIZ SHD DRU BST ROG");
        CostsTheSame(old, byHand.View, "the seating worked out by hand");
        Assert.All(byHand.Plan.Loadouts.Where(l => l.Classes.Contains("WIZ")), l => Assert.Single(l.Classes, roles.Carries.Contains));

        var (plan, view) = Planned("classes", Campaign.RolesQuality(roles, check: false));
        CostsTheSame(old, view, "the new planner");
        var wiz = plan.Loadouts.Where(l => l.Classes.Contains("WIZ")).Select(l => string.Join("/", l.Classes)).ToList();
        Assert.Equal(3, wiz.Count);
        Assert.All(wiz, t => Assert.Single(t.Split('/'), roles.Carries.Contains));
    }

    // ------------------------------------------------------------------ where you level

    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0);

    /// <summary>A played relevel: its crawls by difficulty and its open-world minutes, all of its active time.</summary>
    private static Journey Relevel(int daysAgo, double openWorld, params (int Difficulty, double Minutes)[] crawls) => Relevel(daysAgo, true, openWorld, crawls);

    private static Journey Relevel(int daysAgo, bool done, double openWorld, params (int Difficulty, double Minutes)[] crawls)
    {
        var at = T0.AddDays(-daysAgo);
        var route = crawls.Select(c => new JourneyLeg("Befallen", c.Difficulty, 1, 20, 3, c.Minutes, 5, 0)).ToList();
        if (openWorld > 0) route.Add(new JourneyLeg("Commonlands", null, 1, 20, 0, openWorld, 2, 0, "open world"));
        double hours = (crawls.Sum(c => c.Minutes) + openWorld) / 60;
        return new Journey("WAR / CLR / RNG", "CLR/RNG/WAR", "Human", at, at, at.AddHours(hours), 1, 50, done, hours, hours,
            crawls.Length * 3, 100, 0, 0, 49, 1, route, null, null, null, []);
    }

    private static readonly int[] Farmed = [3, 4];

    [Fact]
    public void TriosAreCheckedWhereYouLevelAtADifficultyYouFarm()
    {
        var j = new[] { Relevel(1, 30, (4, 300)), Relevel(5, 0, (4, 200), (0, 100)), Relevel(9, 0, (0, 250)) };
        Assert.Equal(new SurvivalCheck("checked", 4, 1), SurvivalCheck.Of(j, Farmed));   // D4: 300 of 330 minutes
        Assert.True(SurvivalCheck.Of(j, Farmed).Checks);
    }

    [Fact]
    public void TheCheckIsQuietWhereYouLevelAtADifficultyYouDontFarm()
    {
        var j = new[] { Relevel(1, 0, (0, 300)), Relevel(5, 0, (0, 200), (4, 100)) };
        var check = SurvivalCheck.Of(j, Farmed);
        Assert.Equal(new SurvivalCheck("quiet", 0, 1), check);
        Assert.False(check.Checks);
    }

    /// <summary>
    /// Where you level is the relevel Home reads "mostly D4" from (the one on now with crawl minutes, else the latest finished), never
    /// a pool of three: on the user's real data (11-19) the three held D4 40% and D0 46% of the hours, so no difficulty had over half,
    /// while Home said D4 and so did the player.
    /// </summary>
    [Fact]
    public void WhereYouLevelIsWhatHomeCallsMostlyNotAPoolOfThree()
    {
        var now = Relevel(0, false, 0, (4, 40));                                  // 40 min in, all D4
        var last = Relevel(1, 0, (4, 348), (0, 141), (1, 20));                    // D4 348 of 509
        var before = Relevel(2, 0, (1, 83), (2, 36), (0, 309));                   // D0 309 of 428
        Assert.Equal(new SurvivalCheck("checked", 4, 1), SurvivalCheck.Of([now, last, before], Farmed));
        Assert.Equal("D4", now.Mostly);
        // Between relevels (none on now), the latest finished one says it.
        Assert.Equal(new SurvivalCheck("checked", 4, 1), SurvivalCheck.Of([last, before], Farmed));
        // A relevel just begun, with no crawl yet, isn't read: the last finished one is.
        Assert.Equal(new SurvivalCheck("checked", 4, 1), SurvivalCheck.Of([Relevel(0, false, 20), last, before], Farmed));
    }

    /// <summary>The older relevels never outvote the one Home reads.</summary>
    [Fact]
    public void OlderRelevelsDontSayWhereYouLevel()
    {
        var j = new[] { Relevel(1, 0, (0, 200)), Relevel(2, 0, (4, 2000)), Relevel(3, 0, (4, 2000)) };
        Assert.Equal(new SurvivalCheck("quiet", 0, 1), SurvivalCheck.Of(j, Farmed));
    }

    /// <summary>No difficulty over half (open world counts in the hours, as Home counts it), or no relevel yet: the check runs, since it only breaks ties.</summary>
    [Fact]
    public void WithNoDifficultyOverHalfOrNoRelevelYetTriosAreChecked()
    {
        var j = new[] { Relevel(1, 100, (4, 200), (0, 100)), Relevel(4, 0, (4, 50), (0, 250)) };   // D4 200 of 400: exactly half
        Assert.Equal(new SurvivalCheck("spread", null, 1), SurvivalCheck.Of(j, Farmed));
        Assert.Equal(new SurvivalCheck("norelevel", null, 0), SurvivalCheck.Of([], Farmed));
        Assert.Equal(new SurvivalCheck("norelevel", null, 0), SurvivalCheck.Of([Relevel(0, false, 20)], Farmed));
        Assert.True(SurvivalCheck.Of([], Farmed).Checks);
    }

    [Fact]
    public void FarmingNoDifficultyTurnsTheCheckOff()
    {
        var j = new[] { Relevel(1, 0, (4, 300)) };
        var check = SurvivalCheck.Of(j, []);
        Assert.Equal("off", check.State);
        Assert.False(check.Checks);
        Assert.False(SurvivalCheck.Of([], []).Checks);
    }

    /// <summary>"One strong slow per trio": with it on, a trio with SHM and ENC scores far below one without both; off, it doesn't mind.</summary>
    [Theory]
    [InlineData("BRD", "SHM", "ENC")]
    [InlineData("WAR", "SHM", "ENC")]
    public void OneStrongSlowCostsATrioWithBothShmAndEncOnlyWhenItIsOn(string x, string y, string z)
    {
        var on = Campaign.RolesQuality(new CampaignRoles([], true), check: false);
        var off = Campaign.RolesQuality(new CampaignRoles([], false), check: false);
        string[] both = [x, y, z], one = [x, y, "CLR"];
        Assert.True(on(both) < on(one), "on: SHM and ENC together score below a trio without both");
        Assert.Equal(off(both) - 1000, on(both));
        Assert.Equal(off(one), on(one));
    }
}
