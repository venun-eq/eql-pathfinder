using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The new planner out of beta (docs/specs/planner-out-of-beta.md): everyone plans on the new planner, in the beta or not; no
/// old plan is shown or brought across; a plan made in the beta is the player's plan; and the one-time card's Got it removes
/// every old plan and nothing else.
/// </summary>
public class PlannerOutOfBetaTests
{
    /// <summary>settings.json as the app reads and writes it: these fields, the rest carried as they are.</summary>
    private sealed class SettingsFile : IPlannerSettings
    {
        public bool Beta { get; set; }
        public bool NewPlanner { get; set; }
        public Dictionary<string, CampaignPlan> Campaigns { get; set; } = new();
        public Dictionary<string, NewCampaignPlan> NewCampaigns { get; set; } = new();
        [JsonExtensionData] public Dictionary<string, JsonElement>? Rest { get; set; }
    }

    // AppSettings writes with these options (AppSettings.Json).
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static SettingsFile Read(string text) => JsonSerializer.Deserialize<SettingsFile>(text, Json)!;
    private static string Write(SettingsFile s) => JsonSerializer.Serialize(s, Json);
    private static string Of(CampaignPlan p) => JsonSerializer.Serialize(p, Json);
    private static JsonObject Saved(SettingsFile s) => JsonNode.Parse(Write(s))!.AsObject();

    private const string Regular = "Standin_Server", Alt = "Alt_Server";

    private const string OldPlan = """
        { "Classes": { "SHD": 3, "ROG": 2 }, "Races": { "Iksar": 3 }, "First": [ "class:ROG" ],
          "Loadouts": [ { "Race": "Iksar", "Classes": [ "SHD", "ROG", "SHM" ], "Times": 2, "Pinned": true, "Marked": 1 } ],
          "Since": "2026-11-01T12:00:00", "Prefer": [ "carry" ] }
        """;

    /// <summary>Made up: a player who never joined the beta, with an old plan on two characters.</summary>
    private const string NeverInTheBeta = $$"""
        { "Beta": false, "Goal": "balanced", "Campaigns": { "Standin_Server": {{OldPlan}}, "Alt_Server": {{OldPlan}} } }
        """;

    /// <summary>
    /// Made up: a beta player who made a new plan and then turned the switch off (their new plan's key in another case), and
    /// whose other character has only carries kept (an entry with no plan, as Class roles leaves one), both over old plans.
    /// </summary>
    private const string BetaSwitchedBack = $$"""
        {
          "Beta": true, "NewPlanner": false, "Goal": "balanced",
          "Campaigns": { "Standin_Server": {{OldPlan}}, "Alt_Server": {{OldPlan}} },
          "NewCampaigns": {
            "standin_server": {
              "Plan": { "Classes": { "MAG": 3, "NEC": 2 }, "Races": { "Ogre": 3 },
                        "Loadouts": [ { "Race": "Ogre", "Classes": [ "MAG", "NEC", "ENC" ], "Times": 3 } ],
                        "Combos": [ { "Classes": [ "MAG", "NEC" ], "Times": 2, "Must": true } ],
                        "ClassOrder": [ { "Key": "MAG", "Target": 3 } ], "RaceOrder": [] },
              "Carries": [ "NEC", "MAG" ], "Card": "seen", "Lists": 1
            },
            "Alt_Server": { "Plan": null, "Carries": [ "ENC" ] }
          }
        }
        """;

    /// <summary>Made up: a new player, and a player whose only plan is one made in the beta.</summary>
    private const string NoPlan = """{ "Beta": false }""";
    private const string OnlyANewPlan = """
        { "Beta": true, "NewPlanner": true, "NewCampaigns": { "Standin_Server": { "Plan": { "Classes": { "WAR": 1 } }, "Lists": 1 } } }
        """;

    private static void IsEmptyPlan(CampaignPlan p, string who)
    {
        Assert.True(p.Classes.Count == 0 && p.Races.Count == 0 && p.Loadouts.Count == 0 && p.First.Count == 0 && (p.Combos ?? []).Count == 0,
            $"{who}: Campaign shows the old plan, or something brought across from it:\n{Of(p)}");
    }

    [Fact]
    public void EveryoneGetsTheNewPlannerAndClassRolesInTheBetaOrNot()
    {
        foreach (var (name, text) in new[] { ("never in the beta", NeverInTheBeta), ("a beta player switched back", BetaSwitchedBack), ("no plan", NoPlan) })
        {
            var s = Read(text);
            var input = PlannerSwitch.PageInput(s, Regular);
            Assert.True(input is { Roles: not null }, $"{name}: Campaign isn't the new planner (the page gets newPlanner only with the carries)");
            Assert.True(PlannerSwitch.StoreRoles(s, Regular, new CampaignRoles(["RNG"], false)), $"{name}: Class roles didn't save the carries");
            s = Read(Write(s));
            Assert.Equal(["RNG"], PlannerSwitch.RolesFor(s, Regular).Carries);
            Assert.False(PlannerSwitch.RolesFor(s, Regular).OneStrongSlow);
        }
    }

    [Fact]
    public void AnOldPlanIsNeitherShownNorBroughtAcross()
    {
        var never = Read(NeverInTheBeta);
        IsEmptyPlan(PlannerSwitch.PlanFor(never, Regular), "never in the beta");
        IsEmptyPlan(PlannerSwitch.PlanFor(never, Alt), "never in the beta, the second character");
        Assert.Empty(never.NewCampaigns);   // a read writes nothing

        var beta = Read(BetaSwitchedBack);
        beta.NewPlanner = true;             // even with the old switch on, an entry with no plan isn't filled from the old one
        IsEmptyPlan(PlannerSwitch.PlanFor(beta, Alt), "the beta player's character with carries only");
    }

