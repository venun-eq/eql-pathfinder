using System.Text.Json;
using System.Text.Json.Nodes;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Pages;
using EqlPathfinder.Engine.Storage;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The wiring from the tracker to the race (docs/specs/journey-race-in-engine.md): <c>ReincarnationData</c> hands <see cref="JourneyRace.For"/>
/// the open relevel's index (from Home's state) and the campaign's next run, so a wrong argument there shows here and not only on a page.
/// </summary>
public class JourneyRaceWiringTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    private static string Stamp(DateTime t) => t.ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task TheRaceIsMadeForTheOpenRelevelsIndexAndTheCampaignsNextRunXp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pf-jrw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var log = Path.Combine(dir, "eqlog_Venun_oggok.txt");
        var t0 = Now.AddHours(-5);
        string L(double m, string text) => $"[{Stamp(t0.AddMinutes(m))}] {text}";
        var lines = new List<string>();
        // Two relevels, then back on the older one, so the relevel open now is not journey 0.
        void Relevel(double at, string[] classes, string trio, string zone)
        {
            foreach (var c in classes) lines.Add(L(at, $"You have gained the ability \"Class Reincarnation: {c}\" at a cost of 0 ability points."));
            lines.Add(L(at + 1, $"[20 {trio}] Venun (Ogre) ZONE: {zone}"));
            lines.Add(L(at + 2, "You have entered Befallen 1 (Refined)."));
            for (int k = 0; k < 24; k++) { lines.Add(L(at + 3 + k * 0.5, "You gain experience! (3.000%)")); lines.Add(L(at + 3 + k * 0.5, "You have slain a lava guardian!")); }
            lines.Add(L(at + 10, "You have gained a level! Welcome to level 21!"));
            lines.Add(L(at + 17, "You have completed the Dungeon Crawl and earned reward loot!"));
        }
        Relevel(0, ["Warrior", "Cleric", "Ranger"], "WAR/CLR/RNG", "Befallen");
        Relevel(120, ["Monk", "Paladin", "Druid"], "MNK/PAL/DRU", "Befallen");
        lines.Add(L(300, "[21 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"));   // back on the first one: the open relevel is not the newest
        File.WriteAllLines(log, lines);
        try
        {
            using var store = new PathfinderStore(Path.Combine(dir, "t.db"));
            using var svc = new TrackerService(store, new CharacterLogs("Venun", "oggok", log, [log]), new TrackerOptions { Expansions = [new("Classic", DateTime.MinValue, 50)] }, new FixedClock(Now));
            await svc.ImportHistoryAsync();
            svc.Start();
            var plan = new CampaignPlan { Classes = { ["MAG"] = 1 } };
            var data = await svc.Query(s =>
            {
                plan = s.RebuildCampaign(plan, new CampaignRoles([], false));
                return JsonSerializer.SerializeToNode(s.ReincarnationData(plan), PageJson.Options)!.AsObject();
            });
            var index = data["homeNow"]!["now"]!["journeyIndex"]!.GetValue<int>();
            var nextXp = data["campaign"]!["runs"]!.AsArray().First(r => r!["state"]!.GetValue<string>() == "next")!["xp"]!.GetValue<double>();
            Assert.Equal(1, index);
            Assert.True(nextXp > 0);
            var races = data["journeyRaces"]!;
            Assert.Equal(index, races["now"]!["now"]!["journey"]!.GetValue<int>());
            Assert.Equal(nextXp, races["next"]!["xp"]!.GetValue<double>());
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
