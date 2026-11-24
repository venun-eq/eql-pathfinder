using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The plan Campaign keeps (docs/specs/planner-out-of-beta.md): what a character has stored, the carries kept with it, and
/// the rebuild after a carries edit.
/// </summary>
public class PlannerSwitchTests
{
    /// <summary>settings.json as the app reads and writes it: these fields, the rest carried as they are.</summary>
    private sealed class SettingsFile : IPlannerSettings
    {
        public Dictionary<string, CampaignPlan> Campaigns { get; set; } = new();
        public Dictionary<string, NewCampaignPlan> NewCampaigns { get; set; } = new();
        [JsonExtensionData] public Dictionary<string, JsonElement>? Rest { get; set; }
    }

    // AppSettings writes with these options (AppSettings.Json).
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static SettingsFile Read(string text) => JsonSerializer.Deserialize<SettingsFile>(text, Json)!;
    private static string Write(SettingsFile s) => JsonSerializer.Serialize(s, Json);
    private static string Of(CampaignPlan p) => JsonSerializer.Serialize(p, Json);

    private const string Regular = "Standin_Server", HandEdited = "Other_Server";

    /// <summary>
    /// Today's settings.json, made up: a player 20 runs into a plan with pins, a tick-off and hand edits, in the beta; and
    /// a second character whose plan was edited by hand in the file (a class that isn't one, a rank of 7, 9 runs of a
    /// two-class loadout, a preference that isn't one, fields missing).
    /// </summary>
    private const string Today = """
    {
      "Goal": "balanced",
      "Campaigns": {
        "Standin_Server": {
          "Classes": { "SHD": 3, "ROG": 3, "SHM": 3, "MAG": 2, "NEC": 2, "ENC": 1 },
          "Races": { "Iksar": 3, "Ogre": 2 },
          "Locked": [ "Vah Shir" ],
          "First": [ "class:ROG", "race:Iksar" ],
          "Loadouts": [
            { "Race": "Iksar", "Classes": [ "SHD", "ROG", "SHM" ], "Times": 3, "Pinned": true, "Marked": 0 },
            { "Race": "Ogre", "Classes": [ "MAG", "NEC", "ENC" ], "Times": 2, "Pinned": false, "Marked": 1 },
            { "Race": "Iksar", "Classes": [ "SHD", "MAG", "NEC" ], "Times": 2, "Pinned": false, "Marked": 0 },
            { "Race": "Iksar", "Classes": [ "ROG", "SHM", "SHD" ], "Times": 1, "Pinned": true, "Marked": 0 }
          ],
          "Since": "2026-11-01T12:00:00",
          "Prefer": [ "heals" ]
        },
        "Other_Server": {
          "Classes": { "MAG": 7, "XYZ": 2 },
          "Races": { "Iksar": 3 },
          "Loadouts": [
            { "Race": "Iksar", "Classes": [ "SHD", "ROG" ], "Times": 9, "Pinned": true },
            { "Race": "Iksar", "Classes": [ "MAG", "NEC", "ENC" ], "Times": 3, "Pinned": true }
          ],
          "Prefer": [ "speed", "carry" ]
        }
      }
    }
    """;

    /// <summary>What today's app keeps of the plans when it reads settings.json and saves it again, with no switch at all.</summary>
    private static JsonNode TodaysPlansSaved() => JsonNode.Parse(Write(Read(Today)))!["Campaigns"]!;

    /// <summary>A player's edit in the new planner: every pin off, First cleared, a goal changed, a run gone.</summary>
    private static CampaignPlan Edited(CampaignPlan p)
    {
        foreach (var l in p.Loadouts) l.Pinned = false;
        p.First.Clear();
        p.Classes["MAG"] = 3;
        p.Prefer.Clear();
        if (p.Loadouts.Count > 0) p.Loadouts.RemoveAt(p.Loadouts.Count - 1);
        return p;
    }