    [Fact]
    public void APlanMadeInTheBetaIsThePlayersPlanWhetherItsSwitchWasOnOrOff()
    {
        var on = Read(BetaSwitchedBack);
        on.NewPlanner = true;
        var left = PlannerSwitch.PlanFor(on, Regular);   // today's new planner, switch on: the plan as the player left it
        Assert.Equal(3, left.Classes["MAG"]);

        foreach (var (beta, newPlanner) in new[] { (true, false), (false, false), (false, true) })
        {
            var s = Read(BetaSwitchedBack);
            s.Beta = beta;
            s.NewPlanner = newPlanner;
            Assert.True(Of(left) == Of(PlannerSwitch.PlanFor(s, "STANDIN_SERVER")),
                $"Beta {beta}, switch {newPlanner}: Campaign doesn't show the plan made in the beta as it was left");
            Assert.Equal(["NEC", "MAG"], PlannerSwitch.RolesFor(s, Regular).Carries);
        }
    }

    [Fact]
    public void AnEditIsSavedToTheNewPlanForEveryoneAndLeavesTheOldPlansForGotIt()
    {
        var s = Read(NeverInTheBeta);
        var oldPlans = Saved(s)["Campaigns"]!.DeepClone();

        var edit = PlannerSwitch.PlanFor(s, Regular);
        edit.Classes["WAR"] = 3;
        PlannerSwitch.Store(s, Regular, edit);
        s = Read(Write(s));

        Assert.True(s.NewCampaigns.Keys.Any(k => k.Equals(Regular, StringComparison.OrdinalIgnoreCase)), "the edit wasn't saved as the new plan");
        Assert.Equal(3, PlannerSwitch.PlanFor(s, Regular).Classes.GetValueOrDefault("WAR"));
        Assert.True(JsonNode.DeepEquals(oldPlans, Saved(s)["Campaigns"]), "an edit changed the old plans");
        Assert.True(PlannerSwitch.HasOldPlans(s), "an edit before Got it took the card away");
    }

    [Fact]
    public void TheCardShowsToAnyoneWithAnOldPlanUntilGotItWhichRemovesEveryOldPlanAndNothingElse()
    {
        foreach (var (name, text) in new[] { ("never in the beta", NeverInTheBeta), ("a beta player switched back", BetaSwitchedBack) })
        {
            var s = Read(text);
            Assert.True(PlannerSwitch.HasOldPlans(s), $"{name}: no card for a player with old plans");
            var before = Saved(s);
            var shown = new[] { Regular, Alt }.Select(c => Of(PlannerSwitch.PlanFor(s, c))).ToList();

            PlannerSwitch.RemoveOldPlans(s);   // Got it, on whichever character Campaign opened
            var after = Saved(s);

            Assert.True((after["Campaigns"] as JsonObject)?.Count is null or 0, $"{name}: Got it left old plans:\n{after["Campaigns"]}");
            foreach (var (key, value) in before)
                if (key != "Campaigns")
                    Assert.True(JsonNode.DeepEquals(value, after[key]), $"{name}: Got it changed {key}:\n{after[key]}\n\nwas:\n{value}");

            s = Read(Write(s));                // saved, and the app started again
            Assert.False(PlannerSwitch.HasOldPlans(s), $"{name}: the card came back after Got it");
            Assert.Equal(shown, new[] { Regular, Alt }.Select(c => Of(PlannerSwitch.PlanFor(s, c))).ToList());
        }

        Assert.False(PlannerSwitch.HasOldPlans(Read(NoPlan)), "a new player sees the card");
        Assert.False(PlannerSwitch.HasOldPlans(Read(OnlyANewPlan)), "a player with only a plan made in the beta sees the card");
    }

    /// <summary>
    /// Got it pressed while a Class roles rebuild is out: the two write different sides, so the rebuilt plan still lands on
    /// the new plan, and the old plans are still gone.
    /// </summary>
    [Fact]
    public async Task GotItDuringACarriesRebuildLeavesTheRebuiltPlanSaved()
    {
        var s = Read(BetaSwitchedBack);
        s.Beta = false;
        s.NewPlanner = false;
        var saved = await PlannerSwitch.RebuildAround(s, Regular, (plan, roles) =>
        {
            PlannerSwitch.RemoveOldPlans(s);   // Got it, while the rebuild is out
            plan.Classes["WAR"] = 3;
            return Task.FromResult(plan);
        });
        Assert.True(saved, "Got it during a rebuild dropped the rebuilt plan");
        s = Read(Write(s));
        Assert.Equal(3, PlannerSwitch.PlanFor(s, Regular).Classes.GetValueOrDefault("WAR"));
        Assert.Equal(3, PlannerSwitch.PlanFor(s, Regular).Classes.GetValueOrDefault("MAG"));
        Assert.Empty(s.Campaigns);
    }

    private static string AppDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "EqlPathfinder.App"));

    /// <summary>
    /// Reads the window's source, which the tests can't load: the card's Got it must reach the engine's removal (and the
    /// window saves after it), and nothing in the window turns a beta switch or brings a plan across any more.
    /// </summary>
    [Fact]
    public void GotItInTheWindowRemovesTheOldPlansAndNoSwitchIsLeft()
    {
        var app = string.Join("\n", Directory.GetFiles(AppDir(), "App*.cs")
            .Where(f => Path.GetFileName(f) != "AppSettings.cs")
            .Select(File.ReadAllText));
        Assert.Contains("PlannerSwitch.RemoveOldPlans(", app);
        foreach (var gone in new[] { "PlannerSwitch.SetOn(", "PlannerSwitch.StartAgain(", "PlannerSwitch.CardSeen(" })
            Assert.DoesNotContain(gone, app);
    }
}
