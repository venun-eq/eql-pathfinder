using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The wiring of the new planner (docs/specs/campaign-class-roles.md): the tracker uses the class-roles tie-break only
/// when it is handed the roles (the switch is on), and Swap's "a carry" is the player's list, not BST and the old four.
/// </summary>
public class ClassRolesWiringTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    private static async Task WithService(Func<TrackerService, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-roles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        File.WriteAllLines(log, [$"[{Now.AddHours(-1).ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] Welcome to EverQuest!"]);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions { Expansions = [new("Classic", DateTime.MinValue, 50)] }, new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            await body(svc);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    private static string Trios(CampaignPlan p) => string.Join("; ", p.Loadouts.Select(l => string.Join("/", l.Classes)));

    [Fact]
    public async Task TheClassRolesTieBreakDecidesBetweenPlansThatCostTheSame()
    {
        await WithService(async svc =>
        {
            var (fresh, none) = await svc.Query(s =>
            {
                var n = s.RebuildCampaign(Campaign.Preset("all"), new CampaignRoles(["MAG", "NEC", "ENC", "RNG"], true));
                var e = s.RebuildCampaign(Campaign.Preset("all"), new CampaignRoles([], false));
                return (n, e);
            });
            Assert.NotEqual(Trios(fresh), Trios(none));
            // The new plan checks each trio: no trio of ranger, druid and berserker, and never a shaman with an enchanter.
            Assert.DoesNotContain(fresh.Loadouts, l => l.Classes.Contains("SHM") && l.Classes.Contains("ENC"));
            Assert.Equal(none.Loadouts.Sum(l => l.Times), fresh.Loadouts.Sum(l => l.Times));   // never a run more
        });
    }

    /// <summary>
    /// The presets' runs and XP are the same whoever sits where (that is the point of a tie-break), so what the key decides is read from the
    /// key itself: other carries, their order, the toggle or the check's state is another key, and no roles is the old planner's "".
    /// </summary>
    [Fact]
    public void ThePresetCacheKeyDecidesWhatTheRolesAndTheCheckChange()
    {
        var checkedAt4 = new SurvivalCheck("checked", 4, 1);
        string Key(string[] carries, bool slow = true, SurvivalCheck? ck = null) => Campaign.TieKey(new CampaignRoles(carries, slow), ck ?? checkedAt4);
        Assert.Equal("", Campaign.TieKey(null, checkedAt4));
        Assert.NotEqual(Key(["MAG", "NEC", "ENC", "RNG"]), Key(["WIZ"]));
        Assert.NotEqual(Key(["MAG", "NEC"]), Key(["NEC", "MAG"]));
        Assert.NotEqual(Key(["MAG"]), Key(["MAG"], slow: false));
        Assert.NotEqual(Key(["MAG"]), Key(["MAG"], ck: new SurvivalCheck("quiet", 0, 1)));
        Assert.Equal(Key(["MAG"]), Key(["MAG"], ck: new SurvivalCheck("checked", 3, 1)));   // where you level only matters as checked or not
        Assert.NotEqual("", Key([]));
    }

    [Fact]
    public async Task ThePresetCacheIsKeyedOnTheRolesSoACarriesEditBuildsThemAgain()
    {
        await WithService(async svc =>
        {
            await svc.Query(s =>
            {
                var a = s.CampaignData(Campaign.Preset("all"), s.Journeys(), new CampaignRoles(["MAG", "NEC", "ENC", "RNG"], true));
                var b = s.CampaignData(Campaign.Preset("all"), s.Journeys(), new CampaignRoles(["WIZ"], true));
                var c = s.CampaignData(Campaign.Preset("all"), s.Journeys(), new CampaignRoles([], false));
                Assert.NotEmpty(a.Presets); Assert.NotEmpty(b.Presets); Assert.NotEmpty(c.Presets);
                return 0;
            });
        });
    }

    [Fact]
    public async Task APlanRebuildsItsPresetsWhenAnOrderMoves()
    {
        // The presets start from the plan's own lists, so a rank, reorder or Unlock now click on a plan with runs builds them again.
        await WithService(async svc =>
        {
            await svc.Query(s =>
            {
                var roles = new CampaignRoles(["MAG"], true);
                var classes = ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
                var races = ClassNames.Races.ToDictionary(r => r, _ => 0);
                CampaignPlan Planned(params string[] ranked)
                {
                    var p = Campaign.Preset("all");
                    p.ClassOrder = [.. ranked.Select(k => new CampaignWant { Key = k, Target = 3 })];
                    p.RaceOrder = [];
                    p.Loadouts = Campaign.Generate(p, classes, races);
                    return p;
                }
                var a = s.CampaignData(Planned("ROG"), s.Journeys(), roles);
                var b = s.CampaignData(Planned("SHD", "ROG"), s.Journeys(), roles);
                Assert.NotSame(a.Presets, b.Presets);
                return 0;
            });
        });
    }

    [Fact]
    public void SwapSaysACarryFromThePlayersListAndNeverWhenItIsEmpty()
    {
        var none = ClassNames.All.ToDictionary(c => c.Abbrev, _ => 0);
        var noRaces = ClassNames.Races.ToDictionary(r => r, _ => 0);
        var p = Campaign.Preset("classes");
        p.Loadouts = Campaign.Generate(p, none, noRaces);
        static IEnumerable<CampaignDelta> Deltas(CampaignOptions o) => o.Swaps.Select(s => s.Delta)
            .Concat(o.Races.Select(r => r.Delta)).Concat(o.Classes.SelectMany(c => c).Select(c => c.Delta)).OfType<CampaignDelta>();
        bool Says(CampaignDelta d) => d.Lost.Contains("a carry") || d.Gained.Contains("a carry");
        var old = Deltas(Campaign.Options(p, none, noRaces, [], null, 0)!).ToList();
        var empty = Deltas(Campaign.Options(p, none, noRaces, [], null, 0, [])!).ToList();
        var bst = Deltas(Campaign.Options(p, none, noRaces, [], null, 0, ["BST"])!).ToList();
        Assert.Contains(old, Says);       // today's planner: carries are MAG, NEC, ENC, RNG and BST
        Assert.DoesNotContain(empty, Says);
        Assert.Contains(bst, Says);
        Assert.NotEqual(old.Count(Says), bst.Count(Says));
    }
}