    [Fact]
    public void ACharacterWithNoPlanHasNothingBroughtAcrossAndItsNewPlanStaysNew()
    {
        var s = Read(Today);

        var empty = PlannerSwitch.PlanFor(s, "Newcomer_Server");
        Assert.Empty(empty.Loadouts);
        Assert.Empty(empty.Classes);
        Assert.DoesNotContain("Newcomer_Server", s.NewCampaigns.Keys);

        var made = new CampaignPlan { Classes = new(StringComparer.OrdinalIgnoreCase) { ["WAR"] = 1 } };
        PlannerSwitch.Store(s, "Newcomer_Server", made);
        Assert.DoesNotContain("Newcomer_Server", s.Campaigns.Keys);
        Assert.Contains("Newcomer_Server", s.NewCampaigns.Keys);

        Assert.Equal(1, PlannerSwitch.PlanFor(s, "Newcomer_Server").Classes["WAR"]);
    }

    private static string AppDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "EqlPathfinder.App"));

    /// <summary>
    /// Reads the window's source: the app can't be loaded here, and a plan read or saved around the switch would write the
    /// new plan into the old one, or show the old one while the switch is on. Every read and write goes through PlannerSwitch.
    /// </summary>
    [Fact]
    public void TheWindowReadsAndSavesPlansOnlyThroughTheSwitch()
    {
        var files = Directory.GetFiles(AppDir(), "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileName(f) != "AppSettings.cs").ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);
        foreach (var (name, text) in files)
        {
            Assert.DoesNotContain("CampaignFor(", text);
            Assert.DoesNotContain(".Campaigns", text);
        }
        Assert.Contains("PlannerSwitch.PlanFor(", files["MainWindow.xaml.cs"]);
        Assert.Contains("PlannerSwitch.Store(", files["App.Settings.cs"]);
    }

    // ------------------------------------------------------------------ campaign-class-roles: what the new plan keeps

    private static readonly string[] PathfindersCarries = ["MAG", "NEC", "ENC", "RNG"];

    /// <summary>A plan brought across, or one brought across before this draft (no carries stored), starts with Pathfinder's list.</summary>
    [Fact]
    public void ABroughtAcrossPlanStartsWithPathfindersCarriesAndOneStrongSlow()
    {
        Assert.Equal(PathfindersCarries, ClassRoles.DefaultCarries);
        var s = Read(Today);
        s = Read(Write(s));
        Assert.Equal(PathfindersCarries, PlannerSwitch.RolesFor(s, Regular).Carries);
        Assert.True(PlannerSwitch.RolesFor(s, Regular).OneStrongSlow);

        var before = Read("""{ "NewCampaigns": { "Standin_Server": { "Plan": { "Classes": { "WAR": 3 } } } } }""");
        Assert.Equal(PathfindersCarries, PlannerSwitch.RolesFor(before, Regular).Carries);
        Assert.True(PlannerSwitch.RolesFor(before, Regular).OneStrongSlow);
    }

    /// <summary>
    /// The Class roles tab's carries and toggle are kept with the new plan: through a save, an edit on Campaign and Start
    /// again; an empty list stays empty; the old plan never changes.
    /// </summary>
    [Fact]
    public void YourCarriesAndTheToggleAreKeptWithTheNewPlanAndNeverTouchTheOldOne()
    {
        var s = Read(Today);
        Assert.True(PlannerSwitch.StoreRoles(s, Regular, new CampaignRoles(["WIZ", "MAG"], false)), "the carries weren't saved");
        s = Read(Write(s));
        Assert.Equal(["WIZ", "MAG"], PlannerSwitch.RolesFor(s, "STANDIN_SERVER").Carries);
        Assert.False(PlannerSwitch.RolesFor(s, Regular).OneStrongSlow);

        PlannerSwitch.Store(s, Regular, Edited(PlannerSwitch.PlanFor(s, Regular)));
        Assert.Equal(["WIZ", "MAG"], PlannerSwitch.RolesFor(s, Regular).Carries);
        Assert.False(PlannerSwitch.RolesFor(s, Regular).OneStrongSlow);
        Assert.Equal(["WIZ", "MAG"], PlannerSwitch.RolesFor(Read(Write(s)), Regular).Carries);

        PlannerSwitch.StoreRoles(s, Regular, new CampaignRoles([], true));
        Assert.Empty(PlannerSwitch.RolesFor(Read(Write(s)), Regular).Carries);

        var saved = JsonNode.Parse(Write(s))!;
        Assert.True(JsonNode.DeepEquals(TodaysPlansSaved(), saved["Campaigns"]), $"the old plans changed:\n{saved["Campaigns"]}");
    }

    /// <summary>A hand-edited settings.json: a class that isn't one is dropped, a name or a lower case becomes the class, a repeat goes.</summary>
    [Fact]
    public void AHandEditedListKeepsOnlyRealClassesOnce()
    {
        var s = Read("""
        {
          "NewCampaigns": { "Standin_Server": { "Plan": { }, "Carries": [ "mag", "XYZ", "MAG", "Shaman", "" ], "OneStrongSlow": false } } }
        """);
        Assert.Equal(["MAG", "SHM"], PlannerSwitch.RolesFor(s, Regular).Carries);
        Assert.False(PlannerSwitch.RolesFor(s, Regular).OneStrongSlow);
    }

    // ------------------------------------------------------------------ a carries edit while a Campaign edit rebuilds

    private static SettingsFile OnWithAPlan(string key, params string[] first)
    {
        var s = new SettingsFile();
        var plan = new CampaignPlan { Loadouts = [new CampaignLoadout { Race = "Iksar", Classes = ["SHD", "ROG", "SHM"] }], First = [.. first] };
        PlannerSwitch.Store(s, key, plan);
        return s;
    }

    /// <summary>
    /// A Campaign edit rebuilt around the old carries (A) lands while the carries edit (B) is rebuilding: B's rebuild no longer
    /// matches the stored plan, and used to be dropped, leaving carries B and a plan built around A. It builds again from the stored plan.
    /// </summary>
    [Fact]
    public async Task ACarriesEditBuildsAgainWhenACampaignEditSavedAPlanBuiltAroundTheOldCarries()
    {
        var s = OnWithAPlan("Me_Server");
        var a = new CampaignRoles(["MAG"], true);
        var b = new CampaignRoles(["WIZ"], true);
        PlannerSwitch.StoreRoles(s, "Me_Server", a);
        PlannerSwitch.StoreRoles(s, "Me_Server", b);   // the carries edit: B is saved
        int calls = 0;
        // A fake rebuild: the plan's "First" says which carries it was built around.
        Task<CampaignPlan> Rebuild(CampaignPlan plan, CampaignRoles roles)
        {
            if (++calls == 1)   // while it runs, the Campaign edit's plan (built around A) is saved
                PlannerSwitch.Store(s, "Me_Server", new CampaignPlan { Loadouts = plan.Loadouts, First = ["class:" + a.Carries[0]] });
            return Task.FromResult(new CampaignPlan { Loadouts = plan.Loadouts, First = ["class:" + roles.Carries[0]] });
        }
        Assert.True(await PlannerSwitch.RebuildAround(s, "Me_Server", Rebuild));
        Assert.Equal(2, calls);
        Assert.Equal(["class:WIZ"], PlannerSwitch.PlanFor(s, "Me_Server").First);
    }

    [Fact]
    public async Task ALaterCarriesEditLeavesTheRebuildToItsOwnAndAPlanThatKeepsChangingStopsAtThreeTries()
    {
        var s = OnWithAPlan("Me_Server");
        PlannerSwitch.StoreRoles(s, "Me_Server", new CampaignRoles(["WIZ"], true));
        int calls = 0;
        // Every rebuild is overtaken by another save of the plan: it gives up after three, nothing of its own saved.
        Task<CampaignPlan> Churn(CampaignPlan plan, CampaignRoles roles)
        {
            PlannerSwitch.Store(s, "Me_Server", new CampaignPlan { Loadouts = plan.Loadouts, First = [new[] { "class:MAG", "class:NEC", "class:ENC" }[calls++]] });
            return Task.FromResult(new CampaignPlan { Loadouts = plan.Loadouts, First = ["class:BST"] });
        }
        Assert.False(await PlannerSwitch.RebuildAround(s, "Me_Server", Churn));
        Assert.Equal(3, calls);
        // A carries edit during the rebuild: that edit's own rebuild lands, so this one saves nothing.
        var s2 = OnWithAPlan("Me_Server");
        Assert.False(await PlannerSwitch.RebuildAround(s2, "Me_Server", (plan, _) =>
        {
            PlannerSwitch.StoreRoles(s2, "Me_Server", new CampaignRoles(["CLR"], true));
            return Task.FromResult(new CampaignPlan { Loadouts = plan.Loadouts, First = ["class:RNG"] });
        }));
        Assert.DoesNotContain("class:RNG", PlannerSwitch.PlanFor(s2, "Me_Server").First);
    }
}
